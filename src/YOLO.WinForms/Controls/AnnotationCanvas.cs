using System.ComponentModel;
using YOLO.WinForms.Models;

namespace YOLO.WinForms.Controls;

/// <summary>
/// The mode of interaction on the canvas.
/// </summary>
public enum CanvasMode
{
    Select,
    DrawRect
}

/// <summary>
/// Which resize handle the user is dragging.
/// </summary>
internal enum ResizeHandle
{
    None,
    TopLeft, Top, TopRight,
    Left, Right,
    BottomLeft, Bottom, BottomRight
}

/// <summary>
/// Custom double-buffered canvas for image annotation with rectangle bounding boxes.
/// Supports zoom/pan, drawing, selection, move/resize, and class-colored overlays.
/// </summary>
public partial class AnnotationCanvas : UserControl
{
    // ── State ──────────────────────────────────────────────────────

    private Image? _image;
    private int _imageW, _imageH;
    private List<RectAnnotation> _annotations = [];
    private int _currentClassId;
    private List<string> _classNames = [];

    // Zoom / pan
    private float _zoom = 1f;
    private PointF _panOffset;     // image-space offset of viewport top-left

    // Drawing
    private CanvasMode _mode = CanvasMode.DrawRect;
    private bool _isDrawing;
    private PointF _drawStart;     // image-space
    private PointF _drawCurrent;   // image-space

    // Selection
    private RectAnnotation? _selectedAnnotation;
    private bool _isDragging;
    private PointF _dragStart;     // screen-space
    private double _dragOrigCX, _dragOrigCY, _dragOrigW, _dragOrigH;
    private ResizeHandle _activeHandle = ResizeHandle.None;

    // Pan
    private bool _isPanning;
    private Point _panMouseStart;
    private PointF _panOffsetStart;

    // Space key held = temporary pan mode
    private bool _spaceHeld;

    // Colors
    private static readonly Color[] ClassColors =
    [
        Color.FromArgb(220, 57, 119, 175),
        Color.FromArgb(220, 214, 39, 40),
        Color.FromArgb(220, 44, 160, 44),
        Color.FromArgb(220, 255, 127, 14),
        Color.FromArgb(220, 148, 103, 189),
        Color.FromArgb(220, 140, 86, 75),
        Color.FromArgb(220, 227, 119, 194),
        Color.FromArgb(220, 127, 127, 127),
        Color.FromArgb(220, 188, 189, 34),
        Color.FromArgb(220, 23, 190, 207),
    ];

    private const int HandleSize = 8;

    // ── Events ─────────────────────────────────────────────────────

    public event EventHandler<RectAnnotation>? AnnotationAdded;
    public event EventHandler<RectAnnotation?>? AnnotationSelected;
    public event EventHandler<RectAnnotation>? AnnotationChanged;
    public event EventHandler<RectAnnotation>? AnnotationDeleted;
    public event EventHandler<float>? ZoomChanged;

