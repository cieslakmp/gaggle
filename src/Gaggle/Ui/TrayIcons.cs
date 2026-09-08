using System.Drawing.Drawing2D;
using Gaggle.Interop;

namespace Gaggle.Ui;

/// <summary>
/// The tray icon in each of its states — a glider silhouette circling a thermal — drawn
/// at runtime so the repository carries no binary assets and the icon can recolour to
/// show what the app is doing.
///
/// Built once each and held for the life of the process. NotifyIcon does not take
/// ownership of what it is handed, so drawing a fresh icon per state change leaked a GDI
/// handle every time — and the state changes on every recording, every send and every
/// time Condor comes or goes.
/// </summary>
internal static class TrayIcons
{
    public static readonly Icon Idle = Draw(Color.FromArgb(150, 160, 175));
    public static readonly Icon Ready = Draw(Color.FromArgb(120, 200, 255));
    public static readonly Icon Recording = Draw(Color.FromArgb(255, 95, 95));
    public static readonly Icon Working = Draw(Color.FromArgb(255, 205, 100));

    private static Icon Draw(Color colour)
    {
        using var bitmap = new Bitmap(32, 32);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // The thermal.
            using var pen = new Pen(colour, 2.5f);
            graphics.DrawEllipse(pen, 6, 8, 20, 20);

            // The glider, wings level, at the top of the turn.
            using var brush = new SolidBrush(colour);
            graphics.FillPolygon(brush, [new PointF(16, 2), new PointF(24, 9), new PointF(8, 9)]);
        }

        IntPtr handle = bitmap.GetHicon();

        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
