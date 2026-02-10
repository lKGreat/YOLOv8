using TorchSharp;
using YOLO.Core.Abstractions;
using YOLO.WinForms.Panels;

namespace YOLO.WinForms;

/// <summary>
/// Main application window with tabbed interface for Annotation, Training, Export, and Inference.
/// Uses AntdUI.Window for modern borderless appearance.
/// </summary>
public partial class MainForm : AntdUI.Window
{
    private AnnotationPanel? annotationPanel;
    private TrainingPanel? trainingPanel;
    private ExportPanel? exportPanel;
    private InferencePanel? inferencePanel;
    private ModelTestPanel? modelTestPanel;

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
        annotationPanel.DatasetReadyForTraining += SwitchToTrainingWithDataset;
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

        // Model Test Panel
        modelTestPanel = new ModelTestPanel();
        modelTestPanel.Dock = DockStyle.Fill;
        modelTestPanel.StatusChanged += (s, msg) => SetStatus(msg);
        tabModelTest.Controls.Add(modelTestPanel);
    }

    /// <summary>
    /// Switch to training tab and pre-fill the dataset YAML path.
    /// Called after annotation dataset generation.
    /// </summary>
    public void SwitchToTrainingWithDataset(string yamlPath)
    {
        if (InvokeRequired)
        {
            Invoke(() => SwitchToTrainingWithDataset(yamlPath));
            return;
        }

        tabs.SelectedIndex = 1; // Training tab
        trainingPanel?.SetDatasetPath(yamlPath);
    }

    private void UpdateDeviceStatus()
    {
        bool cuda = torch.cuda.is_available();
        lblDevice.Text = cuda
            ? $"CUDA ({torch.cuda.device_count()} GPU)"
            : "CPU";

        var versions = ModelRegistry.GetVersions();
        SetStatus($"Ready | Models: {string.Join(", ", versions)}");
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        DraggableMouseDown();
        base.OnMouseDown(e);
    }
}
