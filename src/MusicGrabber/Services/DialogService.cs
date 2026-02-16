namespace MusicGrabber.Services;

public class DialogService
{
    private readonly Form _owner;

    public DialogService(Form owner) => _owner = owner;

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
