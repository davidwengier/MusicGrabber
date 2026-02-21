using System.Diagnostics;
using SpotifyExplorer.Models;

namespace SpotifyExplorer.Services;

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
        int maxParallelDownloads = 2,
        bool includeArtistInFileName = false,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var trackDir = string.IsNullOrWhiteSpace(subfolder)
            ? outputDir
            : Path.Combine(outputDir, SanitizePath(subfolder));
        Directory.CreateDirectory(trackDir);

        maxParallelDownloads = Math.Max(1, maxParallelDownloads);
        var completedCount = 0;
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = maxParallelDownloads
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, tracks.Count), options, async (i, ct) =>
        {
            var track = tracks[i];

            try
            {
                var outputPath = Path.Combine(trackDir, track.GetFileName(includeArtistInFileName));

                if (File.Exists(outputPath))
                {
                    Log($"Skipping (already exists): {track}");
                    track.LocalFilePath = outputPath;
                    TrackCompleted?.Invoke(track, true, null);
                    return;
                }

                Log($"Downloading: {track.SearchQuery}");
                await RunYtDlpAsync(track.SearchQuery, outputPath, ytDlpPath, ffmpegPath, ct);

                if (File.Exists(outputPath))
                {
                    await WriteId3TagsAsync(outputPath, track, ffmpegPath, ct);
                    track.LocalFilePath = outputPath;
                    Log($"  ✓ Saved: {track.GetFileName(includeArtistInFileName)}");
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
            finally
            {
                progress?.Report((Interlocked.Increment(ref completedCount), tracks.Count));
            }
        });
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

    private async Task WriteId3TagsAsync(
        string filePath,
        TrackInfo track,
        string ffmpegPath,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(
            Path.GetDirectoryName(filePath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(filePath)}.tagged{Path.GetExtension(filePath)}");

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(filePath);
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:a");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("copy");
        psi.ArgumentList.Add("-id3v2_version");
        psi.ArgumentList.Add("3");
        if (!string.IsNullOrWhiteSpace(track.Title))
        {
            psi.ArgumentList.Add("-metadata");
            psi.ArgumentList.Add($"title={track.Title}");
        }
        if (!string.IsNullOrWhiteSpace(track.Artist))
        {
            psi.ArgumentList.Add("-metadata");
            psi.ArgumentList.Add($"artist={track.Artist}");
        }
        if (!string.IsNullOrWhiteSpace(track.Album))
        {
            psi.ArgumentList.Add("-metadata");
            psi.ArgumentList.Add($"album={track.Album}");
        }
        if (track.TrackNumber > 0)
        {
            psi.ArgumentList.Add("-metadata");
            psi.ArgumentList.Add($"track={track.TrackNumber}");
        }
        psi.ArgumentList.Add(tempPath);

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start ffmpeg at: {ffmpegPath}");
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !File.Exists(tempPath))
            {
                throw new InvalidOperationException(
                    $"ffmpeg exited with code {process.ExitCode}: {stderr.Trim()}");
            }

            File.Copy(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static string SanitizePath(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
