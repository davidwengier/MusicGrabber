namespace MusicGrabber.Models;

public enum FolderNaming
{
    AlbumOnly,
    ArtistAlbum
}

public class TrackInfo
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string SpotifyUri { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int TrackNumber { get; set; }
    public string? LocalFilePath { get; set; }

    public string SearchQuery => $"{Artist} - {Title}";

    public string SafeFileName
    {
        get
        {
            var name = $"{TrackNumber:D2} - {Title}.mp3";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }

    public override string ToString() => $"{Artist} - {Title} ({Album})";
}
