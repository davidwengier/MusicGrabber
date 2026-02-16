using MusicGrabber.Models;
using MusicGrabber.Services;

namespace MusicGrabber.Forms;

public class SettingsForm : Form
{
    private readonly TextBox _clientIdBox;
    private readonly TextBox _downloadPathBox;
    private readonly TextBox _ytDlpPathBox;
    private readonly TextBox _ffmpegPathBox;
    private readonly ComboBox _driveCombo;
    private readonly TextBox _subfolderBox;
    private readonly AppSettings _settings;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;

        Text = "Settings";
        Width = 500;
        Height = 400;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        layout.Controls.Add(new Label { Text = "Spotify Client ID:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _clientIdBox = new TextBox { Text = settings.SpotifyClientId, Dock = DockStyle.Fill };
        layout.Controls.Add(_clientIdBox, 1, row++);

        layout.Controls.Add(new Label { Text = "Download folder:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _downloadPathBox = new TextBox { Text = settings.DownloadPath, Dock = DockStyle.Fill };
        layout.Controls.Add(_downloadPathBox, 1, row++);

        layout.Controls.Add(new Label { Text = "yt-dlp path:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _ytDlpPathBox = new TextBox { Text = settings.YtDlpPath, Dock = DockStyle.Fill };
        layout.Controls.Add(_ytDlpPathBox, 1, row++);

        layout.Controls.Add(new Label { Text = "ffmpeg path:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _ffmpegPathBox = new TextBox { Text = settings.FfmpegPath, Dock = DockStyle.Fill };
        layout.Controls.Add(_ffmpegPathBox, 1, row++);

        layout.Controls.Add(new Label { Text = "MP3 Player drive:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _driveCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        PopulateDrives();
        layout.Controls.Add(_driveCombo, 1, row++);

        layout.Controls.Add(new Label { Text = "Device subfolder:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _subfolderBox = new TextBox { Text = settings.DeviceSubfolder ?? "Music", Dock = DockStyle.Fill };
        layout.Controls.Add(_subfolderBox, 1, row++);

        // Spacer
        layout.Controls.Add(new Panel(), 0, row++);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var cancelBtn = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var okBtn = new Button { Text = "OK" };
        okBtn.Click += OnSave;

        buttonPanel.Controls.Add(okBtn);
        buttonPanel.Controls.Add(cancelBtn);
        layout.Controls.Add(buttonPanel, 1, row);

        Controls.Add(layout);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }

    private void PopulateDrives()
    {
        _driveCombo.Items.Clear();
        var drives = FileTransferService.GetRemovableDrives();
        foreach (var drive in drives)
        {
            var label = $"{drive.Name} ({drive.VolumeLabel}, {drive.DriveType})";
            _driveCombo.Items.Add(label);
            if (drive.Name.TrimEnd('\\') == _settings.DeviceDriveLetter)
                _driveCombo.SelectedIndex = _driveCombo.Items.Count - 1;
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _settings.SpotifyClientId = _clientIdBox.Text.Trim();
        _settings.DownloadPath = _downloadPathBox.Text.Trim();
        _settings.YtDlpPath = _ytDlpPathBox.Text.Trim();
        _settings.FfmpegPath = _ffmpegPathBox.Text.Trim();
        _settings.DeviceSubfolder = _subfolderBox.Text.Trim();

        if (_driveCombo.SelectedItem is string driveLabel)
        {
            // Extract drive letter from "D:\ (MUSIC, Removable)"
            _settings.DeviceDriveLetter = driveLabel.Split(' ')[0].TrimEnd('\\');
        }

        _settings.Save();
        DialogResult = DialogResult.OK;
        Close();
    }
}
