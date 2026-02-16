using System.IO.Compression;

namespace MusicGrabber.Services;

public class ToolDownloadService
{
    private static readonly string ToolsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicGrabber", "tools");

    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string FfmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

    public event Action<string>? LogMessage;

    public string YtDlpPath => Path.Combine(ToolsDir, "yt-dlp.exe");
    public string FfmpegPath => Path.Combine(ToolsDir, "ffmpeg.exe");

    public bool YtDlpExists => File.Exists(YtDlpPath);
    public bool FfmpegExists => File.Exists(FfmpegPath);

    public async Task DownloadYtDlpAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(ToolsDir);
        Log("Downloading yt-dlp...");

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(5);

        var bytes = await http.GetByteArrayAsync(YtDlpUrl, ct);
        await File.WriteAllBytesAsync(YtDlpPath, bytes, ct);

        Log($"yt-dlp downloaded to {YtDlpPath}");
    }

    public async Task DownloadFfmpegAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ToolsDir);
        Log("Downloading ffmpeg (this may take a minute)...");

        var zipPath = Path.Combine(ToolsDir, "ffmpeg.zip");

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(10);

        using (var response = await http.GetAsync(FfmpegUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var file = File.Create(zipPath);
            await stream.CopyToAsync(file, ct);
        }

        Log("Extracting ffmpeg...");

        // Extract just ffmpeg.exe and ffprobe.exe from the zip
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in zip.Entries)
            {
                var name = entry.Name.ToLowerInvariant();
                if (name is "ffmpeg.exe" or "ffprobe.exe")
                {
                    var destPath = Path.Combine(ToolsDir, entry.Name);
                    entry.ExtractToFile(destPath, overwrite: true);
                    Log($"Extracted {entry.Name}");
                }
            }
        }

        // Clean up zip
        File.Delete(zipPath);
        Log("ffmpeg ready!");
    }

    private void Log(string msg) => LogMessage?.Invoke(msg);
}
