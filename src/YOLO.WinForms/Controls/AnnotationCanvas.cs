using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
    TopLeft, TopRight,
    BottomLeft, BottomRight
}

/// <summary>
/// High-performance double-buffered canvas for image annotation.
///
/// Interaction model:
///  - Left-click ON a box (any mode) → select it, show 4 corner handles + close X
///  - Drag a corner handle → resize
///  - Drag the box body → move
///  - Click the X button → delete
///  - Left-click on empty space in DrawRect mode → draw a new box
///  - Right-click on a box → context menu (delete / change class)
///  - Middle-click or Space+Left → pan
///  - Mouse wheel → zoom
/// </summary>
public partial class AnnotationCanvas : UserControl
{
    // ── Cached GDI resources (created once, shared across instances) ─

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

    private static readonly Pen[] NormalPens;
    private static readonly Pen[] SelectedPens;
    private static readonly SolidBrush[] FillBrushes;
    private static readonly SolidBrush[] LabelBgBrushes;
    private static readonly Pen[] HandlePens;
    private static readonly Pen[] DrawPreviewPens;
    private static readonly SolidBrush[] DrawPreviewFills;

    private static readonly Font LabelFont = new("Segoe UI", 9f, FontStyle.Bold);
    private static readonly Font CloseFont = new("Segoe UI", 9f, FontStyle.Bold);
    private static readonly SolidBrush WhiteBrush = new(Color.White);
    private static readonly SolidBrush WhiteTextBrush = new(Color.White);
    private static readonly SolidBrush CloseBgBrush = new(Color.FromArgb(220, 220, 50, 50));
    private static readonly SolidBrush CloseBgHoverBrush = new(Color.FromArgb(240, 240, 60, 60));
    private static readonly Pen CrosshairPen = new(Color.FromArgb(120, 255, 255, 255), 1f)
    {
        DashStyle = DashStyle.Dot
    };

    private const int HandleSize = 10;
    private const float HandleHalf = HandleSize / 2f;
    private const int CloseBtnSize = 18;  // size of the X delete button

    static AnnotationCanvas()
    {
        int n = ClassColors.Length;
        NormalPens = new Pen[n];
        SelectedPens = new Pen[n];
        FillBrushes = new SolidBrush[n];
        LabelBgBrushes = new SolidBrush[n];
        HandlePens = new Pen[n];
        DrawPreviewPens = new Pen[n];
        DrawPreviewFills = new SolidBrush[n];

        for (int i = 0; i < n; i++)
        {
            var c = ClassColors[i];
            NormalPens[i] = new Pen(c, 2f);
            SelectedPens[i] = new Pen(c, 3f);
            FillBrushes[i] = new SolidBrush(Color.FromArgb(40, c));
            LabelBgBrushes[i] = new SolidBrush(c);
            HandlePens[i] = new Pen(c, 1.5f);
            DrawPreviewPens[i] = new Pen(c, 2f) { DashStyle = DashStyle.Dash };
            DrawPreviewFills[i] = new SolidBrush(Color.FromArgb(30, c));
        }
    }

    // ── State ──────────────────────────────────────────────────────

    private Image? _image;
    private int _imageW, _imageH;
    private List<RectAnnotation> _annotations = [];
    private int _currentClassId;
    private List<string> _classNames = [];

    // Zoom / pan
    private float _zoom = 1f;
    private PointF _panOffset;

    // Drawing
    private CanvasMode _mode = CanvasMode.DrawRect;
    private bool _isDrawing;
    private PointF _drawStart;
    private PointF _drawCurrent;

    // Selection
    private RectAnnotation? _selectedAnnotation;
    private bool _isDragging;
    private PointF _dragStart;
    private double _dragOrigCX, _dragOrigCY, _dragOrigW, _dragOrigH;
    private ResizeHandle _activeHandle = ResizeHandle.None;

    // Pan
    private bool _isPanning;
    private Point _panMouseStart;
    private PointF _panOffsetStart;

    // Space key held = temporary pan mode
    private bool _spaceHeld;

    // Hover state for close button
    private bool _hoveringCloseBtn;

    // ── Events ─────────────────────────────────────────────────────

