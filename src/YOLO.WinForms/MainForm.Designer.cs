namespace YOLO.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.tabControl = new TabControl();
        this.tabTraining = new TabPage();
        this.tabExport = new TabPage();
        this.tabInference = new TabPage();
        this.statusStrip = new StatusStrip();
        this.lblStatus = new ToolStripStatusLabel();
        this.lblDevice = new ToolStripStatusLabel();

        // tabControl
        this.tabControl.Controls.Add(this.tabTraining);
        this.tabControl.Controls.Add(this.tabExport);
        this.tabControl.Controls.Add(this.tabInference);
        this.tabControl.Dock = DockStyle.Fill;
        this.tabControl.Font = new Font("Segoe UI", 10F);
        this.tabControl.Location = new Point(0, 0);
        this.tabControl.Name = "tabControl";
        this.tabControl.Padding = new Point(12, 6);
        this.tabControl.SelectedIndex = 0;
        this.tabControl.Size = new Size(1280, 750);
        this.tabControl.TabIndex = 0;

        // tabTraining
        this.tabTraining.Location = new Point(4, 34);
        this.tabTraining.Name = "tabTraining";
        this.tabTraining.Padding = new Padding(8);
        this.tabTraining.Size = new Size(1272, 712);
        this.tabTraining.TabIndex = 0;
        this.tabTraining.Text = "Training";
        this.tabTraining.UseVisualStyleBackColor = true;

        // tabExport
        this.tabExport.Location = new Point(4, 34);
        this.tabExport.Name = "tabExport";
        this.tabExport.Padding = new Padding(8);
        this.tabExport.Size = new Size(1272, 712);
        this.tabExport.TabIndex = 1;
        this.tabExport.Text = "Export";
        this.tabExport.UseVisualStyleBackColor = true;

        // tabInference
        this.tabInference.Location = new Point(4, 34);
        this.tabInference.Name = "tabInference";
        this.tabInference.Padding = new Padding(8);
        this.tabInference.Size = new Size(1272, 712);
        this.tabInference.TabIndex = 2;
        this.tabInference.Text = "Inference";
        this.tabInference.UseVisualStyleBackColor = true;

        // statusStrip
        this.statusStrip.Items.AddRange(new ToolStripItem[] {
            this.lblStatus,
            this.lblDevice
        });
        this.statusStrip.Location = new Point(0, 750);
        this.statusStrip.Name = "statusStrip";
        this.statusStrip.Size = new Size(1280, 22);
        this.statusStrip.TabIndex = 1;

        // lblStatus
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new Size(100, 17);
        this.lblStatus.Text = "Ready";
        this.lblStatus.Spring = true;
        this.lblStatus.TextAlign = ContentAlignment.MiddleLeft;

        // lblDevice
        this.lblDevice.Name = "lblDevice";
        this.lblDevice.Size = new Size(150, 17);
        this.lblDevice.TextAlign = ContentAlignment.MiddleRight;

        // MainForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1280, 772);
        this.Controls.Add(this.tabControl);
        this.Controls.Add(this.statusStrip);
        this.MinimumSize = new Size(960, 600);
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "YOLO Training Tool";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private TabControl tabControl;
    private TabPage tabTraining;
    private TabPage tabExport;
    private TabPage tabInference;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel lblStatus;
    private ToolStripStatusLabel lblDevice;
}
