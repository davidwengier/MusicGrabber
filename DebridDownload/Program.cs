using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

var parsedArgs = CliArguments.Parse(args);
if (parsedArgs.ShowHelp)
{
    CliArguments.PrintUsage();
    return;
}

try
{
    var resolvedApiKey = parsedArgs.ApiKey;
    if (string.IsNullOrWhiteSpace(resolvedApiKey))
    {
        resolvedApiKey = ApiKeyStore.Load();
    }

    if (string.IsNullOrWhiteSpace(resolvedApiKey))
    {
        resolvedApiKey = UserPrompts.PromptRequired("Enter your Real-Debrid API key: ");
    }

    if (string.IsNullOrWhiteSpace(resolvedApiKey))
    {
        throw new InvalidOperationException("A Real-Debrid API key is required.");
    }

    if (!string.Equals(ApiKeyStore.Load(), resolvedApiKey, StringComparison.Ordinal))
    {
        ApiKeyStore.Save(resolvedApiKey);
    }

    Directory.CreateDirectory(parsedArgs.OutputDirectory);

    using var realDebrid = new RealDebridClient(resolvedApiKey);
    var pendingMagnet = parsedArgs.Magnet;

    while (true)
    {
        var resolvedMagnet = string.IsNullOrWhiteSpace(pendingMagnet)
            ? UserPrompts.PromptRequired("Enter magnet link: ")
            : pendingMagnet;
        pendingMagnet = null;

        Console.WriteLine("Adding magnet to Real-Debrid...");
        var torrentId = await realDebrid.AddMagnetAsync(resolvedMagnet);
        Console.WriteLine($"Magnet added. Torrent id: {torrentId}");

        Console.WriteLine("Selecting all files in torrent...");
        await realDebrid.SelectAllFilesAsync(torrentId);

        Console.WriteLine("Waiting for Real-Debrid to finish torrent download...");
        var info = await realDebrid.WaitForDownloadAsync(torrentId, parsedArgs.Timeout, parsedArgs.PollInterval);
        Console.WriteLine("Torrent is ready. Downloading files...");

        var torrentFolderName = BuildSafeFolderName(info.FileName);
        var downloadRootDirectory = Path.Combine(parsedArgs.OutputDirectory, torrentFolderName);
        Directory.CreateDirectory(downloadRootDirectory);

        var selectedFiles = info.Files.Where(file => file.Selected == 1).ToList();
        var failedDownloads = 0;
        for (var i = 0; i < info.Links.Count; i++)
        {
            var sourceUri = info.Links[i];
            try
            {
                var unrestricted = await realDebrid.UnrestrictLinkAsync(sourceUri);
                var relativePath = selectedFiles.Count > i
                    ? selectedFiles[i].Path.TrimStart('/', '\\')
                    : unrestricted.FileName;
                var safeRelativePath = string.IsNullOrWhiteSpace(relativePath)
                    ? $"file-{i + 1}"
                    : relativePath;

                // The torrent metadata may list a file as .mp3 but Real-Debrid
                // actually serves a .rar (or other archive). Use the extension
                // from the unrestrict response when it differs.
                if (!string.IsNullOrWhiteSpace(unrestricted.FileName))
                {
                    var unrestrictedExt = Path.GetExtension(unrestricted.FileName);
                    var metadataExt = Path.GetExtension(safeRelativePath);
                    if (!string.IsNullOrEmpty(unrestrictedExt)
                        && !string.Equals(unrestrictedExt, metadataExt, StringComparison.OrdinalIgnoreCase))
                    {
                        safeRelativePath = Path.ChangeExtension(safeRelativePath, unrestrictedExt);
                    }
                }

                var destinationPath = Path.Combine(downloadRootDirectory, safeRelativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                if (!File.Exists(destinationPath))
                {
                    Console.WriteLine($"Downloading: {safeRelativePath}");
            
                    await realDebrid.DownloadFileAsync(unrestricted.DownloadUrl, destinationPath);
                }
            }
            catch (Exception ex)
            {
                failedDownloads++;
                Console.Error.WriteLine($"Error processing file {i + 1}/{info.Links.Count}. Source URI: {sourceUri}");
                Console.Error.WriteLine(ex);
            }
        }

        if (failedDownloads > 0)
        {
            Console.Error.WriteLine($"Completed with errors. Failed files: {failedDownloads}");
        }
        else
        {
            Console.WriteLine("Done.");
        }

        Console.Write("Enter another magnet link (leave blank to exit): ");
        pendingMagnet = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(pendingMagnet))
        {
            break;
        }
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine("Fatal error:");
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}

static string BuildSafeFolderName(string? name)
{
    var candidate = string.IsNullOrWhiteSpace(name) ? "torrent" : name.Trim();
    foreach (var invalid in Path.GetInvalidFileNameChars())
    {
        candidate = candidate.Replace(invalid, '_');
    }

    return string.IsNullOrWhiteSpace(candidate) ? "torrent" : candidate;
}

sealed class CliArguments
{
    public string? ApiKey { get; private set; }
    public string? Magnet { get; private set; }
    public string OutputDirectory { get; private set; } = Path.Combine(Environment.CurrentDirectory, "..", "Music");
    public TimeSpan PollInterval { get; private set; } = TimeSpan.FromSeconds(5);
    public TimeSpan Timeout { get; private set; } = TimeSpan.FromMinutes(30);
    public bool ShowHelp { get; private set; }

    public static CliArguments Parse(string[] args)
    {
        var parsed = new CliArguments();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--api-key":
                case "-k":
                    parsed.ApiKey = GetValue(args, ref i);
                    break;
                case "--magnet":
                case "-m":
                    parsed.Magnet = GetValue(args, ref i);
                    break;
                case "--output":
                case "-o":
                    parsed.OutputDirectory = Path.GetFullPath(GetValue(args, ref i));
                    break;
                case "--poll-seconds":
                    parsed.PollInterval = TimeSpan.FromSeconds(ParsePositiveInt(GetValue(args, ref i), "--poll-seconds"));
                    break;
                case "--timeout-seconds":
                    parsed.Timeout = TimeSpan.FromSeconds(ParsePositiveInt(GetValue(args, ref i), "--timeout-seconds"));
                    break;
                case "--help":
                case "-h":
                    parsed.ShowHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        return parsed;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  DebridDownload [--api-key <REAL_DEBRID_API_KEY>] [--magnet <MAGNET_LINK>] [--output <FOLDER>] [--poll-seconds <SECONDS>] [--timeout-seconds <SECONDS>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --api-key, -k         Real-Debrid API key (optional; loaded from AppData or prompted when missing)");
        Console.WriteLine("  --magnet, -m          Magnet link (optional; prompted when missing)");
        Console.WriteLine("  --output, -o          Local output folder (default: .\\downloads)");
        Console.WriteLine("  --poll-seconds        Poll interval in seconds (default: 5)");
        Console.WriteLine("  --timeout-seconds     Max wait time for RD torrent completion (default: 1800)");
        Console.WriteLine("  --help, -h            Show help");
    }

    private static string GetValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for argument: {args[index]}");
        }

        index++;
        return args[index];
    }

    private static int ParsePositiveInt(string value, string argumentName)
    {
        if (!int.TryParse(value, out var parsed) || parsed <= 0)
        {
            throw new ArgumentException($"Argument {argumentName} must be a positive integer.");
        }

        return parsed;
    }
}

