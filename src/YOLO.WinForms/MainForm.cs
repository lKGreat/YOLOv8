using TorchSharp;
using YOLO.Core.Abstractions;
using YOLO.WinForms.Panels;

namespace YOLO.WinForms;

/// <summary>
/// Main application form with tabbed interface for Training, Export, and Inference.
/// </summary>
public partial class MainForm : Form
{
    private AnnotationPanel? annotationPanel;
    private TrainingPanel? trainingPanel;
    private ExportPanel? exportPanel;
    private InferencePanel? inferencePanel;

    public MainForm()
    {
        InitializeComponent();
        InitializePanels();
        UpdateDeviceStatus();
    }

    private void InitializePanels()
    {
        // Annotation Panel
        annotationPanel = new AnnotationPanel();
        annotationPanel.Dock = DockStyle.Fill;
        annotationPanel.StatusChanged += (s, msg) => SetStatus(msg);
        tabAnnotation.Controls.Add(annotationPanel);

        // Training Panel
        trainingPanel = new TrainingPanel();
        trainingPanel.Dock = DockStyle.Fill;
        trainingPanel.StatusChanged += (s, msg) => SetStatus(msg);
        tabTraining.Controls.Add(trainingPanel);

        // Export Panel
        exportPanel = new ExportPanel();
        exportPanel.Dock = DockStyle.Fill;
        exportPanel.StatusChanged += (s, msg) => SetStatus(msg);
        tabExport.Controls.Add(exportPanel);

        // Inference Panel
        inferencePanel = new InferencePanel();
        inferencePanel.Dock = DockStyle.Fill;
        inferencePanel.StatusChanged += (s, msg) => SetStatus(msg);
        tabInference.Controls.Add(inferencePanel);
    }

    private void UpdateDeviceStatus()
    {
        bool cuda = torch.cuda.is_available();
        lblDevice.Text = cuda
            ? $"CUDA ({torch.cuda.device_count()} GPU)"
            : "CPU";

        var versions = ModelRegistry.GetVersions();
        SetStatus($"Ready | Registered models: {string.Join(", ", versions)}");
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetStatus(message));
            return;
        }
        lblStatus.Text = message;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        trainingPanel?.StopTraining();
        base.OnFormClosing(e);
    }
}
