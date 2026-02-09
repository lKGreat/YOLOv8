using System.ComponentModel;

namespace YOLO.WinForms.Controls;

/// <summary>
/// Custom image preview control with zoom, fit, and save functionality.
/// Suitable for displaying original and detection result images.
/// </summary>
public partial class ImagePreview : UserControl
{
    private float zoomLevel = 1.0f;
    private const float ZoomStep = 0.25f;
    private const float MinZoom = 0.1f;
    private const float MaxZoom = 10.0f;

    public ImagePreview()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        btnZoomIn.Click += (s, e) => SetZoom(zoomLevel + ZoomStep);
        btnZoomOut.Click += (s, e) => SetZoom(zoomLevel - ZoomStep);
        btnFit.Click += (s, e) =>
        {
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            zoomLevel = 1.0f;
            UpdateInfoLabel();
        };
        btnSave.Click += BtnSave_Click;
        pictureBox.MouseWheel += PictureBox_MouseWheel;
    }

    /// <summary>
    /// Gets or sets the displayed image.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? Image
    {
        get => pictureBox.Image;
        set
        {
            pictureBox.Image = value;
            zoomLevel = 1.0f;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            UpdateInfoLabel();
        }
    }

    /// <summary>
    /// Gets or sets the info text displayed at the bottom.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string InfoText
    {
        get => lblInfo.Text;
        set => lblInfo.Text = value;
    }

    private void SetZoom(float newZoom)
    {
        zoomLevel = Math.Clamp(newZoom, MinZoom, MaxZoom);

        if (pictureBox.Image != null)
        {
            pictureBox.SizeMode = PictureBoxSizeMode.AutoSize;
            int w = (int)(pictureBox.Image.Width * zoomLevel);
            int h = (int)(pictureBox.Image.Height * zoomLevel);
            pictureBox.Size = new Size(w, h);
        }

        UpdateInfoLabel();
    }

    private void PictureBox_MouseWheel(object? sender, MouseEventArgs e)
    {
        float delta = e.Delta > 0 ? ZoomStep : -ZoomStep;
        SetZoom(zoomLevel + delta);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (pictureBox.Image == null) return;

        using var dlg = new SaveFileDialog
        {
            Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp",
            DefaultExt = "png"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            var format = Path.GetExtension(dlg.FileName).ToLower() switch
            {
                ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                _ => System.Drawing.Imaging.ImageFormat.Png
            };
            pictureBox.Image.Save(dlg.FileName, format);
        }
    }

    private void UpdateInfoLabel()
    {
        if (pictureBox.Image != null)
        {
            lblInfo.Text = $"{pictureBox.Image.Width} x {pictureBox.Image.Height} | Zoom: {zoomLevel:P0}";
        }
        else
        {
            lblInfo.Text = "No image loaded";
        }
    }
}
