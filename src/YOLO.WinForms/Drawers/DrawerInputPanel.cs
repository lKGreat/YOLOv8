using AntdUI;

namespace YOLO.WinForms.Drawers;

/// <summary>
/// Drawer panel for user input with a label, text input, and OK/Cancel buttons.
/// </summary>
public partial class DrawerInputPanel : UserControl
{
    private readonly AntdUI.Label lblTitle;
    private readonly AntdUI.Input txtInput;
    private readonly AntdUI.Button btnConfirm;
    private readonly AntdUI.Button btnCancel;

    public string InputValue { get; private set; } = "";
    public bool IsConfirmed { get; private set; }

    public DrawerInputPanel(string title, string placeholder = "")
    {
        // Initialize controls
        lblTitle = new AntdUI.Label
        {
            Text = title,
            Font = new Font("Segoe UI", 12F, FontStyle.Regular),
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 16)
        };

        txtInput = new AntdUI.Input
        {
            PlaceholderText = placeholder,
            Dock = DockStyle.Top,
            Height = 40
        };

        var panelButtons = new System.Windows.Forms.Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Padding = new Padding(0, 16, 0, 0)
        };

        btnCancel = new AntdUI.Button
        {
            Text = "取消",
            Width = 100,
            Height = 36,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };
        btnCancel.Location = new Point(panelButtons.Width - 220, 12);

        btnConfirm = new AntdUI.Button
        {
            Text = "确定",
            Type = AntdUI.TTypeMini.Primary,
            Width = 100,
            Height = 36,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };
        btnConfirm.Location = new Point(panelButtons.Width - 110, 12);

        panelButtons.Controls.Add(btnCancel);
        panelButtons.Controls.Add(btnConfirm);

        // Set up layout
        Controls.Add(panelButtons);
        Controls.Add(txtInput);
        Controls.Add(lblTitle);

        BackColor = Color.Transparent;

        // Event handlers
        btnConfirm.Click += BtnConfirm_Click;
        btnCancel.Click += BtnCancel_Click;
    }

    private void BtnConfirm_Click(object? sender, EventArgs e)
    {
        InputValue = txtInput.Text;
        IsConfirmed = true;
        // Dispose will trigger Drawer to close
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
        // Set button positions after parent size is known
        if (Parent != null)
        {
            btnConfirm.Location = new Point(Width - 220, 12);
            btnCancel.Location = new Point(Width - 110, 12);
        }
        txtInput.Focus();
    }
}
