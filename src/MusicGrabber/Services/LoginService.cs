using MusicGrabber.Forms;
using MusicGrabber.Services;

namespace MusicGrabber.Services;

/// <summary>
/// Bridges the Blazor UI to the WinForms LoginForm dialog for Spotify OAuth.
/// </summary>
public class LoginService
{
    private readonly SpotifyService _spotify;
    private readonly Form _owner;

    public LoginService(SpotifyService spotify, Form owner)
    {
        _spotify = spotify;
        _owner = owner;
    }

    public Task<string?> LoginAsync(string clientId)
    {
        var tcs = new TaskCompletionSource<string?>();

        _owner.Invoke(() =>
        {
            var (uri, _) = _spotify.CreateLoginRequest(clientId);
            using var loginForm = new LoginForm(uri);
            if (loginForm.ShowDialog(_owner) == DialogResult.OK)
                tcs.SetResult(loginForm.AuthorizationCode);
            else
                tcs.SetResult(null);
        });

        return tcs.Task;
    }
}
