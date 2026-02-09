using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using YOLO.Runtime.Internal.Pipeline;
using YOLO.Runtime.Results;

namespace YOLO.Runtime;

/// <summary>
/// Fluent builder for multi-model chain inference.
/// Chain multiple YOLO models (e.g. detect -> classify) in sequence.
/// <para>
/// <b>Usage:</b>
/// <code>
/// var chain = YoloChain.Create()
///     .Then("yolov8n.onnx")
///     .Then("yolov8n-cls.onnx", new YoloOptions { ImgSize = 224 })
///     .Build();
/// var results = chain.Run("image.jpg");
/// </code>
/// </para>
/// </summary>
public sealed class YoloChain : IDisposable
{
    private readonly List<ChainStep> _steps;
    private bool _disposed;

    private YoloChain(List<ChainStep> steps)
    {
        _steps = steps;
    }

    /// <summary>
    /// Start building a new chain.
    /// </summary>
    public static YoloChainBuilder Create() => new();

    /// <summary>
    /// Run the chain on a single image file.
    /// </summary>
    /// <param name="imagePath">Path to the input image.</param>
    /// <returns>Array of results, one per chain step.</returns>
    public InferenceResult[] Run(string imagePath)
    {
        return RunAsync(imagePath).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Run the chain asynchronously on a single image file.
    /// </summary>
    public async Task<InferenceResult[]> RunAsync(string imagePath, CancellationToken ct = default)
    {
        var results = new InferenceResult[_steps.Count];
        using var originalImage = Image.Load<Rgb24>(imagePath);

        // Step 0: always runs on the original image
        var step0 = _steps[0];
        var detections = await step0.Pipeline.DetectAsync(imagePath, ct);

        results[0] = new InferenceResult(step0.Name)
        {
            Detections = detections
        };

        // Subsequent steps: process ROIs from the previous detection step
        for (int i = 1; i < _steps.Count; i++)
        {
            var step = _steps[i];

            if (detections.Length == 0)
            {
                // No detections to pass forward
                results[i] = new InferenceResult(step.Name)
                {
                    Classifications = [],
                    Detections = []
                };
                continue;
            }

            // Crop ROIs from original image and run through next model
            var classifications = new List<ClassificationResult>();
            var stepDetections = new List<DetectionResult>();

            foreach (var det in detections)
            {
                ct.ThrowIfCancellationRequested();

                // Crop the ROI from the original image
                int x = Math.Max(0, (int)det.X1);
                int y = Math.Max(0, (int)det.Y1);
                int w = Math.Min((int)det.Width, originalImage.Width - x);
                int h = Math.Min((int)det.Height, originalImage.Height - y);

                if (w <= 0 || h <= 0) continue;

                using var cropped = originalImage.Clone(ctx =>
                    ctx.Crop(new Rectangle(x, y, w, h)));

                // Run the step's pipeline on the cropped image
                var clsResults = step.Pipeline.Classify(cropped);
                if (clsResults.Length > 0)
                {
                    classifications.AddRange(clsResults);
                }
                else
                {
                    // Fallback to detection
                    var detResults = step.Pipeline.Detect(cropped);
                    stepDetections.AddRange(detResults);
                }
            }

            results[i] = new InferenceResult(step.Name)
            {
                Classifications = classifications.Count > 0 ? classifications.ToArray() : null,
                Detections = stepDetections.Count > 0 ? stepDetections.ToArray() : null
            };
        }

        return results;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var step in _steps)
            step.Pipeline.Dispose();
    }

    private sealed record ChainStep(string Name, SingleModelPipeline Pipeline);

    /// <summary>
    /// Builder for constructing a <see cref="YoloChain"/>.
    /// </summary>
    public sealed class YoloChainBuilder
    {
        private readonly List<(string modelPath, YoloOptions? options)> _steps = [];

        internal YoloChainBuilder() { }

        /// <summary>
        /// Add a model step to the chain.
        /// </summary>
        /// <param name="modelPath">Path to the model file (.onnx or .pt).</param>
        /// <param name="options">Optional per-step configuration.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public YoloChainBuilder Then(string modelPath, YoloOptions? options = null)
        {
            _steps.Add((modelPath, options));
            return this;
        }

        /// <summary>
        /// Build the chain. All models are loaded at this point.
        /// </summary>
        /// <returns>A ready-to-use <see cref="YoloChain"/>.</returns>
        public YoloChain Build()
        {
            if (_steps.Count == 0)
                throw new InvalidOperationException("Chain must have at least one model step. Call .Then() first.");

            var steps = new List<ChainStep>();
            foreach (var (modelPath, options) in _steps)
            {
                var opts = options ?? new YoloOptions();
                var name = Path.GetFileNameWithoutExtension(modelPath);
                var pipeline = new SingleModelPipeline(modelPath, opts);
                steps.Add(new ChainStep(name, pipeline));
            }

            return new YoloChain(steps);
        }
    }
}
