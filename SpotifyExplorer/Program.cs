using SpotifyExplorer.Forms;

namespace SpotifyExplorer;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}