    // ── Properties ─────────────────────────────────────────────────

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CanvasMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            UpdateCursor();
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CurrentClassId
    {
        get => _currentClassId;
        set { _currentClassId = value; }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<string> ClassNames
    {
        get => _classNames;
        set { _classNames = value ?? []; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RectAnnotation? SelectedAnnotation
    {
        get => _selectedAnnotation;
        set
        {
            _selectedAnnotation = value;
            AnnotationSelected?.Invoke(this, value);
            Invalidate();
        }
    }

    public float Zoom => _zoom;

    // ── Constructor ────────────────────────────────────────────────

    public AnnotationCanvas()
    {
        InitializeComponent();

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        BackColor = Color.FromArgb(30, 30, 30);
        TabStop = true;
    }

    // ── Public methods ─────────────────────────────────────────────

    /// <summary>
    /// Load an image and its annotations for display.
    /// </summary>
    public void LoadImage(Image? image, List<RectAnnotation> annotations)
    {
        _image = image;
        _imageW = image?.Width ?? 0;
        _imageH = image?.Height ?? 0;
        _annotations = annotations;
        _selectedAnnotation = null;
        _isDrawing = false;
        FitToWindow();
    }

    /// <summary>
    /// Fit the image to the control bounds.
    /// </summary>
    public void FitToWindow()
    {
        if (_image == null || _imageW == 0 || _imageH == 0)
        {
            _zoom = 1f;
            _panOffset = PointF.Empty;
            Invalidate();
            return;
        }

        float zw = (float)drawPanel.Width / _imageW;
        float zh = (float)drawPanel.Height / _imageH;
        _zoom = Math.Min(zw, zh) * 0.95f; // 5% margin
        // Center image
        _panOffset = new PointF(
            (drawPanel.Width / _zoom - _imageW) / 2f,
            (drawPanel.Height / _zoom - _imageH) / 2f);

        ZoomChanged?.Invoke(this, _zoom);
        Invalidate();
    }

    /// <summary>
    /// Set zoom level programmatically.
    /// </summary>
    public void SetZoom(float zoom)
    {
        var center = new PointF(drawPanel.Width / 2f, drawPanel.Height / 2f);
        ApplyZoom(zoom, center);
    }

    /// <summary>
    /// Delete the currently selected annotation.
    /// </summary>
    public bool DeleteSelected()
    {
        if (_selectedAnnotation == null) return false;
        var a = _selectedAnnotation;
        _annotations.Remove(a);
        _selectedAnnotation = null;
        AnnotationDeleted?.Invoke(this, a);
        AnnotationSelected?.Invoke(this, null);
        Invalidate();
        return true;
    }

    /// <summary>
    /// Refresh without reloading.
    /// </summary>
    public void RefreshCanvas() => Invalidate();

    /// <summary>
    /// Notify that the space key is being held (for pan mode).
    /// </summary>
    public void SetSpaceHeld(bool held)
    {
        _spaceHeld = held;
        UpdateCursor();
    }

    // ── Coordinate transforms ──────────────────────────────────────

    /// <summary>Screen coords -> image coords.</summary>
    private PointF ScreenToImage(Point screen)
    {
        float x = screen.X / _zoom - _panOffset.X;
        float y = screen.Y / _zoom - _panOffset.Y;
        return new PointF(x, y);
    }

    /// <summary>Image coords -> screen coords.</summary>
    private PointF ImageToScreen(float imgX, float imgY)
    {
        float x = (imgX + _panOffset.X) * _zoom;
        float y = (imgY + _panOffset.Y) * _zoom;
        return new PointF(x, y);
    }

    /// <summary>Convert annotation to screen rectangle.</summary>
    private RectangleF AnnotationToScreenRect(RectAnnotation a)
    {
        var (x1, y1, x2, y2) = a.ToPixelRect(_imageW, _imageH);
        var tl = ImageToScreen((float)x1, (float)y1);
        var br = ImageToScreen((float)x2, (float)y2);
        return RectangleF.FromLTRB(tl.X, tl.Y, br.X, br.Y);
    }

    // ── Painting ───────────────────────────────────────────────────

    private void DrawPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

        if (_image == null) return;

        // Draw image
        var tl = ImageToScreen(0, 0);
        var br = ImageToScreen(_imageW, _imageH);
        g.DrawImage(_image, tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);

        // Draw annotations
        foreach (var a in _annotations)
        {
            var rect = AnnotationToScreenRect(a);
            var color = ClassColors[a.ClassId % ClassColors.Length];
            bool selected = a == _selectedAnnotation;

            using var pen = new Pen(color, selected ? 3f : 2f);
            if (selected) pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

            // Fill with semi-transparent
            using var brush = new SolidBrush(Color.FromArgb(40, color));
            g.FillRectangle(brush, rect);

            // Label
            string label = a.ClassId < _classNames.Count
                ? _classNames[a.ClassId]
                : $"class_{a.ClassId}";

            using var labelFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            var labelSize = g.MeasureString(label, labelFont);
            var labelRect = new RectangleF(rect.X, rect.Y - labelSize.Height - 2,
                labelSize.Width + 6, labelSize.Height + 2);
            if (labelRect.Y < 0) labelRect.Y = rect.Y;

            using var labelBg = new SolidBrush(color);
            g.FillRectangle(labelBg, labelRect);
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString(label, labelFont, textBrush, labelRect.X + 3, labelRect.Y + 1);

            // Resize handles for selected
            if (selected) DrawResizeHandles(g, rect, color);
        }

        // Drawing preview
        if (_isDrawing)
        {
            var tl2 = ImageToScreen(_drawStart.X, _drawStart.Y);
            var br2 = ImageToScreen(_drawCurrent.X, _drawCurrent.Y);
            var drawRect = RectangleF.FromLTRB(
                Math.Min(tl2.X, br2.X), Math.Min(tl2.Y, br2.Y),
                Math.Max(tl2.X, br2.X), Math.Max(tl2.Y, br2.Y));

            var drawColor = ClassColors[_currentClassId % ClassColors.Length];
            using var drawPen = new Pen(drawColor, 2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.DrawRectangle(drawPen, drawRect.X, drawRect.Y, drawRect.Width, drawRect.Height);
            using var drawBrush = new SolidBrush(Color.FromArgb(30, drawColor));
            g.FillRectangle(drawBrush, drawRect);
        }
    }

    private void DrawResizeHandles(Graphics g, RectangleF rect, Color color)
    {
        int hs = HandleSize;
        using var brush = new SolidBrush(Color.White);
        using var pen = new Pen(color, 1.5f);

        var handles = GetHandleRects(rect);
        foreach (var h in handles.Values)
        {
            g.FillRectangle(brush, h);
            g.DrawRectangle(pen, h.X, h.Y, h.Width, h.Height);
        }
    }

    private Dictionary<ResizeHandle, RectangleF> GetHandleRects(RectangleF rect)
    {
        int hs = HandleSize;
        float hh = hs / 2f;
        float mx = rect.X + rect.Width / 2;
        float my = rect.Y + rect.Height / 2;

        return new Dictionary<ResizeHandle, RectangleF>
        {
            [ResizeHandle.TopLeft] = new(rect.X - hh, rect.Y - hh, hs, hs),
            [ResizeHandle.Top] = new(mx - hh, rect.Y - hh, hs, hs),
            [ResizeHandle.TopRight] = new(rect.Right - hh, rect.Y - hh, hs, hs),
            [ResizeHandle.Left] = new(rect.X - hh, my - hh, hs, hs),
            [ResizeHandle.Right] = new(rect.Right - hh, my - hh, hs, hs),
            [ResizeHandle.BottomLeft] = new(rect.X - hh, rect.Bottom - hh, hs, hs),
            [ResizeHandle.Bottom] = new(mx - hh, rect.Bottom - hh, hs, hs),
            [ResizeHandle.BottomRight] = new(rect.Right - hh, rect.Bottom - hh, hs, hs),
        };
    }

    // ── Mouse events ───────────────────────────────────────────────

    private void DrawPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        Focus();

        // Middle button or Space + Left = pan
        if (e.Button == MouseButtons.Middle ||
            (_spaceHeld && e.Button == MouseButtons.Left))
        {
            _isPanning = true;
            _panMouseStart = e.Location;
            _panOffsetStart = _panOffset;
            drawPanel.Cursor = Cursors.SizeAll;
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        var imgPt = ScreenToImage(e.Location);

        if (_mode == CanvasMode.Select)
        {
            // Check resize handles first
            if (_selectedAnnotation != null)
            {
                var rect = AnnotationToScreenRect(_selectedAnnotation);
                var handles = GetHandleRects(rect);
                foreach (var (handle, r) in handles)
                {
                    if (r.Contains(e.Location))
                    {
                        _activeHandle = handle;
                        _isDragging = true;
                        _dragStart = e.Location;
                        _dragOrigCX = _selectedAnnotation.CX;
                        _dragOrigCY = _selectedAnnotation.CY;
                        _dragOrigW = _selectedAnnotation.W;
                        _dragOrigH = _selectedAnnotation.H;
                        return;
                    }
                }
            }

            // Check hit on annotation body (reverse order = top-most first)
            RectAnnotation? hit = null;
            for (int i = _annotations.Count - 1; i >= 0; i--)
            {
                var rect = AnnotationToScreenRect(_annotations[i]);
                if (rect.Contains(e.Location))
                {
                    hit = _annotations[i];
                    break;
                }
            }

            SelectedAnnotation = hit;

            if (hit != null)
            {
                _isDragging = true;
                _dragStart = e.Location;
                _activeHandle = ResizeHandle.None;
                _dragOrigCX = hit.CX;
                _dragOrigCY = hit.CY;
                _dragOrigW = hit.W;
                _dragOrigH = hit.H;
            }
        }
        else if (_mode == CanvasMode.DrawRect)
        {
            // Clamp to image bounds
            imgPt.X = Math.Clamp(imgPt.X, 0, _imageW);
            imgPt.Y = Math.Clamp(imgPt.Y, 0, _imageH);
            _isDrawing = true;
            _drawStart = imgPt;
            _drawCurrent = imgPt;
        }
    }

    private void DrawPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            float dx = (e.X - _panMouseStart.X) / _zoom;
            float dy = (e.Y - _panMouseStart.Y) / _zoom;
            _panOffset = new PointF(_panOffsetStart.X + dx, _panOffsetStart.Y + dy);
            Invalidate();
            return;
        }

        if (_isDrawing)
        {
            var imgPt = ScreenToImage(e.Location);
            imgPt.X = Math.Clamp(imgPt.X, 0, _imageW);
            imgPt.Y = Math.Clamp(imgPt.Y, 0, _imageH);
            _drawCurrent = imgPt;
            Invalidate();
            return;
        }

        if (_isDragging && _selectedAnnotation != null)
        {
            float dxScreen = e.X - _dragStart.X;
            float dyScreen = e.Y - _dragStart.Y;

            // Convert screen delta to normalized delta
            double dxNorm = dxScreen / (_zoom * _imageW);
            double dyNorm = dyScreen / (_zoom * _imageH);

            if (_activeHandle == ResizeHandle.None)
            {
                // Move entire annotation
                _selectedAnnotation.CX = Math.Clamp(_dragOrigCX + dxNorm, 0, 1);
                _selectedAnnotation.CY = Math.Clamp(_dragOrigCY + dyNorm, 0, 1);
            }
            else
            {
                ApplyResize(dxNorm, dyNorm);
            }

            AnnotationChanged?.Invoke(this, _selectedAnnotation);
            Invalidate();
            return;
        }

        // Update cursor based on hover
        if (_mode == CanvasMode.Select && _selectedAnnotation != null)
        {
            var rect = AnnotationToScreenRect(_selectedAnnotation);
            var handles = GetHandleRects(rect);
            foreach (var (handle, r) in handles)
            {
                if (r.Contains(e.Location))
                {
                    drawPanel.Cursor = GetHandleCursor(handle);
                    return;
                }
            }
            if (rect.Contains(e.Location))
            {
                drawPanel.Cursor = Cursors.SizeAll;
                return;
            }
        }

        UpdateCursor();
    }

    private void DrawPanel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            UpdateCursor();
            return;
        }

