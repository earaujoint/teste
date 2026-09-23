using Emgu.CV;
using Emgu.CV.CvEnum;
using System.Drawing;
using System.IO;

namespace Macro.Services;

/// <summary>Matches both reward buttons at a common scale in client-area pixels.</summary>
public sealed class RaidRewardDetector : IDisposable
{
    // Each button is checked independently. Requiring both prevents an unrelated
    // OK prompt (or a blue button elsewhere) from being dismissed.
    public const double MinimumConfidence = 0.84;
    private const double ButtonOffsetX = 406;
    private readonly Mat team;
    private readonly Mat ok;

    public RaidRewardDetector()
    {
        team = Load("team-reward.png");
        try { ok = Load("ok.png"); }
        catch { team.Dispose(); throw; }
    }

    private static Mat Load(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "RaidDetection", name);
        if (!File.Exists(path)) throw new FileNotFoundException("Template de recompensa ausente.", path);
        Mat result = CvInvoke.Imread(path, ImreadModes.Grayscale);
        if (result.IsEmpty) { result.Dispose(); throw new InvalidDataException($"Template inválido: {path}"); }
        return result;
    }

    public Rectangle? Detect(Mat client, out double confidence)
    {
        confidence = 0;
        using var gray = new Mat();
        CvInvoke.CvtColor(client, gray, ColorConversion.Bgr2Gray);
        // Reduce sensitivity to subpixel text antialiasing when the UI is resized.
        CvInvoke.GaussianBlur(gray, gray, new Size(3, 3), .8);
        // The reference client is 1919 x 1009 (without title/task bars).
        double baseScale = Math.Min(client.Width / 1919.0, client.Height / 1009.0);
        var searchArea = new Rectangle((int)(client.Width * .15), (int)(client.Height * .72),
            (int)(client.Width * .70), (int)(client.Height * .27));
        if (searchArea.Width < 20 || searchArea.Height < 20) return null;
        using var search = new Mat(gray, searchArea);
        Rectangle? best = null;
        for (int step = -6; step <= 6; step++)
        {
            double scale = baseScale * (1 + step * .025);
            var size = new Size((int)Math.Round(team.Width * scale), (int)Math.Round(team.Height * scale));
            if (size.Width < 20 || size.Height < 8 || size.Width > search.Width || size.Height > search.Height) continue;
            using var teamScaled = new Mat();
            using var okScaled = new Mat();
            CvInvoke.Resize(team, teamScaled, size, 0, 0, Inter.Linear);
            CvInvoke.Resize(ok, okScaled, size, 0, 0, Inter.Linear);
            CvInvoke.GaussianBlur(teamScaled, teamScaled, new Size(3, 3), .8);
            CvInvoke.GaussianBlur(okScaled, okScaled, new Size(3, 3), .8);
            // Locate the blue Party Rewards button first. Then search for OK only
            // in the nearby position used by the reward dialog. Looking for both
            // templates globally could combine two unrelated UI elements.
            var left = Match(search, teamScaled);
            int toleranceX = Math.Max(24, (int)Math.Round(55 * scale));
            int toleranceY = Math.Max(16, (int)Math.Round(35 * scale));
            int expectedOkX = left.Point.X + (int)Math.Round(ButtonOffsetX * scale);
            var okArea = Rectangle.Intersect(
                new Rectangle(expectedOkX - toleranceX, left.Point.Y - toleranceY,
                    okScaled.Width + toleranceX * 2, okScaled.Height + toleranceY * 2),
                new Rectangle(Point.Empty, search.Size));
            if (okArea.Width < okScaled.Width || okArea.Height < okScaled.Height) continue;

            using var okSearch = new Mat(search, okArea);
            var localOk = Match(okSearch, okScaled);
            Point okPoint = new(localOk.Point.X + okArea.X, localOk.Point.Y + okArea.Y);
            double score = Math.Min(left.Score, localOk.Score);
            if (score <= confidence) continue;
            confidence = score;
            best = new Rectangle(okPoint.X + searchArea.X, okPoint.Y + searchArea.Y, size.Width, size.Height);
        }
        return confidence >= MinimumConfidence ? best : null;
    }

    private static (double Score, Point Point) Match(Mat source, Mat template)
    {
        using var result = new Mat();
        CvInvoke.MatchTemplate(source, template, result, TemplateMatchingType.CcoeffNormed);
        double min = 0, max = 0;
        Point minPoint = default, maxPoint = default;
        CvInvoke.MinMaxLoc(result, ref min, ref max, ref minPoint, ref maxPoint);
        return (double.IsFinite(max) ? max : 0, maxPoint);
    }

    public void Dispose() { team.Dispose(); ok.Dispose(); }
}
