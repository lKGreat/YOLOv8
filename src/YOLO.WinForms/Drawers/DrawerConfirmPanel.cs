using AntdUI;

namespace YOLO.WinForms.Drawers;

/// <summary>
/// Drawer panel for confirmation dialogs with a message and OK/Cancel buttons.
/// </summary>
public class DrawerConfirmPanel : UserControl
{
    private readonly System.Windows.Forms.Label lblMessage;
    private readonly AntdUI.Button btnConfirm;
    private readonly AntdUI.Button btnCancel;

    public bool IsConfirmed { get; private set; }

    public DrawerConfirmPanel(string message, string title = "确认")
    {
        // Initialize controls
        var lblTitle = new AntdUI.Label
        {
            Text = title,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 16)
        };

        lblMessage = new System.Windows.Forms.Label
        {
            Text = message,
            Font = new Font("Segoe UI", 11F),
            AutoSize = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 24)
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
        Controls.Add(lblMessage);
        Controls.Add(lblTitle);

        BackColor = Color.Transparent;

        // Event handlers
        btnConfirm.Click += BtnConfirm_Click;
        btnCancel.Click += BtnCancel_Click;
    }

    private void BtnConfirm_Click(object? sender, EventArgs e)
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
        // Set button positions after parent size is known
        if (Parent != null)
        {
            btnConfirm.Location = new Point(Width - 220, 12);
            btnCancel.Location = new Point(Width - 110, 12);
        }
    }
}
