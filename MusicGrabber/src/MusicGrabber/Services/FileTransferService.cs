using MusicGrabber.Models;

namespace MusicGrabber.Services;

public class FileTransferService
{
    public event Action<string>? LogMessage;

    public async Task TransferTracksAsync(
        IReadOnlyList<TrackInfo> tracks,
        string targetRootPath,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.GetPathRoot(targetRootPath)))
            throw new DirectoryNotFoundException($"Drive not found: {targetRootPath}");

        Directory.CreateDirectory(targetRootPath);

        var tracksWithFiles = tracks.Where(t => t.LocalFilePath != null && File.Exists(t.LocalFilePath)).ToList();

        if (tracksWithFiles.Count == 0)
        {
            Log("No downloaded files to transfer.");
            return;
        }

        for (int i = 0; i < tracksWithFiles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var track = tracksWithFiles[i];
            progress?.Report((i + 1, tracksWithFiles.Count));

            var destPath = Path.Combine(targetRootPath, track.SafeFileName);

            if (File.Exists(destPath))
            {
                var srcInfo = new FileInfo(track.LocalFilePath!);
                var destInfo = new FileInfo(destPath);
                if (srcInfo.Length == destInfo.Length)
                {
                    Log($"Skipping (already on device): {track.SafeFileName}");
                    continue;
                }
            }

            Log($"Copying: {track.SafeFileName}");
            await Task.Run(() => File.Copy(track.LocalFilePath!, destPath, overwrite: true), cancellationToken);
            Log($"  ✓ Copied to device");
        }

        Log($"Transfer complete. {tracksWithFiles.Count} tracks on device.");
    }

    public static List<DriveInfo> GetRemovableDrives()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType is DriveType.Removable or DriveType.Fixed)
            .Where(d => d.Name != Path.GetPathRoot(Environment.SystemDirectory))
            .ToList();
    }

    private void Log(string message) => LogMessage?.Invoke(message);
}
