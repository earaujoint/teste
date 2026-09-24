using Emgu.CV;
using Emgu.CV.CvEnum;
using System.Drawing;
using System.IO;

namespace Macro.Services;

/// <summary>Detects the EnergySave dragon screen without relying on localized text.</summary>
public sealed class EnergySaveDetector : IDisposable
{
    // Keep the match strict: a loose threshold can mistake similar dark-blue game screens for EnergySave.
    public const double MinimumConfidence = 0.82;
    private readonly Mat template;

    public EnergySaveDetector()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "EnergySave", "energy-save.png");
        if (!File.Exists(path)) throw new FileNotFoundException("Template EnergySave ausente.", path);
        template = CvInvoke.Imread(path, ImreadModes.Grayscale);
        if (template.IsEmpty) throw new InvalidDataException("Template EnergySave inválido.");
    }

    public bool Detect(Mat client, out double confidence)
    {
        confidence = 0;
        using var gray = new Mat();
        CvInvoke.CvtColor(client, gray, ColorConversion.Bgr2Gray);
        CvInvoke.GaussianBlur(gray, gray, new Size(3, 3), .8);

        var searchArea = new Rectangle((int)(client.Width * .28), (int)(client.Height * .12),
            (int)(client.Width * .44), (int)(client.Height * .55));
        searchArea = Rectangle.Intersect(searchArea, new Rectangle(Point.Empty, client.Size));
        if (searchArea.Width < 20 || searchArea.Height < 20) return false;

        using var search = new Mat(gray, searchArea);
        double baseScale = client.Width / 1912.0;
        for (int step = -5; step <= 5; step++)
        {
            double scale = baseScale * (1 + step * .03);
            var size = new Size((int)Math.Round(template.Width * scale), (int)Math.Round(template.Height * scale));
            if (size.Width > search.Width || size.Height > search.Height || size.Width < 20) continue;
            using var scaled = new Mat();
            CvInvoke.Resize(template, scaled, size, 0, 0, Inter.Linear);
            CvInvoke.GaussianBlur(scaled, scaled, new Size(3, 3), .8);
            using var result = new Mat();
            CvInvoke.MatchTemplate(search, scaled, result, TemplateMatchingType.CcoeffNormed);
            double min = 0, max = 0;
            Point minPoint = default, maxPoint = default;
            CvInvoke.MinMaxLoc(result, ref min, ref max, ref minPoint, ref maxPoint);
            confidence = Math.Max(confidence, double.IsFinite(max) ? max : 0);
        }
        return confidence >= MinimumConfidence;
    }

    public void Dispose() => template.Dispose();
}
