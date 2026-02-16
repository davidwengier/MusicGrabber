using MusicGrabber.Forms;

namespace MusicGrabber.Services;

/// <summary>
/// Bridges the Blazor UI to WinForms dialogs (OAuth login, folder picker, etc.).
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

        // BeginInvoke to avoid deadlocking the UI thread
        _owner.BeginInvoke(() =>
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

    public Task<string?> BrowseForFolderAsync(string? initialPath = null)
    {
        var tcs = new TaskCompletionSource<string?>();

        _owner.BeginInvoke(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select download folder",
                UseDescriptionForTitle = true
            };
            if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                dialog.InitialDirectory = initialPath;

            if (dialog.ShowDialog(_owner) == DialogResult.OK)
                tcs.SetResult(dialog.SelectedPath);
            else
                tcs.SetResult(null);
        });

        return tcs.Task;
    }
}
