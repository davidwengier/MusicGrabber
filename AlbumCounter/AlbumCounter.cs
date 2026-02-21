using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

var root = AppContext.BaseDirectory;
// When running as a file-based app, use the directory containing the .cs file
if (args.Length > 0 && Directory.Exists(args[0]))
{
    root = args[0];
}
else
{
    root = Path.Combine(Environment.CurrentDirectory, "..", "Music");
}

var albumCounts = new Dictionary<string, int>(StringComparer.Ordinal);
var albumSources = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

foreach (var file in Directory.EnumerateFiles(root, "*Track Listing.md", SearchOption.AllDirectories))
{
    var relativePath = Path.GetRelativePath(root, file);

    foreach (var line in File.ReadLines(file))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.Contains("---"))
        {
            continue;
        }

        var parts = line.Split('|', StringSplitOptions.None);
        if (parts.Length < 5)
        {
            continue;
        }

        var album = parts[4].Trim();
        if (string.IsNullOrEmpty(album) || album.Equals("Album", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        albumCounts[album] = albumCounts.GetValueOrDefault(album) + 1;

        if (!albumSources.TryGetValue(album, out var sources))
        {
            sources = new HashSet<string>(StringComparer.Ordinal);
            albumSources[album] = sources;
        }
        sources.Add(relativePath);
    }
}

var filtered = albumCounts.Where(kv => kv.Value > 1).ToDictionary(kv => kv.Key, kv => kv.Value);

if (filtered.Count == 0)
{
    Console.WriteLine("No albums with more than one track found.");
    return;
}

int maxLen = filtered.Keys.Max(k => k.Length);
maxLen = Math.Min(maxLen, 80);

string header = "Album".PadRight(maxLen) + "  Tracks";

var writers = new List<TextWriter> { Console.Out };
var outputPath = Path.Combine(root, "Album Count.md");
var fileWriter = new StreamWriter(outputPath, append: false);
writers.Add(fileWriter);

void WriteAll(string text)
{
    foreach (var w in writers) w.WriteLine(text);
}

WriteAll(header);
WriteAll(new string('-', header.Length));

foreach (var kv in filtered.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
{
    string name = kv.Key.Length > maxLen ? kv.Key[..maxLen] : kv.Key.PadRight(maxLen);
    WriteAll($"{name}  {kv.Value,6}");
    if (albumSources.TryGetValue(kv.Key, out var sources))
    {
        var indent = new string(' ', maxLen);
        foreach (var source in sources.OrderBy(s => s))
        {
            WriteAll($"{indent}    └ {source}");
        }
    }
}

WriteAll(new string('-', header.Length));
WriteAll($"{"TOTAL".PadRight(maxLen)}  {filtered.Values.Sum(),6}");
WriteAll($"\n{filtered.Count} albums with more than one track.");

fileWriter.Flush();
fileWriter.Dispose();

Console.WriteLine($"\nOutput written to {outputPath}");
