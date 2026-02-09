using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TorchSharp;
using YOLO.Core.Models;
using YOLO.Inference.PostProcess;
using static TorchSharp.torch;

namespace YOLO.Inference;

/// <summary>
/// Detection result for a single image.
/// </summary>
public record Detection(float X1, float Y1, float X2, float Y2, float Confidence, int ClassId);

/// <summary>
/// YOLOv inference predictor.
/// Handles the full pipeline: preprocess -> model forward -> NMS -> postprocess.
/// </summary>
public class Predictor : IDisposable
{
    private readonly YOLOvModel model;
    private readonly Device device;
    private readonly int imgSize;
    private readonly double confThreshold;
    private readonly double iouThreshold;
    private readonly int maxDet;
    private bool disposed;

    /// <summary>
    /// Create a predictor from a model.
    /// </summary>
    public Predictor(YOLOvModel model, int imgSize = 640,
        double confThreshold = 0.25, double iouThreshold = 0.45,
        int maxDet = 300, Device? device = null)
    {
        this.model = model;
        this.imgSize = imgSize;
        this.confThreshold = confThreshold;
        this.iouThreshold = iouThreshold;
        this.maxDet = maxDet;
        this.device = device ?? (torch.cuda.is_available() ? torch.CUDA : torch.CPU);

        model.eval();
    }

    /// <summary>
    /// Run inference on a single image file.
    /// </summary>
    public List<Detection> Predict(string imagePath)
    {
        using var img = Image.Load<Rgb24>(imagePath);
        int origW = img.Width;
        int origH = img.Height;

        // Preprocess: letterbox
        var (imgTensor, ratio, padX, padY) = Preprocess(img);

        // Forward pass
        List<Detection> detections;
        using (torch.no_grad())
        {
            var input = imgTensor.unsqueeze(0).to(device); // (1, 3, H, W)
            var (boxes, scores, _) = model.forward(input);

            // NMS
            var results = NMS.NonMaxSuppression(boxes, scores,
                confThreshold, iouThreshold, maxDet);

            detections = PostProcess(results[0], origW, origH, ratio, padX, padY);
        }

        return detections;
    }

    /// <summary>
    /// Run inference on a pre-loaded tensor.
    /// </summary>
    public List<List<Detection>> PredictBatch(Tensor images, int[] origWidths, int[] origHeights,
        float[] ratios, float[] padXs, float[] padYs)
    {
        var allDetections = new List<List<Detection>>();

        using (torch.no_grad())
        {
            var input = images.to(device);
            var (boxes, scores, _) = model.forward(input);

            var results = NMS.NonMaxSuppression(boxes, scores,
                confThreshold, iouThreshold, maxDet);

            for (int i = 0; i < results.Count; i++)
            {
                allDetections.Add(PostProcess(results[i],
                    origWidths[i], origHeights[i], ratios[i], padXs[i], padYs[i]));
            }
        }

        return allDetections;
    }

    /// <summary>
    /// Preprocess image: letterbox resize, normalize, convert to tensor.
    /// </summary>
    private (Tensor tensor, float ratio, float padX, float padY) Preprocess(Image<Rgb24> img)
    {
        int origW = img.Width;
        int origH = img.Height;

        // Compute letterbox parameters
        float ratio = Math.Min((float)imgSize / origH, (float)imgSize / origW);
        ratio = Math.Min(ratio, 1.0f); // Only scale down

        int newW = (int)Math.Round(origW * ratio);
        int newH = (int)Math.Round(origH * ratio);

        float padX = (imgSize - newW) / 2.0f;
        float padY = (imgSize - newH) / 2.0f;

        int top = (int)Math.Round(padY - 0.1);
        int left = (int)Math.Round(padX - 0.1);

        // Resize
        var resized = img.Clone();
        resized.Mutate(ctx => ctx.Resize(newW, newH));

        // Create padded tensor
        var data = new float[3 * imgSize * imgSize];
        // Fill with gray (114/255)
        float gray = 114.0f / 255.0f;
        for (int i = 0; i < data.Length; i++)
            data[i] = gray;

        // Copy resized image (and normalize to 0-1)
        resized.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < newH && (y + top) < imgSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < newW && (x + left) < imgSize; x++)
                {
                    var pixel = row[x];
                    int dy = y + top;
                    int dx = x + left;
                    data[0 * imgSize * imgSize + dy * imgSize + dx] = pixel.R / 255.0f;
                    data[1 * imgSize * imgSize + dy * imgSize + dx] = pixel.G / 255.0f;
                    data[2 * imgSize * imgSize + dy * imgSize + dx] = pixel.B / 255.0f;
                }
            }
        });

        resized.Dispose();

        var tensor = torch.tensor(data, dtype: ScalarType.Float32).reshape(3, imgSize, imgSize);
        return (tensor, ratio, padX, padY);
    }

    /// <summary>
    /// Post-process NMS results: scale boxes back to original image coordinates.
    /// </summary>
    private static List<Detection> PostProcess(Tensor nmsResult,
        int origW, int origH, float ratio, float padX, float padY)
    {
        var detections = new List<Detection>();

        if (nmsResult.shape[0] == 0)
            return detections;

        var data = nmsResult.data<float>().ToArray();
        long numDets = nmsResult.shape[0];

        for (long i = 0; i < numDets; i++)
        {
            float x1 = data[i * 6 + 0];
            float y1 = data[i * 6 + 1];
            float x2 = data[i * 6 + 2];
            float y2 = data[i * 6 + 3];
            float conf = data[i * 6 + 4];
            int cls = (int)data[i * 6 + 5];

            // Remove padding
            x1 -= padX;
            y1 -= padY;
            x2 -= padX;
            y2 -= padY;

            // Scale back to original
            x1 /= ratio;
            y1 /= ratio;
            x2 /= ratio;
            y2 /= ratio;

            // Clip to image bounds
            x1 = Math.Clamp(x1, 0, origW);
            y1 = Math.Clamp(y1, 0, origH);
            x2 = Math.Clamp(x2, 0, origW);
            y2 = Math.Clamp(y2, 0, origH);

            detections.Add(new Detection(x1, y1, x2, y2, conf, cls));
        }

        return detections;
    }

    public void Dispose()
    {
        if (!disposed)
        {
            // Model is owned by caller
            disposed = true;
        }
    }
}
