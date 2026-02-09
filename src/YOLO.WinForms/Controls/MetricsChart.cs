using ScottPlot;
using ScottPlot.WinForms;

namespace YOLO.WinForms.Controls;

/// <summary>
/// Custom control for displaying real-time training metrics charts.
/// Uses ScottPlot for loss curves, mAP curves, and learning rate schedules.
/// </summary>
public partial class MetricsChart : UserControl
{
    private FormsPlot plotLoss = null!;
    private FormsPlot plotMap = null!;
    private FormsPlot plotLR = null!;

    // Data storage
    private readonly List<double> epochs = [];
    private readonly List<double> boxLoss = [];
    private readonly List<double> clsLoss = [];
    private readonly List<double> dflLoss = [];
    private readonly List<double> totalLoss = [];
    private readonly List<double> map50 = [];
    private readonly List<double> map5095 = [];
    private readonly List<double> lrValues = [];

    public MetricsChart()
    {
        InitializeComponent();
        InitializePlots();
    }

    private void InitializePlots()
    {
        // Loss Plot
        plotLoss = new FormsPlot { Dock = DockStyle.Fill };
        plotLoss.Plot.Title("Training Loss");
        plotLoss.Plot.XLabel("Epoch");
        plotLoss.Plot.YLabel("Loss");
        tabLoss.Controls.Add(plotLoss);

        // mAP Plot
        plotMap = new FormsPlot { Dock = DockStyle.Fill };
        plotMap.Plot.Title("Validation mAP");
        plotMap.Plot.XLabel("Epoch");
        plotMap.Plot.YLabel("mAP");
        tabMap.Controls.Add(plotMap);

        // LR Plot
        plotLR = new FormsPlot { Dock = DockStyle.Fill };
        plotLR.Plot.Title("Learning Rate");
        plotLR.Plot.XLabel("Epoch");
        plotLR.Plot.YLabel("LR");
        tabLR.Controls.Add(plotLR);
    }

    /// <summary>
    /// Add a data point for one epoch of training metrics.
    /// </summary>
    public void AddEpoch(int epoch, double boxL, double clsL, double dflL,
        double mAP50, double mAP5095, double lr)
    {
        if (InvokeRequired)
        {
            Invoke(() => AddEpoch(epoch, boxL, clsL, dflL, mAP50, mAP5095, lr));
            return;
        }

        epochs.Add(epoch);
        boxLoss.Add(boxL);
        clsLoss.Add(clsL);
        dflLoss.Add(dflL);
        totalLoss.Add(boxL + clsL + dflL);
        map50.Add(mAP50);
        map5095.Add(mAP5095);
        lrValues.Add(lr);

        UpdateLossChart();
        UpdateMapChart();
        UpdateLRChart();
    }

    /// <summary>
    /// Clear all chart data.
    /// </summary>
    public void Clear()
    {
        if (InvokeRequired)
        {
            Invoke(Clear);
            return;
        }

        epochs.Clear();
        boxLoss.Clear();
        clsLoss.Clear();
        dflLoss.Clear();
        totalLoss.Clear();
        map50.Clear();
        map5095.Clear();
        lrValues.Clear();

        plotLoss.Plot.Clear();
        plotMap.Plot.Clear();
        plotLR.Plot.Clear();
        plotLoss.Refresh();
        plotMap.Refresh();
        plotLR.Refresh();
    }

    private void UpdateLossChart()
    {
        plotLoss.Plot.Clear();
        var xs = epochs.ToArray();

        if (xs.Length > 0)
        {
            var sigBox = plotLoss.Plot.Add.Scatter(xs, boxLoss.ToArray());
            sigBox.LegendText = "Box";
            sigBox.LineWidth = 2;

            var sigCls = plotLoss.Plot.Add.Scatter(xs, clsLoss.ToArray());
            sigCls.LegendText = "Cls";
            sigCls.LineWidth = 2;

            var sigDfl = plotLoss.Plot.Add.Scatter(xs, dflLoss.ToArray());
            sigDfl.LegendText = "DFL";
            sigDfl.LineWidth = 2;

            var sigTotal = plotLoss.Plot.Add.Scatter(xs, totalLoss.ToArray());
            sigTotal.LegendText = "Total";
            sigTotal.LineWidth = 2.5f;
            sigTotal.LineStyle.Pattern = ScottPlot.LinePattern.Dashed;

            plotLoss.Plot.ShowLegend();
        }

        plotLoss.Refresh();
    }

    private void UpdateMapChart()
    {
        plotMap.Plot.Clear();
        var xs = epochs.ToArray();

        if (xs.Length > 0)
        {
            var sig50 = plotMap.Plot.Add.Scatter(xs, map50.ToArray());
            sig50.LegendText = "mAP@50";
            sig50.LineWidth = 2;

            var sig5095 = plotMap.Plot.Add.Scatter(xs, map5095.ToArray());
            sig5095.LegendText = "mAP@50:95";
            sig5095.LineWidth = 2;

            plotMap.Plot.ShowLegend();
        }

        plotMap.Refresh();
    }

    private void UpdateLRChart()
    {
        plotLR.Plot.Clear();
        var xs = epochs.ToArray();

        if (xs.Length > 0)
        {
            var sig = plotLR.Plot.Add.Scatter(xs, lrValues.ToArray());
            sig.LegendText = "LR";
            sig.LineWidth = 2;
        }

        plotLR.Refresh();
    }
}
