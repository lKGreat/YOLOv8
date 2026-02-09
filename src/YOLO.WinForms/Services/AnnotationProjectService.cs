using PDFtoImage;
using SkiaSharp;
using YOLO.WinForms.Models;

namespace YOLO.WinForms.Services;

/// <summary>
/// Service for annotation project CRUD, image/PDF import, and YOLO label export.
/// </summary>
public class AnnotationProjectService
{
    private static readonly string[] SupportedImageExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp"];

    /// <summary>
    /// Create a new annotation project with folder structure.
    /// </summary>
    public AnnotationProject CreateProject(string name, string parentFolder)
    {
        var projectPath = Path.Combine(parentFolder, name);
        Directory.CreateDirectory(projectPath);
        Directory.CreateDirectory(Path.Combine(projectPath, "images"));

        var project = new AnnotationProject
        {
            Name = name,
            ProjectPath = projectPath,
            CreatedAt = DateTime.Now
        };

        project.Save();
        return project;
    }

    /// <summary>
    /// Open an existing project from its .yolo-anno file.
    /// </summary>
    public AnnotationProject OpenProject(string projectFilePath)
    {
        return AnnotationProject.Load(projectFilePath);
    }

    /// <summary>
    /// Import images by copying them to the project images folder.
    /// Returns the count of newly imported images.
    /// </summary>
    public int ImportImages(AnnotationProject project, string[] filePaths,
        IProgress<(int current, int total)>? progress = null)
    {
        Directory.CreateDirectory(project.ImagesFolder);
        var existingNames = new HashSet<string>(
            project.Images.Select(i => i.FileName),
            StringComparer.OrdinalIgnoreCase);

        int imported = 0;
        for (int i = 0; i < filePaths.Length; i++)
        {
            var srcPath = filePaths[i];
            var ext = Path.GetExtension(srcPath).ToLowerInvariant();

            if (!SupportedImageExtensions.Contains(ext))
                continue;

            var destName = Path.GetFileName(srcPath);

            // Avoid duplicates by appending a number
            if (existingNames.Contains(destName))
            {
                var baseName = Path.GetFileNameWithoutExtension(srcPath);
                int counter = 1;
                while (existingNames.Contains($"{baseName}_{counter}{ext}"))
                    counter++;
                destName = $"{baseName}_{counter}{ext}";
            }

            var destPath = Path.Combine(project.ImagesFolder, destName);
            File.Copy(srcPath, destPath, overwrite: false);

            project.Images.Add(new AnnotationImageInfo { FileName = destName });
            existingNames.Add(destName);
            imported++;

            progress?.Report((i + 1, filePaths.Length));
        }

        project.Save();
        return imported;
    }

    /// <summary>
    /// Import a PDF file by rendering each page to a PNG image.
    /// Returns the count of pages imported.
    /// </summary>
    public int ImportPdf(AnnotationProject project, string pdfPath,
        int dpi = 300, IProgress<(int current, int total)>? progress = null)
    {
        Directory.CreateDirectory(project.ImagesFolder);
        var existingNames = new HashSet<string>(
            project.Images.Select(i => i.FileName),
            StringComparer.OrdinalIgnoreCase);

        var pdfBaseName = Path.GetFileNameWithoutExtension(pdfPath);
        using var pdfStream = File.OpenRead(pdfPath);

        int pageCount = Conversion.GetPageCount(pdfStream);
        int imported = 0;

        for (int page = 0; page < pageCount; page++)
        {
            pdfStream.Position = 0;
            var renderOptions = new RenderOptions { Dpi = dpi };
            using var bitmap = Conversion.ToImage(pdfStream, page: new Index(page), options: renderOptions);

            string destName = $"{pdfBaseName}_page{page + 1:D4}.png";
            if (existingNames.Contains(destName))
            {
                int counter = 1;
                while (existingNames.Contains($"{pdfBaseName}_page{page + 1:D4}_{counter}.png"))
                    counter++;
                destName = $"{pdfBaseName}_page{page + 1:D4}_{counter}.png";
            }

            var destPath = Path.Combine(project.ImagesFolder, destName);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(destPath, data.ToArray());

            project.Images.Add(new AnnotationImageInfo { FileName = destName });
            existingNames.Add(destName);
            imported++;

            progress?.Report((page + 1, pageCount));
        }

        project.Save();
        return imported;
    }

    /// <summary>
    /// Export all annotations to YOLO-format .txt label files alongside images.
    /// Creates a 'labels' folder next to 'images' in the project.
    /// Returns count of label files written.
    /// </summary>
    public int ExportYoloLabels(AnnotationProject project)
    {
        var labelsFolder = Path.Combine(project.ProjectPath, "labels");
        Directory.CreateDirectory(labelsFolder);

        int count = 0;
        foreach (var img in project.Images)
        {
            if (img.Annotations.Count == 0) continue;

            var labelName = Path.ChangeExtension(img.FileName, ".txt");
            var labelPath = Path.Combine(labelsFolder, labelName);

            var lines = img.Annotations.Select(a => a.ToYoloLine());
            File.WriteAllLines(labelPath, lines);
            count++;
        }

        return count;
    }
}
