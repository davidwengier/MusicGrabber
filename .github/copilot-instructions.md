# MusicGrabber - Copilot Instructions

## Build & Run

```powershell
dotnet build                    # Build the solution
dotnet run --project src/MusicGrabber   # Run the app
```

There are no tests yet. No linter is configured.

## Architecture

MusicGrabber is a WinForms (.NET 10) desktop app that downloads music from Spotify playlists/albums as MP3s and copies them to a USB MP3 player.

**Data flow:** Spotify API (metadata) → yt-dlp (YouTube search + download) → local MP3 files → USB device copy.

### Services (`src/MusicGrabber/Services/`)

- **SpotifyService** — Spotify OAuth (PKCE flow) and API calls via `SpotifyAPI.Web`. Handles login, fetching playlists, albums, and tracks. All API results are paginated automatically.
- **DownloadService** — Wraps `yt-dlp` as a subprocess. Searches YouTube with `ytsearch1:"{artist} - {title}"`, downloads as MP3. Skips already-downloaded files. Reports progress via events.
- **FileTransferService** — Copies downloaded MP3s to a target USB drive path. Skips files already on device (same file size). Detects non-system drives.

### Forms (`src/MusicGrabber/Forms/`)

- **MainForm** — Primary UI. Split panel with collection browser (left) and checked track list (right). Wires up all services.
- **LoginForm** — WebView2 embedded browser for Spotify OAuth. Captures auth code from redirect URI (`http://127.0.0.1:5543/callback`).
- **SettingsForm** — Configures Spotify Client ID, download path, yt-dlp/ffmpeg paths, and target USB drive.

### Models (`src/MusicGrabber/Models/`)

- **TrackInfo** — Track metadata with `SearchQuery` (for yt-dlp) and `SafeFileName` (filesystem-safe) computed properties.
- **PlaylistInfo / AlbumInfo** — Lightweight display models for the collection browser.
- **AppSettings** — Serialized to `%AppData%/MusicGrabber/settings.json`. Handles load/save.

## Conventions

- All forms are code-only (no `.Designer.cs` files) — UI is built programmatically in constructors.
- Services communicate progress via `event Action<string> LogMessage` and `IProgress<(int current, int total)>`.
- All long-running service methods accept `CancellationToken` and are async.
- External tools (yt-dlp, ffmpeg) are invoked as subprocesses, never as libraries.
- Spotify auth uses PKCE (no client secret at runtime). The Client ID is stored in user settings, not in source.

## External Dependencies

- **yt-dlp** and **ffmpeg** must be installed separately and either on PATH or configured in Settings.
- A Spotify Developer app must be registered at https://developer.spotify.com/dashboard with redirect URI `http://127.0.0.1:5543/callback`.