    public event EventHandler<RectAnnotation>? AnnotationAdded;
    public event EventHandler<RectAnnotation?>? AnnotationSelected;
    public event EventHandler<RectAnnotation>? AnnotationChanged;
    public event EventHandler<RectAnnotation>? AnnotationDeleted;
    public event EventHandler<(RectAnnotation Annotation, int NewClassId)>? AnnotationClassChangeRequested;
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
            InvalidateCanvas();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CurrentClassId
    {
        get => _currentClassId;
        set => _currentClassId = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<string> ClassNames
    {
        get => _classNames;
        set { _classNames = value ?? []; InvalidateCanvas(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RectAnnotation? SelectedAnnotation
    {
        get => _selectedAnnotation;
        set
        {
            _selectedAnnotation = value;
            AnnotationSelected?.Invoke(this, value);
            InvalidateCanvas();
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

    public void ClearImage()
    {
        _image = null;
        _imageW = 0;
        _imageH = 0;
        _annotations = [];
        _selectedAnnotation = null;
        _isDrawing = false;
        InvalidateCanvas();
    }

    public void FitToWindow()
    {
        if (_image == null || _imageW == 0 || _imageH == 0)
        {
            _zoom = 1f;
            _panOffset = PointF.Empty;
            InvalidateCanvas();
            return;
        }

        float zw = (float)drawPanel.Width / _imageW;
        float zh = (float)drawPanel.Height / _imageH;
        _zoom = Math.Min(zw, zh) * 0.95f;
        _panOffset = new PointF(
            (drawPanel.Width / _zoom - _imageW) / 2f,
            (drawPanel.Height / _zoom - _imageH) / 2f);

        ZoomChanged?.Invoke(this, _zoom);
        InvalidateCanvas();
    }

    public void SetZoom(float zoom)
    {
        var center = new PointF(drawPanel.Width / 2f, drawPanel.Height / 2f);
        ApplyZoom(zoom, center);
    }

    public bool DeleteSelected()
    {
        if (_selectedAnnotation == null) return false;
        var a = _selectedAnnotation;
        _annotations.Remove(a);
        _selectedAnnotation = null;
        AnnotationDeleted?.Invoke(this, a);
        AnnotationSelected?.Invoke(this, null);
        InvalidateCanvas();
        return true;
    }

    public void RefreshCanvas() => InvalidateCanvas();

    public void SetSpaceHeld(bool held)
    {
        _spaceHeld = held;
        UpdateCursor();
    }

    public static Bitmap? LoadOptimizedBitmap(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var original = Image.FromStream(fs, false, false);
            var bmp = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppPArgb);
            bmp.SetResolution(original.HorizontalResolution, original.VerticalResolution);
            using var g = Graphics.FromImage(bmp);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(original, 0, 0, original.Width, original.Height);
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    // ── Coordinate transforms ──────────────────────────────────────

    private PointF ScreenToImage(Point screen)
    {
        float x = screen.X / _zoom - _panOffset.X;
        float y = screen.Y / _zoom - _panOffset.Y;
        return new PointF(x, y);
    }

    private PointF ImageToScreen(float imgX, float imgY)
    {
        float x = (imgX + _panOffset.X) * _zoom;
        float y = (imgY + _panOffset.Y) * _zoom;
        return new PointF(x, y);
    }

    private RectangleF AnnotationToScreenRect(RectAnnotation a)
    {
        var (x1, y1, x2, y2) = a.ToPixelRect(_imageW, _imageH);
        var tl = ImageToScreen((float)x1, (float)y1);
        var br = ImageToScreen((float)x2, (float)y2);
        return RectangleF.FromLTRB(tl.X, tl.Y, br.X, br.Y);
    }

    // ── Hit-test helpers ───────────────────────────────────────────

    /// <summary>Get the close-button rect for an annotation's screen rect.</summary>
    private static RectangleF GetCloseButtonRect(RectangleF boxRect)
    {
        // Positioned at top-right corner, slightly outside the box
        return new RectangleF(
            boxRect.Right - CloseBtnSize + 4,
            boxRect.Y - 4,
            CloseBtnSize, CloseBtnSize);
    }

    /// <summary>Get the 4 corner handle rects for an annotation's screen rect.</summary>
    private static Dictionary<ResizeHandle, RectangleF> GetHandleRects(RectangleF rect)
    {
        return new Dictionary<ResizeHandle, RectangleF>(4)
        {
            [ResizeHandle.TopLeft] = new(rect.X - HandleHalf, rect.Y - HandleHalf, HandleSize, HandleSize),
            [ResizeHandle.TopRight] = new(rect.Right - HandleHalf, rect.Y - HandleHalf, HandleSize, HandleSize),
            [ResizeHandle.BottomLeft] = new(rect.X - HandleHalf, rect.Bottom - HandleHalf, HandleSize, HandleSize),
            [ResizeHandle.BottomRight] = new(rect.Right - HandleHalf, rect.Bottom - HandleHalf, HandleSize, HandleSize),
        };
    }

    /// <summary>Hit-test annotations (top-most first).</summary>
    private RectAnnotation? HitTestAnnotation(Point screenLocation)
    {
        for (int i = _annotations.Count - 1; i >= 0; i--)
        {
            var rect = AnnotationToScreenRect(_annotations[i]);
            if (rect.Contains(screenLocation))
                return _annotations[i];
        }
        return null;
    }

    // ── Painting ───────────────────────────────────────────────────

    private void DrawPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(drawPanel.BackColor);

        if (_image == null) return;

        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = _zoom > 2f
            ? InterpolationMode.NearestNeighbor
            : InterpolationMode.Bilinear;
        g.PixelOffsetMode = _zoom > 2f
            ? PixelOffsetMode.Half
            : PixelOffsetMode.HighQuality;

        // Draw image
        var tl = ImageToScreen(0, 0);
        var br = ImageToScreen(_imageW, _imageH);
        g.DrawImage(_image, tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);

        // Draw annotations
        for (int i = 0; i < _annotations.Count; i++)
        {
            var a = _annotations[i];
            var rect = AnnotationToScreenRect(a);
            int colorIdx = a.ClassId % ClassColors.Length;
            bool selected = a == _selectedAnnotation;

            // Border
            var pen = selected ? SelectedPens[colorIdx] : NormalPens[colorIdx];
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

            // Semi-transparent fill
            g.FillRectangle(FillBrushes[colorIdx], rect);

            // Label at top-left
            string label = a.ClassId < _classNames.Count
                ? _classNames[a.ClassId]
                : $"class_{a.ClassId}";

            var labelSize = g.MeasureString(label, LabelFont);
            float labelY = rect.Y - labelSize.Height - 2;
            if (labelY < 0) labelY = rect.Y;

            var labelRect = new RectangleF(rect.X, labelY,
                labelSize.Width + 6, labelSize.Height + 2);

            g.FillRectangle(LabelBgBrushes[colorIdx], labelRect);
            g.DrawString(label, LabelFont, WhiteTextBrush, labelRect.X + 3, labelRect.Y + 1);

            // Selected → draw 4 corner handles + close X button
            if (selected)
            {
                DrawCornerHandles(g, rect, colorIdx);
                DrawCloseButton(g, rect);
            }
        }

        // Drawing preview
        if (_isDrawing)
        {
            var tl2 = ImageToScreen(_drawStart.X, _drawStart.Y);
            var br2 = ImageToScreen(_drawCurrent.X, _drawCurrent.Y);
            var drawRect = RectangleF.FromLTRB(
                Math.Min(tl2.X, br2.X), Math.Min(tl2.Y, br2.Y),
                Math.Max(tl2.X, br2.X), Math.Max(tl2.Y, br2.Y));

            int previewColorIdx = _currentClassId % ClassColors.Length;
            g.DrawRectangle(DrawPreviewPens[previewColorIdx],
                drawRect.X, drawRect.Y, drawRect.Width, drawRect.Height);
            g.FillRectangle(DrawPreviewFills[previewColorIdx], drawRect);
        }
    }

    private void DrawCornerHandles(Graphics g, RectangleF rect, int colorIdx)
    {
        var pen = HandlePens[colorIdx];
        DrawHandle(g, pen, rect.X, rect.Y);
        DrawHandle(g, pen, rect.Right, rect.Y);
        DrawHandle(g, pen, rect.X, rect.Bottom);
        DrawHandle(g, pen, rect.Right, rect.Bottom);
    }

    private static void DrawHandle(Graphics g, Pen pen, float cx, float cy)
    {
        float x = cx - HandleHalf;
        float y = cy - HandleHalf;
        g.FillRectangle(WhiteBrush, x, y, HandleSize, HandleSize);
        g.DrawRectangle(pen, x, y, HandleSize, HandleSize);
    }

    /// <summary>Draw the red X close button at top-right of the annotation box.</summary>
    private void DrawCloseButton(Graphics g, RectangleF boxRect)
    {
        var btnRect = GetCloseButtonRect(boxRect);
        var bg = _hoveringCloseBtn ? CloseBgHoverBrush : CloseBgBrush;

        // Rounded rectangle background
        g.FillEllipse(bg, btnRect);

        // Draw the X
        float cx = btnRect.X + btnRect.Width / 2f;
        float cy = btnRect.Y + btnRect.Height / 2f;
        float d = 4f; // half-length of X strokes
        using var xPen = new Pen(Color.White, 2f);
        g.DrawLine(xPen, cx - d, cy - d, cx + d, cy + d);
        g.DrawLine(xPen, cx + d, cy - d, cx - d, cy + d);
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

        // Right-click → context menu on annotation
        if (e.Button == MouseButtons.Right)
        {
            HandleRightClick(e.Location);
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        // ── Priority 1: close button on selected annotation ──
        if (_selectedAnnotation != null)
        {
            var selRect = AnnotationToScreenRect(_selectedAnnotation);
            var closeRect = GetCloseButtonRect(selRect);
            if (closeRect.Contains(e.Location))
            {
                // Delete this annotation
                var toDelete = _selectedAnnotation;
                _annotations.Remove(toDelete);
                _selectedAnnotation = null;
                AnnotationDeleted?.Invoke(this, toDelete);
                AnnotationSelected?.Invoke(this, null);
                InvalidateCanvas();
                return;
            }
        }

        // ── Priority 2: resize handle on selected annotation ──
        if (_selectedAnnotation != null)
        {
            var selRect = AnnotationToScreenRect(_selectedAnnotation);
            var handles = GetHandleRects(selRect);
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

        // ── Priority 3: click ON an existing annotation (any mode) ──
        var hit = HitTestAnnotation(e.Location);
        if (hit != null)
        {
            SelectedAnnotation = hit;
            _isDragging = true;
            _dragStart = e.Location;
            _activeHandle = ResizeHandle.None;
            _dragOrigCX = hit.CX;
            _dragOrigCY = hit.CY;
            _dragOrigW = hit.W;
            _dragOrigH = hit.H;
            return;
        }

        // ── Priority 4: click on empty space ──
        // Deselect any selected annotation
        if (_selectedAnnotation != null)
        {
            SelectedAnnotation = null;
        }

        // In DrawRect mode, start drawing on empty space
        if (_mode == CanvasMode.DrawRect)
        {
            var imgPt = ScreenToImage(e.Location);
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
            InvalidateCanvas();
            return;
        }

        if (_isDrawing)
        {
            var imgPt = ScreenToImage(e.Location);
            imgPt.X = Math.Clamp(imgPt.X, 0, _imageW);
            imgPt.Y = Math.Clamp(imgPt.Y, 0, _imageH);
            _drawCurrent = imgPt;
            InvalidateCanvas();
            return;
        }

        if (_isDragging && _selectedAnnotation != null)
        {
            float dxScreen = e.X - _dragStart.X;
            float dyScreen = e.Y - _dragStart.Y;

            double dxNorm = dxScreen / (_zoom * _imageW);
            double dyNorm = dyScreen / (_zoom * _imageH);

            if (_activeHandle == ResizeHandle.None)
            {
                _selectedAnnotation.CX = Math.Clamp(_dragOrigCX + dxNorm, 0, 1);
                _selectedAnnotation.CY = Math.Clamp(_dragOrigCY + dyNorm, 0, 1);
            }
            else
            {
                ApplyResize(dxNorm, dyNorm);
            }

            AnnotationChanged?.Invoke(this, _selectedAnnotation);
            InvalidateCanvas();
            return;
        }

        // ── Hover cursor updates ──
        // Check close button hover (triggers repaint for highlight)
        bool wasHovering = _hoveringCloseBtn;
        _hoveringCloseBtn = false;

        if (_selectedAnnotation != null)
        {
            var selRect = AnnotationToScreenRect(_selectedAnnotation);

            // Close button
            var closeRect = GetCloseButtonRect(selRect);
            if (closeRect.Contains(e.Location))
            {
                _hoveringCloseBtn = true;
                drawPanel.Cursor = Cursors.Hand;
                if (!wasHovering) InvalidateCanvas();
                return;
            }

            // Resize handles
            var handles = GetHandleRects(selRect);
            foreach (var (handle, r) in handles)
            {
                if (r.Contains(e.Location))
                {
                    drawPanel.Cursor = GetHandleCursor(handle);
                    if (wasHovering) InvalidateCanvas();
                    return;
                }
            }

            // Body
            if (selRect.Contains(e.Location))
            {
                drawPanel.Cursor = Cursors.SizeAll;
                if (wasHovering) InvalidateCanvas();
                return;
            }
        }

        // Check hover on any annotation body (show hand cursor)
        var hoverHit = HitTestAnnotation(e.Location);
        if (hoverHit != null)
        {
            drawPanel.Cursor = Cursors.Hand;
            if (wasHovering) InvalidateCanvas();
            return;
        }

        if (wasHovering) InvalidateCanvas();
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

            float x1 = Math.Min(_drawStart.X, _drawCurrent.X);
            float y1 = Math.Min(_drawStart.Y, _drawCurrent.Y);
            float x2 = Math.Max(_drawStart.X, _drawCurrent.X);
            float y2 = Math.Max(_drawStart.Y, _drawCurrent.Y);

            if (x2 - x1 > 4 && y2 - y1 > 4)
            {
                var annotation = RectAnnotation.FromPixelRect(
                    x1, y1, x2, y2, _imageW, _imageH, _currentClassId);
                AnnotationAdded?.Invoke(this, annotation);
            }

            InvalidateCanvas();
            return;
        }

        if (_isDragging && _selectedAnnotation != null)
        {
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
        var imgPivot = ScreenToImage(Point.Round(screenPivot));
        _zoom = Math.Clamp(newZoom, 0.05f, 50f);
        _panOffset = new PointF(
            screenPivot.X / _zoom - imgPivot.X,
            screenPivot.Y / _zoom - imgPivot.Y);

        ZoomChanged?.Invoke(this, _zoom);
        InvalidateCanvas();
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
            case ResizeHandle.TopRight:
                right += dxNorm; top += dyNorm; break;
            case ResizeHandle.BottomLeft:
                left += dxNorm; bottom += dyNorm; break;
            case ResizeHandle.BottomRight:
                right += dxNorm; bottom += dyNorm; break;
        }

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
        _ => Cursors.Default
    };

    // ── Right-click context menu ──────────────────────────────────

    private void HandleRightClick(Point screenLocation)
    {
        var hit = HitTestAnnotation(screenLocation);
        if (hit == null) return;

        SelectedAnnotation = hit;

        var menu = new ContextMenuStrip();
        menu.Font = new Font("Segoe UI", 9F);

        var deleteItem = new ToolStripMenuItem("Delete annotation");
        deleteItem.ShortcutKeyDisplayString = "Del";
        deleteItem.Click += (s, e) =>
        {
            _annotations.Remove(hit);
            _selectedAnnotation = null;
            AnnotationDeleted?.Invoke(this, hit);
            AnnotationSelected?.Invoke(this, null);
            InvalidateCanvas();
        };
        menu.Items.Add(deleteItem);

        menu.Items.Add(new ToolStripSeparator());

        if (_classNames.Count > 0)
        {
            var changeClassItem = new ToolStripMenuItem("Change class");
            for (int i = 0; i < _classNames.Count; i++)
            {
                int classId = i;
                string label = $"[{i}] {_classNames[i]}";
                var classItem = new ToolStripMenuItem(label);
                if (classId == hit.ClassId)
                {
                    classItem.Checked = true;
                    classItem.Enabled = false;
                }
                classItem.Click += (s, e) =>
                {
                    hit.ClassId = classId;
                    AnnotationClassChangeRequested?.Invoke(this, (hit, classId));
                    AnnotationChanged?.Invoke(this, hit);
                    InvalidateCanvas();
                };
                changeClassItem.DropDownItems.Add(classItem);
            }
            menu.Items.Add(changeClassItem);
        }

        menu.Show(drawPanel, screenLocation);
    }

    // ── Invalidation ──────────────────────────────────────────────

    private void InvalidateCanvas()
    {
        if (drawPanel != null && !drawPanel.IsDisposed)
            drawPanel.Invalidate();
    }

    public override void Refresh()
    {
        base.Refresh();
        InvalidateCanvas();
    }
}
