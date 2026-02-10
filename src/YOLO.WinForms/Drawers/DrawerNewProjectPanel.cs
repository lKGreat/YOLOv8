using AntdUI;

namespace YOLO.WinForms.Drawers;

/// <summary>
/// Drawer panel for creating a new annotation project.
/// Integrates project name input and folder selection into a single drawer.
/// </summary>
public class DrawerNewProjectPanel : UserControl
{
    private readonly AntdUI.Input txtProjectName;
    private readonly System.Windows.Forms.Label lblFolderPath;
    private readonly AntdUI.Button btnBrowseFolder;
    private readonly AntdUI.Button btnCreate;
    private readonly AntdUI.Button btnCancel;

    public string ProjectName => txtProjectName.Text;
    public string FolderPath { get; private set; } = "";
    public bool IsConfirmed { get; private set; }

    public DrawerNewProjectPanel()
    {
        // Initialize controls
        var lblTitle = new AntdUI.Label
        {
            Text = "新建标注项目",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 16)
        };

        var lblProjectName = new AntdUI.Label
        {
            Text = "项目名称",
            Font = new Font("Segoe UI", 10F),
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 8)
        };

        txtProjectName = new AntdUI.Input
        {
            PlaceholderText = "输入项目名称...",
            Dock = DockStyle.Top,
            Height = 40,
            Margin = new Padding(0, 0, 0, 16)
        };

        var lblFolderTitle = new AntdUI.Label
        {
            Text = "项目位置",
            Font = new Font("Segoe UI", 10F),
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 8)
        };

        var panelFolderSelection = new System.Windows.Forms.Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            Margin = new Padding(0, 0, 0, 16)
        };

        lblFolderPath = new System.Windows.Forms.Label
        {
            Text = "未选择文件夹",
            Font = new Font("Segoe UI", 10F),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Gray,
            Location = new Point(0, 7),
            Height = 36
        };

        btnBrowseFolder = new AntdUI.Button
        {
            Text = "浏览...",
            Width = 100,
            Height = 36,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };

        panelFolderSelection.Controls.Add(btnBrowseFolder);
        panelFolderSelection.Controls.Add(lblFolderPath);

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

        btnCreate = new AntdUI.Button
        {
            Text = "创建",
            Type = AntdUI.TTypeMini.Primary,
            Width = 110,
            Height = 36,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };

        panelButtons.Controls.Add(btnCancel);
        panelButtons.Controls.Add(btnCreate);

        // Set up layout
        Controls.Add(panelButtons);
        Controls.Add(panelFolderSelection);
        Controls.Add(lblFolderTitle);
        Controls.Add(txtProjectName);
        Controls.Add(lblProjectName);
        Controls.Add(lblTitle);

        BackColor = Color.Transparent;

        // Event handlers
        btnBrowseFolder.Click += BtnBrowseFolder_Click;
        btnCreate.Click += BtnCreate_Click;
        btnCancel.Click += BtnCancel_Click;
    }

    private void BtnBrowseFolder_Click(object? sender, EventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择项目文件夹",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            FolderPath = dlg.SelectedPath;
            lblFolderPath.Text = FolderPath;
            lblFolderPath.ForeColor = Color.FromArgb(220, 220, 220);
        }
    }

    private void BtnCreate_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            var form = FindForm();
            if (form != null)
                AntdUI.Message.warn(form, "请输入项目名称", SystemFonts.DefaultFont);
            return;
        }

        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            var form = FindForm();
            if (form != null)
                AntdUI.Message.warn(form, "请选择项目位置", SystemFonts.DefaultFont);
            return;
        }

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
        txtProjectName.Focus();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutButtons();
    }

    private void LayoutButtons()
    {
        // Layout bottom buttons (取消 on left, 创建 on right)
        int rightMargin = 24;
        int buttonSpacing = 12;
        
        btnCreate.Location = new Point(Width - rightMargin - btnCreate.Width, 12);
        btnCancel.Location = new Point(btnCreate.Left - buttonSpacing - btnCancel.Width, 12);
        
        // Layout browse button in folder panel
        var panelFolder = btnBrowseFolder.Parent;
        if (panelFolder != null)
        {
            btnBrowseFolder.Location = new Point(panelFolder.Width - rightMargin - btnBrowseFolder.Width, 7);
            
            // Adjust label width to not overlap with button
            lblFolderPath.Width = btnBrowseFolder.Left - buttonSpacing;
        }
    }
}
