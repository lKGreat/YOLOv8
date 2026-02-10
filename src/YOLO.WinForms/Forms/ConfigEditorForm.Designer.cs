namespace YOLO.WinForms.Forms;

partial class ConfigEditorForm
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
        this.txtEditor = new RichTextBox();
        this.panelBottom = new System.Windows.Forms.Panel();
        this.btnSave = new AntdUI.Button();
        this.btnCancel = new AntdUI.Button();

        this.panelBottom.SuspendLayout();
        this.SuspendLayout();

        // txtEditor
        this.txtEditor.AcceptsTab = true;
        this.txtEditor.BackColor = Color.FromArgb(30, 30, 30);
        this.txtEditor.Dock = DockStyle.Fill;
        this.txtEditor.Font = new Font("Cascadia Code", 11F, FontStyle.Regular);
        this.txtEditor.ForeColor = Color.FromArgb(220, 220, 220);
        this.txtEditor.Location = new Point(0, 0);
        this.txtEditor.Name = "txtEditor";
        this.txtEditor.Size = new Size(600, 380);
        this.txtEditor.TabIndex = 0;
        this.txtEditor.Text = "";
        this.txtEditor.WordWrap = false;
        this.txtEditor.ScrollBars = RichTextBoxScrollBars.Both;

        // panelBottom
        this.panelBottom.Controls.Add(this.btnCancel);
        this.panelBottom.Controls.Add(this.btnSave);
        this.panelBottom.Dock = DockStyle.Bottom;
        this.panelBottom.Location = new Point(0, 380);
        this.panelBottom.Name = "panelBottom";
        this.panelBottom.Padding = new Padding(8);
        this.panelBottom.Size = new Size(600, 54);
        this.panelBottom.TabIndex = 1;

        // btnSave
        this.btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnSave.Type = AntdUI.TTypeMini.Primary;
        this.btnSave.Location = new Point(395, 10);
        this.btnSave.Name = "btnSave";
        this.btnSave.Size = new Size(90, 34);
        this.btnSave.TabIndex = 0;
        this.btnSave.Text = "Save";
        this.btnSave.Click += BtnSave_Click;

        // btnCancel
        this.btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnCancel.Location = new Point(495, 10);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new Size(90, 34);
        this.btnCancel.TabIndex = 1;
        this.btnCancel.Text = "Cancel";
        this.btnCancel.Click += BtnCancel_Click;

        // ConfigEditorForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(600, 434);
        this.Controls.Add(this.txtEditor);
        this.Controls.Add(this.panelBottom);
        this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
        this.MinimumSize = new Size(400, 300);
        this.Name = "ConfigEditorForm";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Edit Dataset Configuration";

        this.panelBottom.ResumeLayout(false);
        this.ResumeLayout(false);
    }

    #endregion

    private RichTextBox txtEditor;
    private System.Windows.Forms.Panel panelBottom;
    private AntdUI.Button btnSave;
    private AntdUI.Button btnCancel;
}
