namespace YOLO.WinForms.Controls;

partial class MetricsChart
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

        this.tabMetrics = new TabControl();
        this.tabLoss = new TabPage();
        this.tabMap = new TabPage();
        this.tabLR = new TabPage();

        this.SuspendLayout();

        // tabMetrics
        this.tabMetrics.Dock = DockStyle.Fill;
        this.tabMetrics.Controls.Add(this.tabLoss);
        this.tabMetrics.Controls.Add(this.tabMap);
        this.tabMetrics.Controls.Add(this.tabLR);

        // tabLoss
        this.tabLoss.Text = "Loss";
        this.tabLoss.Padding = new Padding(4);
        this.tabLoss.UseVisualStyleBackColor = true;

        // tabMap
        this.tabMap.Text = "mAP";
        this.tabMap.Padding = new Padding(4);
        this.tabMap.UseVisualStyleBackColor = true;

        // tabLR
        this.tabLR.Text = "Learning Rate";
        this.tabLR.Padding = new Padding(4);
        this.tabLR.UseVisualStyleBackColor = true;

        // MetricsChart
        this.Controls.Add(this.tabMetrics);
        this.Name = "MetricsChart";
        this.Size = new Size(600, 300);

        this.ResumeLayout(false);
    }

    #endregion

    private TabControl tabMetrics;
    private TabPage tabLoss;
    private TabPage tabMap;
    private TabPage tabLR;
}
