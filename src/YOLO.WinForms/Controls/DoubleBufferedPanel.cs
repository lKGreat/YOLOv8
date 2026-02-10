namespace YOLO.WinForms.Controls;

/// <summary>
/// A Panel subclass with hardware-accelerated double buffering enabled.
/// Eliminates flicker during Paint operations by rendering to an
/// off-screen buffer before blitting to screen.
/// </summary>
internal sealed class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);
        UpdateStyles();
    }

    /// <summary>
    /// Suppress WM_ERASEBKGND to prevent background flicker.
    /// The Paint handler is responsible for filling the entire surface.
    /// </summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Intentionally empty – the main Paint handler draws everything,
        // including the background, so erasing first would cause flicker.
    }
}
