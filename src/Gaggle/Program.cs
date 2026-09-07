using System.Windows.Forms;
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
            // English on purpose: this fires before any config is read, and the second
            // instance has no business loading one just to word a refusal.
            MessageBox.Show(
                UiText.English.AlreadyRunning,
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
