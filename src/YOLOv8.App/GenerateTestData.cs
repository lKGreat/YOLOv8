namespace YOLOv8.App;

/// <summary>
/// Generates a synthetic test dataset in YOLO format for training validation.
/// Creates simple colored-rectangle images with YOLO label files.
/// </summary>
public static class TestDataGenerator
{
    /// <summary>
    /// Generate synthetic images (BMP) and YOLO-format label files.
    /// </summary>
    /// <param name="baseDir">Base directory for the dataset</param>
    /// <param name="numTrain">Number of training images</param>
    /// <param name="numVal">Number of validation images</param>
    /// <param name="imgSize">Image width and height</param>
    /// <param name="numClasses">Number of classes</param>
    public static void Generate(string baseDir, int numTrain = 32, int numVal = 8,
        int imgSize = 128, int numClasses = 3)
    {
        var rng = new Random(42);

        var trainImgDir = Path.Combine(baseDir, "images", "train");
        var trainLblDir = Path.Combine(baseDir, "labels", "train");
        var valImgDir = Path.Combine(baseDir, "images", "val");
        var valLblDir = Path.Combine(baseDir, "labels", "val");

        Directory.CreateDirectory(trainImgDir);
        Directory.CreateDirectory(trainLblDir);
        Directory.CreateDirectory(valImgDir);
        Directory.CreateDirectory(valLblDir);

        Console.WriteLine($"Generating {numTrain} train + {numVal} val images ({imgSize}x{imgSize}, {numClasses} classes)...");

        for (int i = 0; i < numTrain; i++)
            GenerateImage(rng, trainImgDir, trainLblDir, $"img_{i:D4}", imgSize, numClasses);

        for (int i = 0; i < numVal; i++)
            GenerateImage(rng, valImgDir, valLblDir, $"img_{i:D4}", imgSize, numClasses);

        // Write dataset YAML
        var yamlPath = Path.Combine(baseDir, "testdata.yaml");
        var yaml = $"""
            path: {baseDir}
            train: images/train
            val: images/val

            nc: {numClasses}
            names: [{string.Join(", ", Enumerable.Range(0, numClasses).Select(c => $"'class{c}'"))}]
            """;
        File.WriteAllText(yamlPath, yaml);

        Console.WriteLine($"Dataset YAML written to: {yamlPath}");
        Console.WriteLine("Test data generation complete.");
    }

    private static void GenerateImage(Random rng, string imgDir, string lblDir,
        string name, int size, int numClasses)
    {
        // Create a simple BMP image with colored rectangles representing objects
        // BMP format: header + pixel data (BGR, bottom-up, padded rows)
        var pixels = new byte[size * size * 3];

        // Random background color (dark)
        byte bgR = (byte)rng.Next(30, 80);
        byte bgG = (byte)rng.Next(30, 80);
        byte bgB = (byte)rng.Next(30, 80);

        for (int i = 0; i < pixels.Length; i += 3)
        {
            pixels[i] = bgR;
            pixels[i + 1] = bgG;
            pixels[i + 2] = bgB;
        }

        // Add 1-4 random rectangles (objects)
        int numObjects = rng.Next(1, 5);
        var labels = new List<string>();

        for (int obj = 0; obj < numObjects; obj++)
        {
            int cls = rng.Next(numClasses);

            // Random box (ensure minimum size)
            float cx = (float)(rng.NextDouble() * 0.6 + 0.2); // center x normalized
            float cy = (float)(rng.NextDouble() * 0.6 + 0.2); // center y normalized
            float bw = (float)(rng.NextDouble() * 0.3 + 0.1); // width normalized
            float bh = (float)(rng.NextDouble() * 0.3 + 0.1); // height normalized

            // Clip
            float x1 = Math.Max(0, cx - bw / 2);
            float y1 = Math.Max(0, cy - bh / 2);
            float x2 = Math.Min(1, cx + bw / 2);
            float y2 = Math.Min(1, cy + bh / 2);
            cx = (x1 + x2) / 2;
            cy = (y1 + y2) / 2;
            bw = x2 - x1;
            bh = y2 - y1;

            // Draw colored rectangle into pixel buffer
            byte objR = (byte)(50 + cls * 70 + rng.Next(30));
            byte objG = (byte)(100 + (cls % 2) * 100 + rng.Next(30));
            byte objB = (byte)(150 - cls * 50 + rng.Next(30));

            int px1 = (int)(x1 * size);
            int py1 = (int)(y1 * size);
            int px2 = (int)(x2 * size);
            int py2 = (int)(y2 * size);

            for (int y = py1; y < py2 && y < size; y++)
            {
                for (int x = px1; x < px2 && x < size; x++)
                {
                    int idx = (y * size + x) * 3;
                    pixels[idx] = objR;
                    pixels[idx + 1] = objG;
                    pixels[idx + 2] = objB;
                }
            }

            // YOLO label: class cx cy w h (normalized)
            labels.Add($"{cls} {cx:F6} {cy:F6} {bw:F6} {bh:F6}");
        }

        // Write BMP file
        WriteBmp(Path.Combine(imgDir, $"{name}.bmp"), pixels, size, size);

        // Write label file
        File.WriteAllLines(Path.Combine(lblDir, $"{name}.txt"), labels);
    }

    /// <summary>
    /// Write a simple 24-bit BMP file.
    /// </summary>
    private static void WriteBmp(string path, byte[] rgbPixels, int width, int height)
    {
        // BMP row stride must be a multiple of 4
        int rowStride = (width * 3 + 3) & ~3;
        int imageSize = rowStride * height;
        int fileSize = 54 + imageSize;

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        // BMP File Header (14 bytes)
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write((short)0); // reserved
        bw.Write((short)0); // reserved
        bw.Write(54); // pixel data offset

        // DIB Header (BITMAPINFOHEADER, 40 bytes)
        bw.Write(40); // header size
        bw.Write(width);
        bw.Write(height);
        bw.Write((short)1);  // color planes
        bw.Write((short)24); // bits per pixel
        bw.Write(0);  // compression (none)
        bw.Write(imageSize);
        bw.Write(2835); // horizontal resolution (72 DPI)
        bw.Write(2835); // vertical resolution
        bw.Write(0);    // colors in palette
        bw.Write(0);    // important colors

        // Pixel data (BMP is bottom-up, BGR order)
        var padding = new byte[rowStride - width * 3];
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = (y * width + x) * 3;
                bw.Write(rgbPixels[idx + 2]); // B
                bw.Write(rgbPixels[idx + 1]); // G
                bw.Write(rgbPixels[idx]);     // R
            }
            if (padding.Length > 0)
                bw.Write(padding);
        }
    }
}
