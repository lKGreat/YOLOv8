namespace YOLO.WinForms.Controls;

partial class AnnotationCanvas
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Component Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.drawPanel = new DoubleBufferedPanel();
        this.SuspendLayout();

        // drawPanel — uses DoubleBufferedPanel for flicker-free rendering
        this.drawPanel.Dock = DockStyle.Fill;
        this.drawPanel.BackColor = Color.FromArgb(30, 30, 30);
        this.drawPanel.Location = new Point(0, 0);
        this.drawPanel.Name = "drawPanel";
        this.drawPanel.Size = new Size(800, 600);
        this.drawPanel.TabIndex = 0;
        this.drawPanel.Paint += DrawPanel_Paint;
        this.drawPanel.MouseDown += DrawPanel_MouseDown;
        this.drawPanel.MouseMove += DrawPanel_MouseMove;
        this.drawPanel.MouseUp += DrawPanel_MouseUp;
        this.drawPanel.MouseWheel += DrawPanel_MouseWheel;

        // AnnotationCanvas
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.Controls.Add(this.drawPanel);
        this.Name = "AnnotationCanvas";
        this.Size = new Size(800, 600);
        this.ResumeLayout(false);
    }

    #endregion

    private DoubleBufferedPanel drawPanel;
}
