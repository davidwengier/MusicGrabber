namespace MusicGrabber.Models;

public class AlbumInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int TrackCount { get; set; }
    public string? ImageUrl { get; set; }

    public override string ToString() => $"{Artist} - {Name} ({TrackCount} tracks)";
}
