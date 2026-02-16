using MusicGrabber.Models;
using MusicGrabber.Services;

namespace MusicGrabber.Forms;

public class MainForm : Form
{
    private readonly SpotifyService _spotify = new();
    private readonly DownloadService _downloader = new();
    private readonly FileTransferService _transfer = new();
    private AppSettings _settings;

    // UI controls
    private readonly MenuStrip _menuStrip;
    private readonly SplitContainer _splitContainer;
    private readonly ListBox _collectionList;
    private readonly CheckedListBox _trackList;
    private readonly RichTextBox _logBox;
    private readonly ProgressBar _progressBar;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly Button _downloadBtn;
    private readonly Button _transferBtn;
    private readonly Button _selectAllBtn;
    private readonly RadioButton _playlistsRadio;
    private readonly RadioButton _albumsRadio;

    private List<TrackInfo> _currentTracks = [];
    private CancellationTokenSource? _cts;

    public MainForm()
    {
        _settings = AppSettings.Load();

        Text = "MusicGrabber";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;

        // Menu
        _menuStrip = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("&File");
        fileMenu.DropDownItems.Add("&Settings...", null, (_, _) => OpenSettings());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("E&xit", null, (_, _) => Close());

        var spotifyMenu = new ToolStripMenuItem("&Spotify");
        spotifyMenu.DropDownItems.Add("&Login", null, async (_, _) => await LoginAsync());
        spotifyMenu.DropDownItems.Add("L&ogout", null, (_, _) => Logout());

        _menuStrip.Items.Add(fileMenu);
        _menuStrip.Items.Add(spotifyMenu);

        // Status bar
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Not logged in");
        _statusStrip.Items.Add(_statusLabel);

        // Main layout
        var mainPanel = new Panel { Dock = DockStyle.Fill };

        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 250
        };

        // Left panel — collection browser
        var leftPanel = new Panel { Dock = DockStyle.Fill };

