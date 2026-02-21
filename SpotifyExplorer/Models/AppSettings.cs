namespace SpotifyExplorer.Models;

public class AppSettings
{
    public string SpotifyClientId { get; set; } = string.Empty;
    public string DownloadPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "SpotifyExplorer");
    public string YtDlpPath { get; set; } = "yt-dlp";
    public string FfmpegPath { get; set; } = "ffmpeg";
    public int MaxParallelDownloads { get; set; } = 2;
    public FolderNaming FolderNaming { get; set; } = FolderNaming.ArtistAlbum;
    public string? DeviceDriveLetter { get; set; }
    public string? DeviceSubfolder { get; set; }

    private static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpotifyExplorer", "settings.json");

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsFilePath)!;
        Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(SettingsFilePath, json);
    }

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
            return new AppSettings();

        var json = File.ReadAllText(SettingsFilePath);
        return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }
}
