using AntdUI;
using System.ComponentModel;

namespace YOLO.WinForms.Drawers;

/// <summary>
/// Drawer panel for editing YAML dataset configuration with a RichTextBox editor.
/// </summary>
public class DrawerConfigEditorPanel : UserControl
{
    private readonly RichTextBox txtEditor;
    private readonly AntdUI.Button btnSave;
    private readonly AntdUI.Button btnCancel;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string YamlText
    {
        get => txtEditor.Text;
        set => txtEditor.Text = value;
    }

    public bool IsConfirmed { get; private set; }

    public DrawerConfigEditorPanel(string title = "编辑数据集配置")
    {
        // Initialize title
        var lblTitle = new AntdUI.Label
        {
            Text = title,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 16)
        };

        // Initialize editor
        txtEditor = new RichTextBox
        {
            AcceptsTab = true,
            BackColor = Color.FromArgb(30, 30, 30),
            Dock = DockStyle.Fill,
            Font = new Font("Cascadia Code", 11F, FontStyle.Regular),
            ForeColor = Color.FromArgb(220, 220, 220),
            Text = "",
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both
        };

        // Initialize button panel
        var panelButtons = new System.Windows.Forms.Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Padding = new Padding(0, 16, 0, 0)
        };

        btnCancel = new AntdUI.Button
        {
            Text = "取消",
            Width = 110,
            Height = 36,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };

        btnSave = new AntdUI.Button
        {
            Text = "保存",
            Type = AntdUI.TTypeMini.Primary,
            Width = 110,
            Height = 36,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };

        panelButtons.Controls.Add(btnCancel);
        panelButtons.Controls.Add(btnSave);

        // Set up layout
        Controls.Add(panelButtons);
        Controls.Add(txtEditor);
        Controls.Add(lblTitle);

        BackColor = Color.Transparent;

        // Event handlers
        btnSave.Click += BtnSave_Click;
        btnCancel.Click += BtnCancel_Click;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        IsConfirmed = true;
        Dispose();
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        IsConfirmed = false;
        Dispose();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LayoutButtons();
        txtEditor.Focus();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutButtons();
    }

    private void LayoutButtons()
    {
        int rightMargin = 24;
        int buttonSpacing = 12;
        
        btnSave.Location = new Point(Width - rightMargin - btnSave.Width, 12);
        btnCancel.Location = new Point(btnSave.Left - buttonSpacing - btnCancel.Width, 12);
    }
}
