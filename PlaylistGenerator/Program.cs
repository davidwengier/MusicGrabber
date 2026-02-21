using System.Text.RegularExpressions;

var root = args.Length > 0 && Directory.Exists(args[0])
    ? args[0]
    : Path.Combine(Environment.CurrentDirectory, "..", "Music");

root = Path.GetFullPath(root);

if (!Directory.Exists(root))
{
    Console.WriteLine($"Music directory not found: {root}");
    return;
}

Console.WriteLine($"Scanning: {root}");
Console.WriteLine();

// Index all mp3 files in the Music folder
var allMp3s = Directory.EnumerateFiles(root, "*.mp3", SearchOption.AllDirectories)
    .Select(path => new Mp3File(path, NormalizeForMatch(ExtractTitle(path))))
    .ToList();

Console.WriteLine($"Found {allMp3s.Count} mp3 files.");
Console.WriteLine();

// Find all track listing files
var trackListings = Directory.EnumerateFiles(root, "*Track Listing.md", SearchOption.AllDirectories).ToList();

if (trackListings.Count == 0)
{
    Console.WriteLine("No track listing files found.");
    return;
}

Console.WriteLine($"Found {trackListings.Count} track listing(s):");
foreach (var tl in trackListings)
{
    Console.WriteLine($"  - {Path.GetFileName(tl)}");
}
Console.WriteLine();

foreach (var trackListingPath in trackListings)
{
    ProcessTrackListing(trackListingPath, allMp3s, root);
}

void ProcessTrackListing(string trackListingPath, List<Mp3File> mp3s, string musicRoot)
{
    var fileName = Path.GetFileNameWithoutExtension(trackListingPath);
    var playlistName = fileName.Replace(" - Track Listing", "");
    Console.WriteLine($"Processing: {playlistName}");
    Console.WriteLine(new string('-', 60));

    var tracks = ParseTrackListing(trackListingPath);

    if (tracks.Count == 0)
    {
        Console.WriteLine("  No tracks found in listing.");
        Console.WriteLine();
        return;
    }

    var lines = new List<string>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var found = 0;
    var notFound = 0;

    foreach (var track in tracks)
    {
        var normalizedTitle = NormalizeForMatch(track.Title);
        var match = FindBestMatch(normalizedTitle, track.Title, mp3s);

        if (match is not null)
        {
            var relativePath = Path.GetRelativePath(musicRoot, match.FullPath);
            if (seen.Add(relativePath))
            {
                lines.Add(relativePath);
            }
            found++;
        }
        else
        {
            Console.WriteLine($"  NOT FOUND: #{track.Number} - {track.Title}");
            lines.Add($"# NOT FOUND: {track.Title} - {track.Artist}");
            notFound++;
        }
    }

    Console.WriteLine($"  Matched: {found}/{tracks.Count} tracks ({notFound} not found)");

    if (lines.Count > 0)
    {
        var outputPath = Path.Combine(musicRoot, $"{playlistName}.m3u");
        File.WriteAllLines(outputPath, lines);
        Console.WriteLine($"  Created: {Path.GetFileName(outputPath)} ({lines.Count(l => !l.StartsWith('#'))} unique entries, {lines.Count(l => l.StartsWith('#'))} not found)");
    }

    Console.WriteLine();
}

List<TrackEntry> ParseTrackListing(string path)
{
    var tracks = new List<TrackEntry>();

    foreach (var line in File.ReadLines(path))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.Contains("---"))
            continue;

        var parts = line.Split('|', StringSplitOptions.None);
        if (parts.Length < 5)
            continue;

        var numberStr = parts[1].Trim();
        var title = parts[2].Trim();
        var artist = parts[3].Trim();
        var album = parts[4].Trim();

        if (!int.TryParse(numberStr, out var number))
            continue;

        if (string.IsNullOrEmpty(title) || title.Equals("Title", StringComparison.OrdinalIgnoreCase))
            continue;

        tracks.Add(new TrackEntry(number, title, artist, album));
    }

    return tracks;
}

Mp3File? FindBestMatch(string normalizedTitle, string originalTitle, List<Mp3File> mp3s)
{
    // First try: exact normalized match
    var exact = mp3s.FirstOrDefault(m => m.NormalizedTitle == normalizedTitle);
    if (exact is not null)
        return exact;

    // Second try: check if normalized title contains or is contained by the mp3 title
    var containsMatch = mp3s
        .Where(m => m.NormalizedTitle.Contains(normalizedTitle) || normalizedTitle.Contains(m.NormalizedTitle))
        .Where(m => m.NormalizedTitle.Length > 0)
        .OrderBy(m => Math.Abs(m.NormalizedTitle.Length - normalizedTitle.Length))
        .FirstOrDefault();

    return containsMatch;
}

string ExtractTitle(string mp3Path)
{
    var fileName = Path.GetFileNameWithoutExtension(mp3Path);

    // Strip leading track numbers in various formats:
    // "01 Title", "01. Title", "1 Title", "107-artist-title"
    // For patterns like "107-artist-title", just take the last segment after the last dash
    if (Regex.IsMatch(fileName, @"^\d{3}-"))
    {
        // Format: "107-artist_info-title_here" — take last segment
        var lastDash = fileName.LastIndexOf('-');
        if (lastDash >= 0)
        {
            fileName = fileName[(lastDash + 1)..];
            // Replace underscores with spaces
            fileName = fileName.Replace('_', ' ');
        }
    }
    else
    {
        // Strip leading "01 ", "01. ", "1 " etc.
        fileName = Regex.Replace(fileName, @"^\d+[\.\s\-]\s*", "");
    }

    return fileName;
}

string NormalizeForMatch(string title)
{
    // Lowercase
    var result = title.ToLowerInvariant();

    // Remove common suffixes/annotations like "(feat. ...)", "(Remastered)", "- Remix", "- Remastered 2011" etc.
    result = Regex.Replace(result, @"\s*\(feat\..*?\)", "");
    result = Regex.Replace(result, @"\s*\(with\s.*?\)", "");
    result = Regex.Replace(result, @"\s*\(remaster.*?\)", "");
    result = Regex.Replace(result, @"\s*\(deluxe.*?\)", "");
    result = Regex.Replace(result, @"\s*-\s*remaster.*$", "");
    result = Regex.Replace(result, @"\s*-\s*remix.*$", "");
    result = Regex.Replace(result, @"\s*-\s*spider-man.*$", "");
    result = Regex.Replace(result, @"\s*-\s*music from.*$", "");
    result = Regex.Replace(result, @"\s*-\s*from\s.*$", "");
    result = Regex.Replace(result, @"\s*-\s*skit$", "");

    // Remove special characters but keep spaces and alphanumeric
    result = Regex.Replace(result, @"[^\w\s]", "");

    // Collapse whitespace
    result = Regex.Replace(result, @"\s+", " ").Trim();

    return result;
}

record Mp3File(string FullPath, string NormalizedTitle);
record TrackEntry(int Number, string Title, string Artist, string Album);
