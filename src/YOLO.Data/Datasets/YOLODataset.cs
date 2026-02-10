using TorchSharp;
using static TorchSharp.torch;
using YOLO.Data.Augmentation;
using YOLO.Data.Utils;

namespace YOLO.Data.Datasets;

/// <summary>
/// YOLO format dataset loader.
/// Loads images and YOLO-format labels (class cx cy w h, normalized).
/// Supports mosaic augmentation, random augmentations, and batched loading.
/// </summary>
public class YOLODataset
{
    private readonly string[] imagePaths;
    private readonly string[] labelPaths;
    private readonly int imgSize;
    private AugmentationPipeline pipeline;
    private bool useMosaic;
    private readonly Random rng;
    private List<BboxInstance>[]? labelCache;

    /// <summary>Number of samples in the dataset.</summary>
    public int Count => imagePaths.Length;

    /// <summary>
    /// Create a YOLO dataset from an image directory.
    /// </summary>
    /// <param name="imageDir">Directory containing images</param>
    /// <param name="imgSize">Target image size</param>
    /// <param name="pipeline">Augmentation pipeline</param>
    /// <param name="useMosaic">Whether to use mosaic augmentation</param>
    public YOLODataset(string imageDir, int imgSize = 640,
        AugmentationPipeline? pipeline = null, bool useMosaic = false)
    {
        this.imgSize = imgSize;
        this.useMosaic = useMosaic;
        this.pipeline = pipeline ?? AugmentationPipeline.CreateValPipeline(imgSize);
        rng = new Random();

        // Discover image files
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp" };

        imagePaths = Directory.GetFiles(imageDir, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f)
            .ToArray();

        labelPaths = imagePaths
            .Select(LabelParser.ResolveLabelPath)
            .ToArray();
    }

    /// <summary>
    /// Pre-cache all labels into memory.
    /// Logs diagnostics about missing label files and total GT box counts.
    /// Optionally filters labels by class range [0, numClasses).
    /// </summary>
    /// <param name="numClasses">If > 0, reject any label with classId >= numClasses.</param>
    public void CacheLabels(int numClasses = -1)
    {
        labelCache = new List<BboxInstance>[imagePaths.Length];
        int missingLabels = 0;
        int emptyLabels = 0;
        int totalBoxes = 0;
        int rejectedBoxes = 0;
        int firstMissingIdx = -1;

        for (int i = 0; i < imagePaths.Length; i++)
        {
            if (!File.Exists(labelPaths[i]))
            {
                missingLabels++;
                if (firstMissingIdx < 0) firstMissingIdx = i;
            }

            var labels = LabelParser.ParseYOLOLabel(labelPaths[i]);

            // Filter by class range if specified
            if (numClasses > 0)
            {
                int before = labels.Count;
                labels = labels.Where(l => l.ClassId >= 0 && l.ClassId < numClasses).ToList();
                rejectedBoxes += before - labels.Count;
            }

            labelCache[i] = labels;

            if (labelCache[i].Count == 0 && File.Exists(labelPaths[i]))
                emptyLabels++;

            totalBoxes += labelCache[i].Count;
        }

        if (missingLabels > 0)
        {
            Console.WriteLine($"  WARNING: {missingLabels}/{imagePaths.Length} label files not found!");
            if (firstMissingIdx >= 0)
            {
                Console.WriteLine($"    Example image: {imagePaths[firstMissingIdx]}");
                Console.WriteLine($"    Expected label: {labelPaths[firstMissingIdx]}");
            }
        }

        if (emptyLabels > 0)
        {
            Console.WriteLine($"  WARNING: {emptyLabels}/{imagePaths.Length} label files are empty or unparseable.");
        }

        if (rejectedBoxes > 0)
        {
            Console.WriteLine($"  WARNING: {rejectedBoxes} boxes rejected (classId out of range [0, {numClasses})).");
        }

        Console.WriteLine($"  Total GT boxes: {totalBoxes} across {imagePaths.Length} images");
    }

    /// <summary>
    /// Get label statistics for the dataset.
    /// </summary>
    public (int totalBoxes, int imagesWithBoxes, int missingLabelFiles) GetLabelStats()
    {
        int totalBoxes = 0;
        int imagesWithBoxes = 0;
        int missingLabelFiles = 0;

        for (int i = 0; i < imagePaths.Length; i++)
        {
            if (!File.Exists(labelPaths[i]))
                missingLabelFiles++;

            var labels = labelCache != null
                ? labelCache[i]
                : LabelParser.ParseYOLOLabel(labelPaths[i]);

            if (labels.Count > 0)
            {
                imagesWithBoxes++;
                totalBoxes += labels.Count;
            }
        }

        return (totalBoxes, imagesWithBoxes, missingLabelFiles);
    }

    /// <summary>
    /// Get labels for a given index.
    /// </summary>
    private List<BboxInstance> GetLabels(int index)
    {
        if (labelCache != null)
            return labelCache[index].Select(l => l.Clone()).ToList();
        return LabelParser.ParseYOLOLabel(labelPaths[index]);
    }

