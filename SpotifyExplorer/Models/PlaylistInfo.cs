namespace SpotifyExplorer.Models;

public class PlaylistInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public int TrackCount { get; set; }

    public override string ToString() => $"{Name} ({TrackCount} tracks)";
}
