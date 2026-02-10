using System.ComponentModel;

namespace YOLO.WinForms.Forms;

/// <summary>
/// Dialog for editing YAML dataset configuration files.
/// </summary>
public partial class ConfigEditorForm : Form
{
    /// <summary>
    /// The edited YAML text. Read after DialogResult.OK.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string YamlText
    {
        get => txtEditor.Text;
        set => txtEditor.Text = value;
    }

    public ConfigEditorForm()
    {
        InitializeComponent();
    }

    public ConfigEditorForm(string yamlText, string title = "Edit Dataset Configuration")
        : this()
    {
        txtEditor.Text = yamlText;
        Text = title;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
