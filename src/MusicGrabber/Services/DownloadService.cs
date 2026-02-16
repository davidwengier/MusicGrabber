using System.Diagnostics;
using MusicGrabber.Models;

namespace MusicGrabber.Services;

public class DownloadService
{
    public event Action<string>? LogMessage;
    public event Action<TrackInfo, bool, string?>? TrackCompleted;

    public async Task DownloadTracksAsync(
        IReadOnlyList<TrackInfo> tracks,
        string outputDir,
        string? subfolder,
        string ytDlpPath,
        string ffmpegPath,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var trackDir = string.IsNullOrWhiteSpace(subfolder)
            ? outputDir
            : Path.Combine(outputDir, SanitizePath(subfolder));
        Directory.CreateDirectory(trackDir);

        for (int i = 0; i < tracks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var track = tracks[i];
            progress?.Report((i + 1, tracks.Count));

            try
            {
                var outputPath = Path.Combine(trackDir, track.SafeFileName);

                if (File.Exists(outputPath))
                {
                    Log($"Skipping (already exists): {track}");
                    track.LocalFilePath = outputPath;
                    TrackCompleted?.Invoke(track, true, null);
                    continue;
                }

                Log($"Downloading: {track.SearchQuery}");
                await RunYtDlpAsync(track.SearchQuery, outputPath, ytDlpPath, ffmpegPath, cancellationToken);

                if (File.Exists(outputPath))
                {
                    track.LocalFilePath = outputPath;
                    Log($"  ✓ Saved: {track.SafeFileName}");
                    TrackCompleted?.Invoke(track, true, null);
                }
                else
                {
                    Log($"  ✗ Failed: file not created");
                    TrackCompleted?.Invoke(track, false, "File not created by yt-dlp");
                }
            }
            catch (Exception ex)
            {
                Log($"  ✗ Error: {ex.Message}");
                TrackCompleted?.Invoke(track, false, ex.Message);
            }
        }
    }

    private async Task RunYtDlpAsync(
        string searchQuery, string outputPath, string ytDlpPath, string ffmpegPath,
        CancellationToken cancellationToken)
    {
        // Resolve the YouTube URL first so we can log it and skip a second search
        var url = await ResolveYouTubeUrlAsync(searchQuery, ytDlpPath, cancellationToken);
        if (url != null)
            Log($"  → {url}");

        var args = string.Join(" ", [
            url != null ? $"\"{url}\"" : $"ytsearch1:\"{searchQuery}\"",
            "--extract-audio",
            "--audio-format mp3",
            "--audio-quality 0",
            $"--ffmpeg-location \"{ffmpegPath}\"",
            "-o", $"\"{outputPath}\"",
            "--no-playlist",
            "--quiet",
            "--no-warnings"
        ]);

        Log($"  $ {ytDlpPath} {args}");

        var psi = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start yt-dlp at: {ytDlpPath}");

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"yt-dlp exited with code {process.ExitCode}: {stderr.Trim()}");
        }
    }

    private async Task<string?> ResolveYouTubeUrlAsync(
        string searchQuery, string ytDlpPath, CancellationToken ct)
    {
        var resolveArgs = $"ytsearch1:\"{searchQuery}\" --print webpage_url --no-download --no-warnings";
        Log($"  $ {ytDlpPath} {resolveArgs}");

        var psi = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = resolveArgs,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return null;
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var url = output.Trim();
            return url.StartsWith("http") ? url : null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsYtDlpAvailable(string ytDlpPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private void Log(string message) => LogMessage?.Invoke(message);

    private static string SanitizePath(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
