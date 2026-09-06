using System.Drawing.Drawing2D;
using Gaggle.Interop;

namespace Gaggle.Ui;

/// <summary>
/// Draws the tray icon at runtime — a glider silhouette circling a thermal — so the
/// repository carries no binary assets and the icon can recolour to show state.
/// </summary>
internal static class TrayIcons
{
    public static readonly Color Idle = Color.FromArgb(150, 160, 175);
    public static readonly Color Ready = Color.FromArgb(120, 200, 255);
    public static readonly Color Recording = Color.FromArgb(255, 95, 95);
    public static readonly Color Working = Color.FromArgb(255, 205, 100);

    public static Icon Create(Color colour)
    {
        using var bitmap = new Bitmap(32, 32);

        using (Graphics graphics = Graphics.FromImage(bitmap))
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
