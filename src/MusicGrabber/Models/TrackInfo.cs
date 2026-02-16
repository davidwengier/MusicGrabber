namespace MusicGrabber.Models;

public class TrackInfo
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string SpotifyUri { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? LocalFilePath { get; set; }

    public string SearchQuery => $"{Artist} - {Title}";

    public string SafeFileName
    {
        get
        {
            var name = $"{Artist} - {Title}.mp3";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }

    public override string ToString() => $"{Artist} - {Title} ({Album})";
}
