using SpotifyAPI.Web;
using MusicGrabber.Models;

namespace MusicGrabber.Services;

public class SpotifyService
{
    private const string RedirectUri = "http://localhost:5543/callback";
    private SpotifyClient? _client;
    private string? _verifier;

    public bool IsAuthenticated => _client != null;

    public (Uri loginUri, string verifier) CreateLoginRequest(string clientId)
    {
        var (verifier, challenge) = PKCEUtil.GenerateCodes();
        _verifier = verifier;

        var request = new LoginRequest(new Uri(RedirectUri), clientId, LoginRequest.ResponseType.Code)
        {
            CodeChallengeMethod = "S256",
            CodeChallenge = challenge,
            Scope =
            [
                Scopes.PlaylistReadPrivate,
                Scopes.PlaylistReadCollaborative,
                Scopes.UserLibraryRead
            ]
        };

        return (request.ToUri(), verifier);
    }

    public async Task CompleteLoginAsync(string clientId, string code)
    {
        var response = await new OAuthClient().RequestToken(
            new PKCETokenRequest(clientId, code, new Uri(RedirectUri), _verifier!));

        _client = new SpotifyClient(response.AccessToken);
    }

    public async Task<List<PlaylistInfo>> GetUserPlaylistsAsync()
    {
        EnsureAuthenticated();
        var playlists = new List<PlaylistInfo>();
        var page = await _client!.Playlists.CurrentUsers();

        await foreach (var item in _client.Paginate(page))
        {
            playlists.Add(new PlaylistInfo
            {
                Id = item.Id!,
                Name = item.Name!,
                Owner = item.Owner?.DisplayName ?? "Unknown",
                TrackCount = item.Tracks?.Total ?? 0
            });
        }

        return playlists;
    }

    public async Task<List<AlbumInfo>> GetSavedAlbumsAsync()
    {
        EnsureAuthenticated();
        var albums = new List<AlbumInfo>();
        var page = await _client!.Library.GetAlbums();

        await foreach (var item in _client.Paginate(page))
        {
            albums.Add(new AlbumInfo
            {
                Id = item.Album.Id!,
                Name = item.Album.Name,
                Artist = string.Join(", ", item.Album.Artists.Select(a => a.Name)),
                TrackCount = item.Album.Tracks?.Total ?? 0,
                ImageUrl = item.Album.Images?.FirstOrDefault()?.Url
            });
        }

        return albums;
    }

    public async Task<List<TrackInfo>> GetPlaylistTracksAsync(string playlistId)
    {
        EnsureAuthenticated();
        var tracks = new List<TrackInfo>();
        var page = await _client!.Playlists.GetItems(playlistId);

        await foreach (var item in _client.Paginate(page))
        {
            if (item.Track is FullTrack track)
            {
                tracks.Add(MapTrack(track));
            }
        }

        return tracks;
    }

    public async Task<List<TrackInfo>> GetAlbumTracksAsync(string albumId)
    {
        EnsureAuthenticated();
        var album = await _client!.Albums.Get(albumId);
        var tracks = new List<TrackInfo>();

        await foreach (var item in _client.Paginate(album.Tracks))
        {
            tracks.Add(new TrackInfo
            {
                Title = item.Name,
                Artist = string.Join(", ", item.Artists.Select(a => a.Name)),
                Album = album.Name,
                SpotifyUri = item.Uri,
                Duration = TimeSpan.FromMilliseconds(item.DurationMs)
            });
        }

        return tracks;
    }

    public void Logout()
    {
        _client = null;
        _verifier = null;
    }

    private static TrackInfo MapTrack(FullTrack track) => new()
    {
        Title = track.Name,
        Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
        Album = track.Album?.Name ?? "Unknown",
        SpotifyUri = track.Uri,
        Duration = TimeSpan.FromMilliseconds(track.DurationMs)
    };

    private void EnsureAuthenticated()
    {
        if (_client == null)
            throw new InvalidOperationException("Not authenticated. Call CompleteLoginAsync first.");
    }
}
