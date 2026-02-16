using Microsoft.Web.WebView2.WinForms;

namespace MusicGrabber.Forms;

public class LoginForm : Form
{
    private readonly WebView2 _webView;
    private readonly string _redirectUri = "http://127.0.0.1:5543/callback";

    public string? AuthorizationCode { get; private set; }

    public LoginForm(Uri loginUri)
    {
        Text = "Login to Spotify";
        Width = 500;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        Controls.Add(_webView);

        Load += async (_, _) =>
        {
            await _webView.EnsureCoreWebView2Async();
            _webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            _webView.CoreWebView2.Navigate(loginUri.ToString());
        };
    }

    private void OnNavigationStarting(object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
    {
        if (!e.Uri.StartsWith(_redirectUri, StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;

        var uri = new Uri(e.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        AuthorizationCode = query["code"];

        if (AuthorizationCode != null)
            DialogResult = DialogResult.OK;
        else
            DialogResult = DialogResult.Cancel;

        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _webView.Dispose();
        base.Dispose(disposing);
    }
}