        if (_isDrawing && e.Button == MouseButtons.Left)
        {
            _isDrawing = false;

            // Compute rect in image space
            float x1 = Math.Min(_drawStart.X, _drawCurrent.X);
            float y1 = Math.Min(_drawStart.Y, _drawCurrent.Y);
            float x2 = Math.Max(_drawStart.X, _drawCurrent.X);
            float y2 = Math.Max(_drawStart.Y, _drawCurrent.Y);

            // Min size check (at least 4 pixels in image space)
            if (x2 - x1 > 4 && y2 - y1 > 4)
            {
                var annotation = RectAnnotation.FromPixelRect(
                    x1, y1, x2, y2, _imageW, _imageH, _currentClassId);
                AnnotationAdded?.Invoke(this, annotation);
            }

            Invalidate();
            return;
        }

        if (_isDragging && _selectedAnnotation != null)
        {
            // Fire changed with final state (the panel handles undo)
            AnnotationChanged?.Invoke(this, _selectedAnnotation);
            _isDragging = false;
            _activeHandle = ResizeHandle.None;
        }
    }

    private void DrawPanel_MouseWheel(object? sender, MouseEventArgs e)
    {
        float factor = e.Delta > 0 ? 1.15f : 1f / 1.15f;
        float newZoom = Math.Clamp(_zoom * factor, 0.05f, 50f);
        ApplyZoom(newZoom, e.Location);
    }

    // ── Zoom ───────────────────────────────────────────────────────

    private void ApplyZoom(float newZoom, PointF screenPivot)
    {
        // Zoom around the pivot point
        var imgPivot = ScreenToImage(Point.Round(screenPivot));
        _zoom = Math.Clamp(newZoom, 0.05f, 50f);
        // Adjust pan so that imgPivot stays at screenPivot
        _panOffset = new PointF(
            screenPivot.X / _zoom - imgPivot.X,
            screenPivot.Y / _zoom - imgPivot.Y);

        ZoomChanged?.Invoke(this, _zoom);
        Invalidate();
    }

    // ── Resize logic ───────────────────────────────────────────────

    private void ApplyResize(double dxNorm, double dyNorm)
    {
        if (_selectedAnnotation == null) return;
        var a = _selectedAnnotation;

        double cx = _dragOrigCX, cy = _dragOrigCY, w = _dragOrigW, h = _dragOrigH;
        double left = cx - w / 2, top = cy - h / 2, right = cx + w / 2, bottom = cy + h / 2;

        switch (_activeHandle)
        {
            case ResizeHandle.TopLeft:
                left += dxNorm; top += dyNorm; break;
            case ResizeHandle.Top:
                top += dyNorm; break;
            case ResizeHandle.TopRight:
                right += dxNorm; top += dyNorm; break;
            case ResizeHandle.Left:
                left += dxNorm; break;
            case ResizeHandle.Right:
                right += dxNorm; break;
            case ResizeHandle.BottomLeft:
                left += dxNorm; bottom += dyNorm; break;
            case ResizeHandle.Bottom:
                bottom += dyNorm; break;
            case ResizeHandle.BottomRight:
                right += dxNorm; bottom += dyNorm; break;
        }

        // Clamp & min size
        left = Math.Clamp(left, 0, 1);
        top = Math.Clamp(top, 0, 1);
        right = Math.Clamp(right, 0, 1);
        bottom = Math.Clamp(bottom, 0, 1);

        if (right - left < 0.005) right = left + 0.005;
        if (bottom - top < 0.005) bottom = top + 0.005;

        a.CX = (left + right) / 2;
        a.CY = (top + bottom) / 2;
        a.W = right - left;
        a.H = bottom - top;
    }

    // ── Cursor ─────────────────────────────────────────────────────

    private void UpdateCursor()
    {
        if (_spaceHeld || _isPanning)
            drawPanel.Cursor = Cursors.SizeAll;
        else if (_mode == CanvasMode.DrawRect)
            drawPanel.Cursor = Cursors.Cross;
        else
            drawPanel.Cursor = Cursors.Default;
    }

    private static Cursor GetHandleCursor(ResizeHandle handle) => handle switch
    {
        ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
        ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
        ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
        ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
        _ => Cursors.Default
    };

    // ── Overrides ──────────────────────────────────────────────────

    public override void Refresh()
    {
        base.Refresh();
        Invalidate();
    }

    private void Invalidate()
    {
        if (drawPanel != null && !drawPanel.IsDisposed)
            drawPanel.Invalidate();
    }
}