        var radioPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            FlowDirection = FlowDirection.LeftToRight
        };
        _playlistsRadio = new RadioButton { Text = "Playlists", Checked = true, AutoSize = true };
        _albumsRadio = new RadioButton { Text = "Albums", AutoSize = true };
        _playlistsRadio.CheckedChanged += async (_, _) => { if (_playlistsRadio.Checked) await LoadCollectionsAsync(); };
        _albumsRadio.CheckedChanged += async (_, _) => { if (_albumsRadio.Checked) await LoadCollectionsAsync(); };
        radioPanel.Controls.AddRange([_playlistsRadio, _albumsRadio]);

        _collectionList = new ListBox { Dock = DockStyle.Fill };
        _collectionList.SelectedIndexChanged += async (_, _) => await LoadTracksAsync();

        leftPanel.Controls.Add(_collectionList);
        leftPanel.Controls.Add(radioPanel);

        // Right panel — tracks + actions
        var rightPanel = new Panel { Dock = DockStyle.Fill };

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 35,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Padding = new Padding(0, 3, 0, 0)
        };

        _selectAllBtn = new Button { Text = "Select All", Width = 75 };
        _selectAllBtn.Click += (_, _) => ToggleSelectAll();

        _downloadBtn = new Button { Text = "Download", Width = 75, Enabled = false };
        _downloadBtn.Click += async (_, _) => await DownloadSelectedAsync();

        _transferBtn = new Button { Text = "To Device", Width = 75, Enabled = false };
        _transferBtn.Click += async (_, _) => await TransferToDeviceAsync();

        var cancelBtn = new Button { Text = "Cancel", Width = 75 };
        cancelBtn.Click += (_, _) => _cts?.Cancel();

        actionPanel.Controls.AddRange([_selectAllBtn, _downloadBtn, _transferBtn, cancelBtn]);

        _trackList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        _trackList.ItemCheck += (_, _) => BeginInvoke(UpdateButtonState);

        _progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 20, Style = ProgressBarStyle.Continuous };

        _logBox = new RichTextBox
        {
            Dock = DockStyle.Bottom,
            Height = 150,
            ReadOnly = true,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(200, 200, 200)
        };

        rightPanel.Controls.Add(_trackList);
        rightPanel.Controls.Add(actionPanel);
        rightPanel.Controls.Add(_progressBar);
        rightPanel.Controls.Add(_logBox);

        _splitContainer.Panel1.Controls.Add(leftPanel);
        _splitContainer.Panel2.Controls.Add(rightPanel);

        mainPanel.Controls.Add(_splitContainer);

        Controls.Add(mainPanel);
        Controls.Add(_statusStrip);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;

        // Wire up service events
        _downloader.LogMessage += msg => BeginInvoke(() => AppendLog(msg));
        _downloader.TrackCompleted += (_, _, _) => BeginInvoke(UpdateButtonState);
        _transfer.LogMessage += msg => BeginInvoke(() => AppendLog(msg));
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.SpotifyClientId))
        {
            MessageBox.Show("Set your Spotify Client ID in Settings first.", "Missing Client ID",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            OpenSettings();
            return;
        }

        try
        {
            var (uri, _) = _spotify.CreateLoginRequest(_settings.SpotifyClientId);
            using var loginForm = new LoginForm(uri);

            if (loginForm.ShowDialog(this) == DialogResult.OK && loginForm.AuthorizationCode != null)
            {
                _statusLabel.Text = "Authenticating...";
                await _spotify.CompleteLoginAsync(_settings.SpotifyClientId, loginForm.AuthorizationCode);
                _statusLabel.Text = "Logged in ✓";
                await LoadCollectionsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Login failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Login failed";
        }
    }

    private void Logout()
    {
        _spotify.Logout();
        _collectionList.Items.Clear();
        _trackList.Items.Clear();
        _currentTracks.Clear();
        _statusLabel.Text = "Not logged in";
        UpdateButtonState();
    }

    private async Task LoadCollectionsAsync()
    {
        if (!_spotify.IsAuthenticated) return;

        _collectionList.Items.Clear();
        _statusLabel.Text = "Loading...";

        try
        {
            if (_playlistsRadio.Checked)
            {
                var playlists = await _spotify.GetUserPlaylistsAsync();
                foreach (var p in playlists)
                    _collectionList.Items.Add(p);
            }
            else
            {
                var albums = await _spotify.GetSavedAlbumsAsync();
                foreach (var a in albums)
                    _collectionList.Items.Add(a);
            }

            _statusLabel.Text = $"Loaded {_collectionList.Items.Count} items";
        }
        catch (Exception ex)
        {
            AppendLog($"Error loading: {ex.Message}");
            _statusLabel.Text = "Error loading collections";
        }
    }

    private async Task LoadTracksAsync()
    {
        if (_collectionList.SelectedItem == null) return;

        _trackList.Items.Clear();
        _currentTracks.Clear();
        _statusLabel.Text = "Loading tracks...";

        try
        {
            if (_collectionList.SelectedItem is PlaylistInfo playlist)
                _currentTracks = await _spotify.GetPlaylistTracksAsync(playlist.Id);
            else if (_collectionList.SelectedItem is AlbumInfo album)
                _currentTracks = await _spotify.GetAlbumTracksAsync(album.Id);

            foreach (var track in _currentTracks)
                _trackList.Items.Add(track, isChecked: true);

            _statusLabel.Text = $"{_currentTracks.Count} tracks";
            UpdateButtonState();
        }
        catch (Exception ex)
        {
            AppendLog($"Error loading tracks: {ex.Message}");
            _statusLabel.Text = "Error loading tracks";
        }
    }

    private async Task DownloadSelectedAsync()
    {
        var selected = GetSelectedTracks();
        if (selected.Count == 0) return;

        if (!DownloadService.IsYtDlpAvailable(_settings.YtDlpPath))
        {
            MessageBox.Show(
                "yt-dlp not found. Install it and set the path in Settings.\nhttps://github.com/yt-dlp/yt-dlp",
                "yt-dlp not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        _cts = new CancellationTokenSource();
        _progressBar.Value = 0;
        _progressBar.Maximum = selected.Count;

        var progress = new Progress<(int current, int total)>(p =>
        {
            _progressBar.Value = p.current;
            _statusLabel.Text = $"Downloading {p.current}/{p.total}...";
        });

        try
        {
            await _downloader.DownloadTracksAsync(
                selected, _settings.DownloadPath, _settings.YtDlpPath, _settings.FfmpegPath,
                progress, _cts.Token);

            _statusLabel.Text = "Download complete";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Download cancelled.");
            _statusLabel.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            AppendLog($"Download error: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            _cts = null;
        }
    }

    private async Task TransferToDeviceAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.DeviceDriveLetter))
        {
            MessageBox.Show("Set your MP3 player drive in Settings.", "No device configured",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var targetPath = Path.Combine(_settings.DeviceDriveLetter + "\\", _settings.DeviceSubfolder ?? "Music");
        var selected = GetSelectedTracks().Where(t => t.LocalFilePath != null).ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show("Download tracks first before transferring.", "No files to transfer",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetBusy(true);
        _cts = new CancellationTokenSource();
        _progressBar.Value = 0;
        _progressBar.Maximum = selected.Count;

        var progress = new Progress<(int current, int total)>(p =>
        {
            _progressBar.Value = p.current;
            _statusLabel.Text = $"Copying {p.current}/{p.total}...";
        });

        try
        {
            await _transfer.TransferTracksAsync(selected, targetPath, progress, _cts.Token);
            _statusLabel.Text = "Transfer complete";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Transfer cancelled.");
            _statusLabel.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            AppendLog($"Transfer error: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            _cts = null;
        }
    }

    private List<TrackInfo> GetSelectedTracks()
    {
        var selected = new List<TrackInfo>();
        for (int i = 0; i < _trackList.Items.Count; i++)
        {
            if (_trackList.GetItemChecked(i) && _trackList.Items[i] is TrackInfo track)
                selected.Add(track);
        }
        return selected;
    }

    private void ToggleSelectAll()
    {
        bool allChecked = _trackList.CheckedItems.Count == _trackList.Items.Count;
        for (int i = 0; i < _trackList.Items.Count; i++)
            _trackList.SetItemChecked(i, !allChecked);
    }

    private void UpdateButtonState()
    {
        bool hasChecked = _trackList.CheckedItems.Count > 0;
        bool hasDownloaded = _currentTracks.Any(t => t.LocalFilePath != null);
        _downloadBtn.Enabled = hasChecked;
        _transferBtn.Enabled = hasChecked && hasDownloaded;
    }

    private void SetBusy(bool busy)
    {
        _downloadBtn.Enabled = !busy;
        _transferBtn.Enabled = !busy;
        _collectionList.Enabled = !busy;
        _trackList.Enabled = !busy;
    }

    private void AppendLog(string message)
    {
        _logBox.AppendText(message + Environment.NewLine);
        _logBox.ScrollToCaret();
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog(this) == DialogResult.OK)
            _settings = AppSettings.Load();
    }
}