    /// <summary>
    /// Load a single item (image + labels) with augmentation applied.
    /// </summary>
    public (Tensor image, Tensor bboxes, Tensor classes) GetItem(int index)
    {
        byte[] imgData;
        int w, h;
        List<BboxInstance> labels;

        if (useMosaic)
        {
            // Mosaic: load 4 images
            var images = new (byte[] data, int w, int h)[4];
            var allLabels = new List<BboxInstance>[4];

            for (int i = 0; i < 4; i++)
            {
                int idx = (i == 0) ? index : rng.Next(Count);
                (images[i].data, images[i].w, images[i].h) = ImageUtils.LoadImageHWC(imagePaths[idx]);
                allLabels[i] = GetLabels(idx);
            }

            (imgData, w, h, labels) = pipeline.ApplyMosaic(images, allLabels, imgSize);
        }
        else
        {
            (imgData, w, h) = ImageUtils.LoadImageHWC(imagePaths[index]);
            labels = GetLabels(index);
            (imgData, w, h, labels) = pipeline.Apply(imgData, w, h, labels, imgSize);
        }

        // Convert image to tensor: HWC bytes -> CHW float / 255
        var imgTensor = ImageUtils.HWCToTensor(imgData, w, h) / 255.0f;

        // Convert labels to tensors
        if (labels.Count > 0)
        {
            var bboxData = new float[labels.Count * 4];
            var classData = new float[labels.Count];
            for (int i = 0; i < labels.Count; i++)
            {
                var xyxy = labels[i].BboxXyxy;
                bboxData[i * 4 + 0] = xyxy[0];
                bboxData[i * 4 + 1] = xyxy[1];
                bboxData[i * 4 + 2] = xyxy[2];
                bboxData[i * 4 + 3] = xyxy[3];
                classData[i] = labels[i].ClassId;
            }

            var bboxTensor = torch.tensor(bboxData).reshape(labels.Count, 4);
            var classTensor = torch.tensor(classData).reshape(labels.Count);
            return (imgTensor, bboxTensor, classTensor);
        }
        else
        {
            return (imgTensor,
                torch.zeros(0, 4, dtype: ScalarType.Float32),
                torch.zeros(0, dtype: ScalarType.Float32));
        }
    }

    /// <summary>
    /// Create a training batch with padding for variable numbers of GT boxes.
    /// </summary>
    /// <param name="batchSize">Batch size</param>
    /// <param name="shuffle">Whether to shuffle indices</param>
    /// <returns>Enumerable of (images, gtBboxes, gtLabels, maskGT) batches</returns>
    public IEnumerable<(Tensor images, Tensor gtBboxes, Tensor gtLabels, Tensor maskGT)>
        GetBatches(int batchSize, bool shuffle = true)
    {
        var indices = Enumerable.Range(0, Count).ToArray();
        if (shuffle)
        {
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
        }

        for (int start = 0; start < Count; start += batchSize)
        {
            int end = Math.Min(start + batchSize, Count);
            int actualBatch = end - start;

            var imageList = new Tensor[actualBatch];
            var bboxList = new Tensor[actualBatch];
            var clsList = new Tensor[actualBatch];
            int maxGT = 0;

            for (int i = 0; i < actualBatch; i++)
            {
                (imageList[i], bboxList[i], clsList[i]) = GetItem(indices[start + i]);
                maxGT = Math.Max(maxGT, (int)bboxList[i].shape[0]);
            }

            maxGT = Math.Max(maxGT, 1); // at least 1 to avoid empty tensors

            // Stack images
            var images = torch.stack(imageList);

            // Pad bboxes and classes to maxGT
            var gtBboxes = torch.zeros(actualBatch, maxGT, 4, dtype: ScalarType.Float32);
            var gtLabels = torch.zeros(actualBatch, maxGT, 1, dtype: ScalarType.Float32);
            var maskGT = torch.zeros(actualBatch, maxGT, 1, dtype: ScalarType.Float32);

            for (int i = 0; i < actualBatch; i++)
            {
                int ngt = (int)bboxList[i].shape[0];
                if (ngt > 0)
                {
                    gtBboxes[i, ..ngt] = bboxList[i];
                    gtLabels[i, ..ngt, 0] = clsList[i];
                    maskGT[i, ..ngt, 0] = 1.0f;
                }
            }

            yield return (images, gtBboxes, gtLabels, maskGT);

            // Dispose intermediate tensors
            foreach (var t in imageList) t.Dispose();
            foreach (var t in bboxList) t.Dispose();
            foreach (var t in clsList) t.Dispose();
        }
    }

    /// <summary>
    /// Switch augmentation pipeline (used for close_mosaic epochs at end of training).
    /// </summary>
    /// <param name="newPipeline">The new augmentation pipeline to use</param>
    /// <param name="useMosaic">Whether to use mosaic augmentation</param>
    public void SetPipeline(AugmentationPipeline newPipeline, bool useMosaic = false)
    {
        this.pipeline = newPipeline;
        this.useMosaic = useMosaic;
    }
}
