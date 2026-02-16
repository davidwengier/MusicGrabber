using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using MusicGrabber.Components;
using MusicGrabber.Models;
using MusicGrabber.Services;

namespace MusicGrabber.Forms;

public class MainForm : Form
{
    public MainForm()
    {
        Text = "MusicGrabber";
        Width = 1100;
        Height = 750;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(22, 22, 30);

        var settings = AppSettings.Load();
        var spotify = new SpotifyService();
        var downloader = new DownloadService();
        var transfer = new FileTransferService();
        var toolDownload = new ToolDownloadService();

        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();
        services.AddSingleton(settings);
        services.AddSingleton(spotify);
        services.AddSingleton(downloader);
        services.AddSingleton(transfer);
        services.AddSingleton(toolDownload);
        services.AddSingleton(new LoginService(spotify, this));

        var blazorWebView = new BlazorWebView
        {
            Dock = DockStyle.Fill,
            HostPage = "wwwroot\\index.html",
            Services = services.BuildServiceProvider()
        };

        blazorWebView.RootComponents.Add<MainView>("#app");
        Controls.Add(blazorWebView);
    }
}
