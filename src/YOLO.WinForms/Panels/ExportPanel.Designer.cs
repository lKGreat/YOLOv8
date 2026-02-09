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
        this.lblWeights = new Label();
        this.txtWeights = new TextBox();
        this.btnBrowseWeights = new Button();
        this.lblVersion = new Label();
        this.cboVersion = new ComboBox();
        this.lblVariant = new Label();
        this.cboVariant = new ComboBox();
        this.lblNc = new Label();
        this.numNc = new NumericUpDown();
        this.lblFormat = new Label();
        this.cboFormat = new ComboBox();
        this.lblOutput = new Label();
        this.txtOutput = new TextBox();
        this.btnBrowseOutput = new Button();
        this.lblImgSize = new Label();
        this.numImgSize = new NumericUpDown();
        this.chkHalf = new CheckBox();
        this.chkSimplify = new CheckBox();
        this.chkDynamic = new CheckBox();
        this.btnExport = new Button();
        this.grpLog = new GroupBox();
        this.txtLog = new RichTextBox();

        ((System.ComponentModel.ISupportInitialize)this.numNc).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.numImgSize).BeginInit();
        this.SuspendLayout();

        // tableLayout
        this.tableLayout.ColumnCount = 3;
        this.tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        this.tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));
        this.tableLayout.Dock = DockStyle.Top;
        this.tableLayout.Height = 380;
        this.tableLayout.Padding = new Padding(12);
        this.tableLayout.RowCount = 12;
        for (int i = 0; i < 12; i++)
            this.tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

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
        this.btnBrowseWeights.Size = new Size(28, 26);
        this.tableLayout.Controls.Add(this.btnBrowseWeights, 2, row);

        // Row 1: Version
        row = 1;
        this.lblVersion.Text = "Version:";
        this.lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVersion.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblVersion, 0, row);
        this.cboVersion.Dock = DockStyle.Fill;
        this.cboVersion.DropDownStyle = ComboBoxStyle.DropDownList;
        this.tableLayout.Controls.Add(this.cboVersion, 1, row);
        this.tableLayout.SetColumnSpan(this.cboVersion, 2);

        // Row 2: Variant
        row = 2;
        this.lblVariant.Text = "Variant:";
        this.lblVariant.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVariant.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.lblVariant, 0, row);
        this.cboVariant.Dock = DockStyle.Fill;
        this.cboVariant.DropDownStyle = ComboBoxStyle.DropDownList;
        this.tableLayout.Controls.Add(this.cboVariant, 1, row);
        this.tableLayout.SetColumnSpan(this.cboVariant, 2);

        // Row 3: NumClasses
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
        this.cboFormat.DropDownStyle = ComboBoxStyle.DropDownList;
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
        this.btnBrowseOutput.Size = new Size(28, 26);
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
        this.numImgSize.Increment = 32;
        this.numImgSize.Value = 640;
        this.tableLayout.Controls.Add(this.numImgSize, 1, row);
        this.tableLayout.SetColumnSpan(this.numImgSize, 2);

        // Row 7-9: Checkboxes
        row = 7;
        this.chkHalf.Text = "FP16 Half Precision";
        this.chkHalf.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.chkHalf, 0, row);
        this.tableLayout.SetColumnSpan(this.chkHalf, 3);

        row = 8;
        this.chkSimplify.Text = "Simplify ONNX";
        this.chkSimplify.Checked = true;
        this.chkSimplify.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.chkSimplify, 0, row);
        this.tableLayout.SetColumnSpan(this.chkSimplify, 3);

        row = 9;
        this.chkDynamic.Text = "Dynamic Batch Size";
        this.chkDynamic.Dock = DockStyle.Fill;
        this.tableLayout.Controls.Add(this.chkDynamic, 0, row);
        this.tableLayout.SetColumnSpan(this.chkDynamic, 3);

        // Row 10: Export button
        row = 10;
        this.btnExport.Text = "Export Model";
        this.btnExport.Size = new Size(140, 34);
        this.btnExport.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.btnExport.BackColor = Color.FromArgb(0, 120, 212);
        this.btnExport.ForeColor = Color.White;
        this.btnExport.FlatStyle = FlatStyle.Flat;
        this.btnExport.FlatAppearance.BorderSize = 0;
        this.btnExport.Cursor = Cursors.Hand;
        this.tableLayout.Controls.Add(this.btnExport, 1, row);

        // grpLog
        this.grpLog.Dock = DockStyle.Fill;
        this.grpLog.Text = "Export Log";
        this.grpLog.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpLog.Padding = new Padding(8);
        this.grpLog.Controls.Add(this.txtLog);

        // txtLog
        this.txtLog.Dock = DockStyle.Fill;
        this.txtLog.Font = new Font("Cascadia Code", 9F);
        this.txtLog.ReadOnly = true;
        this.txtLog.BackColor = Color.FromArgb(30, 30, 30);
        this.txtLog.ForeColor = Color.FromArgb(220, 220, 220);
        this.txtLog.BorderStyle = BorderStyle.None;

        // ExportPanel
        this.Controls.Add(this.grpLog);
        this.Controls.Add(this.tableLayout);
        this.Name = "ExportPanel";
        this.Size = new Size(1200, 700);

        ((System.ComponentModel.ISupportInitialize)this.numNc).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.numImgSize).EndInit();
        this.ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel tableLayout;
    private Label lblWeights;
    private TextBox txtWeights;
    private Button btnBrowseWeights;
    private Label lblVersion;
    private ComboBox cboVersion;
    private Label lblVariant;
    private ComboBox cboVariant;
    private Label lblNc;
    private NumericUpDown numNc;
    private Label lblFormat;
    private ComboBox cboFormat;
    private Label lblOutput;
    private TextBox txtOutput;
    private Button btnBrowseOutput;
    private Label lblImgSize;
    private NumericUpDown numImgSize;
    private CheckBox chkHalf;
    private CheckBox chkSimplify;
    private CheckBox chkDynamic;
    private Button btnExport;
    private GroupBox grpLog;
    private RichTextBox txtLog;
}
