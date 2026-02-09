namespace YOLO.WinForms.Controls;

partial class ImagePreview
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    #region Component Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();

        this.pictureBox = new PictureBox();
        this.lblInfo = new Label();
        this.panelToolbar = new FlowLayoutPanel();
        this.btnZoomIn = new Button();
        this.btnZoomOut = new Button();
        this.btnFit = new Button();
        this.btnSave = new Button();

        ((System.ComponentModel.ISupportInitialize)this.pictureBox).BeginInit();
        this.SuspendLayout();

        // panelToolbar
        this.panelToolbar.Dock = DockStyle.Top;
        this.panelToolbar.Height = 34;
        this.panelToolbar.FlowDirection = FlowDirection.LeftToRight;
        this.panelToolbar.Padding = new Padding(2);
        this.panelToolbar.BackColor = Color.FromArgb(45, 45, 48);
        this.panelToolbar.Controls.Add(this.btnZoomIn);
        this.panelToolbar.Controls.Add(this.btnZoomOut);
        this.panelToolbar.Controls.Add(this.btnFit);
        this.panelToolbar.Controls.Add(this.btnSave);

        // btnZoomIn
        this.btnZoomIn.Text = "+";
        this.btnZoomIn.Size = new Size(30, 28);
        this.btnZoomIn.FlatStyle = FlatStyle.Flat;
        this.btnZoomIn.ForeColor = Color.White;
        this.btnZoomIn.FlatAppearance.BorderSize = 0;
        this.btnZoomIn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.btnZoomIn.Cursor = Cursors.Hand;

        // btnZoomOut
        this.btnZoomOut.Text = "-";
        this.btnZoomOut.Size = new Size(30, 28);
        this.btnZoomOut.FlatStyle = FlatStyle.Flat;
        this.btnZoomOut.ForeColor = Color.White;
        this.btnZoomOut.FlatAppearance.BorderSize = 0;
        this.btnZoomOut.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.btnZoomOut.Cursor = Cursors.Hand;

        // btnFit
        this.btnFit.Text = "Fit";
        this.btnFit.Size = new Size(40, 28);
        this.btnFit.FlatStyle = FlatStyle.Flat;
        this.btnFit.ForeColor = Color.White;
        this.btnFit.FlatAppearance.BorderSize = 0;
        this.btnFit.Font = new Font("Segoe UI", 9F);
        this.btnFit.Cursor = Cursors.Hand;

        // btnSave
        this.btnSave.Text = "Save";
        this.btnSave.Size = new Size(50, 28);
        this.btnSave.FlatStyle = FlatStyle.Flat;
        this.btnSave.ForeColor = Color.White;
        this.btnSave.FlatAppearance.BorderSize = 0;
        this.btnSave.Font = new Font("Segoe UI", 9F);
        this.btnSave.Cursor = Cursors.Hand;

        // pictureBox
        this.pictureBox.Dock = DockStyle.Fill;
        this.pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        this.pictureBox.BackColor = Color.FromArgb(30, 30, 30);
        this.pictureBox.BorderStyle = BorderStyle.None;

        // lblInfo
        this.lblInfo.Dock = DockStyle.Bottom;
        this.lblInfo.Height = 22;
        this.lblInfo.TextAlign = ContentAlignment.MiddleLeft;
        this.lblInfo.Font = new Font("Segoe UI", 8F);
        this.lblInfo.ForeColor = Color.FromArgb(180, 180, 180);
        this.lblInfo.BackColor = Color.FromArgb(45, 45, 48);
        this.lblInfo.Padding = new Padding(4, 0, 0, 0);

        // ImagePreview
        this.Controls.Add(this.pictureBox);
        this.Controls.Add(this.panelToolbar);
        this.Controls.Add(this.lblInfo);
        this.Name = "ImagePreview";
        this.Size = new Size(500, 400);

        ((System.ComponentModel.ISupportInitialize)this.pictureBox).EndInit();
        this.ResumeLayout(false);
    }

    #endregion

    private PictureBox pictureBox;
    private Label lblInfo;
    private FlowLayoutPanel panelToolbar;
    private Button btnZoomIn;
    private Button btnZoomOut;
    private Button btnFit;
    private Button btnSave;
}