static class UserPrompts
{
    public static string PromptRequired(string prompt)
    {
        Console.Write(prompt);
        var value = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A required value was not provided.");
        }

        return value;
    }
}

static class ApiKeyStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DebridDownload",
        "realdebrid_api_key.txt");

    public static string? Load()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        var value = File.ReadAllText(FilePath).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static void Save(string apiKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, apiKey);
    }
}

sealed class RealDebridClient : IDisposable
{
    private const string BaseUrl = "https://api.real-debrid.com/rest/1.0";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _httpClient;

    public RealDebridClient(string apiKey)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> AddMagnetAsync(string magnet)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("magnet", magnet)
        });

        var response = await SendAsync(HttpMethod.Post, "torrents/addMagnet", content);
        var payload = await DeserializeAsync<AddMagnetResponse>(response);
        return payload.Id;
    }

    public async Task SelectAllFilesAsync(string torrentId)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("files", "all")
        });

        await SendAsync(HttpMethod.Post, $"torrents/selectFiles/{torrentId}", content);
    }

    public async Task<TorrentInfoResponse> WaitForDownloadAsync(string torrentId, TimeSpan timeout, TimeSpan pollInterval)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var info = await GetTorrentInfoAsync(torrentId);
            if (string.Equals(info.Status, "downloaded", StringComparison.OrdinalIgnoreCase))
            {
                return info;
            }

            if (IsFailureStatus(info.Status))
            {
                throw new InvalidOperationException($"Real-Debrid torrent failed with status '{info.Status}'.");
            }

            await Task.Delay(pollInterval);
        }

        throw new TimeoutException($"Timed out waiting for torrent {torrentId} to download.");
    }

    public async Task<UnrestrictResponse> UnrestrictLinkAsync(string link)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("link", link)
        });

        var response = await SendAsync(HttpMethod.Post, "unrestrict/link", content);
        var payload = await DeserializeAsync<UnrestrictResponse>(response);
        if (string.IsNullOrWhiteSpace(payload.DownloadUrl))
        {
            throw new InvalidOperationException("Real-Debrid did not return a download URL.");
        }

        return payload;
    }

    public async Task DownloadFileAsync(string sourceUrl, string destinationPath)
    {
        using var response = await _httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Download failed ({(int)response.StatusCode}): {body}");
        }

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<TorrentInfoResponse> GetTorrentInfoAsync(string torrentId)
    {
        var response = await SendAsync(HttpMethod.Get, $"torrents/info/{torrentId}");
        return await DeserializeAsync<TorrentInfoResponse>(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, $"{BaseUrl}/{path}");
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var body = await response.Content.ReadAsStringAsync();
        response.Dispose();
        throw new HttpRequestException($"Real-Debrid API call failed for '{path}' ({(int)response.StatusCode}): {body}");
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response) where T : class
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var payload = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        if (payload is null)
        {
            throw new InvalidOperationException("Received an empty or invalid JSON response from Real-Debrid.");
        }

        return payload;
    }

    private static bool IsFailureStatus(string? status) =>
        string.Equals(status, "error", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "magnet_error", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "virus", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "dead", StringComparison.OrdinalIgnoreCase);
}

sealed class AddMagnetResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

sealed class TorrentInfoResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("filename")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("links")]
    public List<string> Links { get; init; } = new();

    [JsonPropertyName("files")]
    public List<TorrentFileResponse> Files { get; init; } = new();
}

sealed class TorrentFileResponse
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("selected")]
    public int Selected { get; init; }
}

sealed class UnrestrictResponse
{
    [JsonPropertyName("download")]
    public string DownloadUrl { get; init; } = string.Empty;

    [JsonPropertyName("filename")]
    public string FileName { get; init; } = string.Empty;
}
