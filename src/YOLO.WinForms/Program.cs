using YOLO.Core.Abstractions;
using YOLO.Core.Models;
using YOLO.Training;

namespace YOLO.WinForms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // Initialize model registry
        YOLOv8Model.EnsureRegistered();
        Trainer.RegisterLossFactories();

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
