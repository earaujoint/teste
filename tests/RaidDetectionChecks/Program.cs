using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Macro.Services;
using System.Drawing;

if (args.Length != 1) throw new ArgumentException("Informe o caminho da captura de referência.");
using var screenshot = CvInvoke.Imread(args[0]);
// Captures may include or omit the window chrome. The detector receives the client
// image in production, so use the supplied frame as-is for this diagnostic.
using var client = screenshot.Clone();
using var detector = new RaidRewardDetector();
int checks = 0;
void Check(string name, Mat frame, bool expected, Point? expectedCenter = null)
{
    var match = detector.Detect(frame, out double score);
    Console.WriteLine($"{name}: encontrado={match.HasValue}, score={score:F3}, posição={match}");
    if (match.HasValue != expected) throw new Exception($"Falhou: {name}");
    if (match is Rectangle button)
    {
        double x = (button.X + button.Width / 2.0) / frame.Width;
        double y = (button.Y + button.Height / 2.0) / frame.Height;
        double expectedX = expectedCenter?.X / (double)frame.Width ?? 1163.0 / 1919;
        double expectedY = expectedCenter?.Y / (double)frame.Height ?? 911.0 / 1009;
        if (Math.Abs(x - expectedX) > .01 || Math.Abs(y - expectedY) > .01)
            throw new Exception("Centro de clique incorreto.");
    }
    checks++;
}
foreach (double scale in new[] { 1.0, .75, 2.0 / 3, 1.25 })
{
    using var resized = new Mat();
    CvInvoke.Resize(client, resized, new Size((int)(client.Width * scale), (int)(client.Height * scale)));
    Check($"Escala {scale:F3}", resized, true);
}
using var dimmed = new Mat();
client.ConvertTo(dimmed, DepthType.Cv8U, .8, 5);
Check("Variação de brilho", dimmed, true);
using var onlyOk = client.Clone();
CvInvoke.Rectangle(onlyOk, new Rectangle(560, 860, 395, 105), new MCvScalar(0, 0, 0), -1);
Check("Apenas OK", onlyOk, false);
using var onlyTeam = client.Clone();
CvInvoke.Rectangle(onlyTeam, new Rectangle(965, 860, 398, 105), new MCvScalar(0, 0, 0), -1);
Check("Apenas informação", onlyTeam, false);
using var blank = new Mat(client.Size, DepthType.Cv8U, 3);
blank.SetTo(new MCvScalar(0, 0, 0));
Check("Tela preta", blank, false);
using var misplaced = client.Clone();
using (var ok = new Mat(client, new Rectangle(968, 863, 390, 96)))
{
    CvInvoke.Rectangle(misplaced, new Rectangle(965, 860, 398, 105), new MCvScalar(0, 0, 0), -1);
    using var destination = new Mat(misplaced, new Rectangle(1400, 863, 390, 96));
    ok.CopyTo(destination);
}
Check("Botões sem alinhamento esperado", misplaced, false);
Console.WriteLine($"{checks} verificações passaram. Nenhuma interação com o jogo foi executada.");
