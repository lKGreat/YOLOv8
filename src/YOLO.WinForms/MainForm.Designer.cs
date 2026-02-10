namespace YOLO.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.windowBar = new AntdUI.PageHeader();
        this.tabs = new AntdUI.Tabs();
        this.tabAnnotation = new AntdUI.TabPage();
        this.tabTraining = new AntdUI.TabPage();
        this.tabExport = new AntdUI.TabPage();
        this.tabInference = new AntdUI.TabPage();
        this.tabModelTest = new AntdUI.TabPage();
        this.tabMLNetTraining = new AntdUI.TabPage();
        this.panelStatus = new System.Windows.Forms.Panel();
        this.lblStatus = new AntdUI.Label();
        this.lblDevice = new AntdUI.Label();

        this.SuspendLayout();

        // ── windowBar (title bar) ──────────────────────────────
        this.windowBar.Dock = DockStyle.Top;
        this.windowBar.Size = new Size(1280, 40);
        this.windowBar.ShowIcon = true;
        this.windowBar.ShowButton = true;
        this.windowBar.Text = "YOLO Training Tool";
        this.windowBar.SubText = "C# Native";

        // ── tabs ───────────────────────────────────────────────
        this.tabs.Dock = DockStyle.Fill;
        this.tabs.Type = AntdUI.TabType.Card;

        this.tabAnnotation.Text = "Annotation";
        this.tabAnnotation.Padding = new Padding(4);

        this.tabTraining.Text = "Training";
        this.tabTraining.Padding = new Padding(4);

        this.tabExport.Text = "Export";
        this.tabExport.Padding = new Padding(4);

        this.tabInference.Text = "Inference";
        this.tabInference.Padding = new Padding(4);

        this.tabModelTest.Text = "Model Test";
        this.tabModelTest.Padding = new Padding(4);

        this.tabMLNetTraining.Text = "ML.NET Training";
        this.tabMLNetTraining.Padding = new Padding(4);

        this.tabs.Pages.Add(this.tabAnnotation);
        this.tabs.Pages.Add(this.tabTraining);
        this.tabs.Pages.Add(this.tabMLNetTraining);
        this.tabs.Pages.Add(this.tabExport);
        this.tabs.Pages.Add(this.tabInference);
        this.tabs.Pages.Add(this.tabModelTest);

        // ── panelStatus (bottom status bar) ────────────────────
        this.panelStatus.Dock = DockStyle.Bottom;
        this.panelStatus.Height = 30;
        this.panelStatus.BackColor = Color.FromArgb(245, 245, 245);
        this.panelStatus.Padding = new Padding(12, 0, 12, 0);

        this.lblStatus.Dock = DockStyle.Fill;
        this.lblStatus.Text = "Ready";
        this.lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        this.lblStatus.Font = new Font("Segoe UI", 9F);
        this.lblStatus.ForeColor = Color.FromArgb(100, 100, 100);

        this.lblDevice.Dock = DockStyle.Right;
        this.lblDevice.AutoSize = true;
        this.lblDevice.Text = "CPU";
        this.lblDevice.TextAlign = ContentAlignment.MiddleRight;
        this.lblDevice.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.lblDevice.ForeColor = Color.FromArgb(22, 119, 255);

        this.panelStatus.Controls.Add(this.lblStatus);
        this.panelStatus.Controls.Add(this.lblDevice);

        // ── MainForm ───────────────────────────────────────────
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1360, 800);
        this.Controls.Add(this.tabs);
        this.Controls.Add(this.windowBar);
        this.Controls.Add(this.panelStatus);
        this.MinimumSize = new Size(1024, 640);
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "YOLO Training Tool";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private AntdUI.PageHeader windowBar;
    private AntdUI.Tabs tabs;
    private AntdUI.TabPage tabAnnotation;
    private AntdUI.TabPage tabTraining;
    private AntdUI.TabPage tabExport;
    private AntdUI.TabPage tabInference;
    private AntdUI.TabPage tabModelTest;
    private AntdUI.TabPage tabMLNetTraining;
    private System.Windows.Forms.Panel panelStatus;
    private AntdUI.Label lblStatus;
    private AntdUI.Label lblDevice;
}
