using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using SpotifyExplorer.Components;
using SpotifyExplorer.Models;
using SpotifyExplorer.Services;

namespace SpotifyExplorer.Forms;

public class MainForm : Form
{
    public MainForm()
    {
        Text = "SpotifyExplorer";
        Width = 1600;
        Height = 1000;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(22, 22, 30);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
            Icon = new Icon(iconPath);

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
        services.AddSingleton(new DialogService(this));

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
