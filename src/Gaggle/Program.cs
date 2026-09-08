using System.Windows.Forms;
using Gaggle.Configuration;
using Gaggle.Localisation;
using Gaggle.Ui;

namespace Gaggle;

internal static class Program
{
    private static Mutex? _singleInstance;

    [STAThread]
    private static void Main()
    {
        // Two instances would install two keyboard hooks and race to type the same
        // message into chat.
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\Gaggle.SingleInstance", out bool isFirstInstance);

        if (!isFirstInstance)
        {
            // TrayApplicationContext is what normally picks the interface language, and
            // this path never reaches it. The config is certainly on disk: the instance
            // this one is losing to wrote it.
            Strings.Use(AppConfig.Load().UiLanguage);

            MessageBox.Show(
                Strings.Current.AlreadyRunning,
                "Gaggle",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());

        GC.KeepAlive(_singleInstance);
    }
}
