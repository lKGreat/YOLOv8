namespace YOLO.WinForms.Panels;

partial class ExportPanel
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

        this.tableLayout = new TableLayoutPanel();
        this.lblWeights = new AntdUI.Label();
        this.txtWeights = new AntdUI.Input();
        this.btnBrowseWeights = new AntdUI.Button();
        this.lblVersion = new AntdUI.Label();
        this.cboVersion = new AntdUI.Select();
        this.lblVariant = new AntdUI.Label();
        this.cboVariant = new AntdUI.Select();
        this.lblNc = new AntdUI.Label();
        this.numNc = new AntdUI.InputNumber();
        this.lblFormat = new AntdUI.Label();
        this.cboFormat = new AntdUI.Select();
        this.lblOutput = new AntdUI.Label();
        this.txtOutput = new AntdUI.Input();
        this.btnBrowseOutput = new AntdUI.Button();
        this.lblImgSize = new AntdUI.Label();
        this.numImgSize = new AntdUI.InputNumber();
        this.chkHalf = new AntdUI.Switch();
        this.lblHalf = new AntdUI.Label();
        this.chkSimplify = new AntdUI.Switch();
        this.lblSimplify = new AntdUI.Label();
        this.chkDynamic = new AntdUI.Switch();
        this.lblDynamic = new AntdUI.Label();
        this.btnExport = new AntdUI.Button();
        this.panelLogHeader = new AntdUI.Label();
        this.grpLog = new System.Windows.Forms.Panel();
        this.txtLog = new RichTextBox();

        this.SuspendLayout();

        // ═══════════════════════════════════════════════════════
        // tableLayout (top config section)
        // ═══════════════════════════════════════════════════════
        this.tableLayout.ColumnCount = 3;
        this.tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        this.tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
        this.tableLayout.Dock = DockStyle.Top;
        this.tableLayout.Height = 430;
        this.tableLayout.Padding = new Padding(12);
        this.tableLayout.RowCount = 12;
        for (int i = 0; i < 12; i++)
            this.tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

        int row = 0;

        // Row 0: Weights
        this.lblWeights.Text = "Weights:";
        this.lblWeights.TextAlign = ContentAlignment.MiddleLeft;
        this.lblWeights.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblWeights, 0, row);
        this.txtWeights.Dock = DockStyle.Fill;
        this.txtWeights.PlaceholderText = "runs/train/exp/weights/best.pt";
        this.tableLayout.Controls.Add(this.txtWeights, 1, row);
        this.btnBrowseWeights.Text = "...";
        this.btnBrowseWeights.Ghost = true;
        this.btnBrowseWeights.Size = new Size(32, 30);
        this.tableLayout.Controls.Add(this.btnBrowseWeights, 2, row);

        // Row 1: Version
        row = 1;
        this.lblVersion.Text = "Version:";
        this.lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVersion.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblVersion, 0, row);
        this.cboVersion.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.cboVersion, 1, row);
        this.tableLayout.SetColumnSpan(this.cboVersion, 2);

        // Row 2: Variant
        row = 2;
        this.lblVariant.Text = "Variant:";
        this.lblVariant.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVariant.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblVariant, 0, row);
        this.cboVariant.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.cboVariant, 1, row);
        this.tableLayout.SetColumnSpan(this.cboVariant, 2);

        // Row 3: Classes
        row = 3;
        this.lblNc.Text = "Classes:";
        this.lblNc.TextAlign = ContentAlignment.MiddleLeft;
        this.lblNc.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblNc, 0, row);
        this.numNc.Dock = DockStyle.Fill;
        this.numNc.Minimum = 1;
        this.numNc.Maximum = 10000;
        this.numNc.Value = 80;
        this.tableLayout.Controls.Add(this.numNc, 1, row);
        this.tableLayout.SetColumnSpan(this.numNc, 2);

        // Row 4: Format
        row = 4;
        this.lblFormat.Text = "Format:";
        this.lblFormat.TextAlign = ContentAlignment.MiddleLeft;
        this.lblFormat.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblFormat, 0, row);
        this.cboFormat.Dock = DockStyle.Fill;
        this.cboFormat.Items.AddRange(new object[] { "ONNX", "TorchScript" });
        this.cboFormat.SelectedIndex = 0;
        this.tableLayout.Controls.Add(this.cboFormat, 1, row);
        this.tableLayout.SetColumnSpan(this.cboFormat, 2);

        // Row 5: Output
        row = 5;
        this.lblOutput.Text = "Output:";
        this.lblOutput.TextAlign = ContentAlignment.MiddleLeft;
        this.lblOutput.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblOutput, 0, row);
        this.txtOutput.Dock = DockStyle.Fill;
        this.txtOutput.PlaceholderText = "model.onnx";
        this.tableLayout.Controls.Add(this.txtOutput, 1, row);
        this.btnBrowseOutput.Text = "...";
        this.btnBrowseOutput.Ghost = true;
        this.btnBrowseOutput.Size = new Size(32, 30);
        this.tableLayout.Controls.Add(this.btnBrowseOutput, 2, row);

        // Row 6: ImgSize
        row = 6;
        this.lblImgSize.Text = "ImgSize:";
        this.lblImgSize.TextAlign = ContentAlignment.MiddleLeft;
        this.lblImgSize.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblImgSize, 0, row);
        this.numImgSize.Dock = DockStyle.Fill;
        this.numImgSize.Minimum = 32;
        this.numImgSize.Maximum = 2048;
        this.numImgSize.Value = 640;
        this.tableLayout.Controls.Add(this.numImgSize, 1, row);
        this.tableLayout.SetColumnSpan(this.numImgSize, 2);

        // Row 7: FP16
        row = 7;
        this.lblHalf.Text = "FP16:";
        this.lblHalf.TextAlign = ContentAlignment.MiddleLeft;
        this.lblHalf.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblHalf, 0, row);
        this.chkHalf.Dock = DockStyle.Left;
        this.chkHalf.Checked = false;
        this.tableLayout.Controls.Add(this.chkHalf, 1, row);

        // Row 8: Simplify
        row = 8;
        this.lblSimplify.Text = "Simplify:";
        this.lblSimplify.TextAlign = ContentAlignment.MiddleLeft;
        this.lblSimplify.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblSimplify, 0, row);
        this.chkSimplify.Dock = DockStyle.Left;
        this.chkSimplify.Checked = true;
        this.tableLayout.Controls.Add(this.chkSimplify, 1, row);

        // Row 9: Dynamic
        row = 9;
        this.lblDynamic.Text = "Dynamic:";
        this.lblDynamic.TextAlign = ContentAlignment.MiddleLeft;
        this.lblDynamic.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblDynamic, 0, row);
        this.chkDynamic.Dock = DockStyle.Left;
        this.chkDynamic.Checked = false;
        this.tableLayout.Controls.Add(this.chkDynamic, 1, row);

        // Row 10: Export button
        row = 10;
        this.btnExport.Text = "Export Model";
        this.btnExport.Type = AntdUI.TTypeMini.Primary;
        this.btnExport.Size = new Size(140, 36);
        this.tableLayout.Controls.Add(this.btnExport, 1, row);

        // ═══════════════════════════════════════════════════════
        // Log section
        // ═══════════════════════════════════════════════════════
        this.panelLogHeader.Text = "Export Log";
        this.panelLogHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelLogHeader.Dock = DockStyle.Top;
        this.panelLogHeader.Height = 30;
        this.panelLogHeader.Padding = new Padding(8, 6, 0, 0);

        this.grpLog.Dock = DockStyle.Fill;
        this.grpLog.Padding = new Padding(4);

        this.txtLog.Dock = DockStyle.Fill;
        this.txtLog.Font = new Font("Cascadia Code", 9F);
        this.txtLog.ReadOnly = true;
        this.txtLog.BackColor = Color.FromArgb(30, 30, 30);
        this.txtLog.ForeColor = Color.FromArgb(220, 220, 220);
        this.txtLog.BorderStyle = BorderStyle.None;
        this.grpLog.Controls.Add(this.txtLog);

        // ═══════════════════════════════════════════════════════
        // ExportPanel
        // ═══════════════════════════════════════════════════════
        this.Controls.Add(this.grpLog);
        this.Controls.Add(this.panelLogHeader);
        this.Controls.Add(this.tableLayout);
        this.Name = "ExportPanel";
        this.Size = new Size(1200, 700);

        this.ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel tableLayout;
    private AntdUI.Label lblWeights;
    private AntdUI.Input txtWeights;
    private AntdUI.Button btnBrowseWeights;
    private AntdUI.Label lblVersion;
    private AntdUI.Select cboVersion;
    private AntdUI.Label lblVariant;
    private AntdUI.Select cboVariant;
    private AntdUI.Label lblNc;
    private AntdUI.InputNumber numNc;
    private AntdUI.Label lblFormat;
    private AntdUI.Select cboFormat;
    private AntdUI.Label lblOutput;
    private AntdUI.Input txtOutput;
    private AntdUI.Button btnBrowseOutput;
    private AntdUI.Label lblImgSize;
    private AntdUI.InputNumber numImgSize;
    private AntdUI.Switch chkHalf;
    private AntdUI.Label lblHalf;
    private AntdUI.Switch chkSimplify;
    private AntdUI.Label lblSimplify;
    private AntdUI.Switch chkDynamic;
    private AntdUI.Label lblDynamic;
    private AntdUI.Button btnExport;
    private AntdUI.Label panelLogHeader;
    private System.Windows.Forms.Panel grpLog;
    private RichTextBox txtLog;
}
