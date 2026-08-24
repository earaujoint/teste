using Emgu.CV;
using Emgu.CV.CvEnum;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using SDPoint = System.Drawing.Point;
using static Macro.Services.ScreenCaptureService;
using Macro.Views;
using System.Windows.Shapes;

namespace Macro.Views
{
    /// <summary>
    /// Interação lógica para FarmingPage.xam
    /// </summary>
    public partial class FarmingPage : Page
    {
        public FarmingPage()
        {
            InitializeComponent();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Parado");
        }
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            using Mat screen = CaptureScreen();
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "teste.png");

            using Mat template = CvInvoke.Imread(path, ImreadModes.ColorBgr);

            if (template.IsEmpty)
            {
                MessageBox.Show("Não foi possível carregar o template.");
                return;
            }

            using Mat result = new Mat();

            CvInvoke.MatchTemplate(screen, template, result, TemplateMatchingType.CcoeffNormed);

            double minVal = 0;
            double maxVal = 0;

            SDPoint minLoc = new SDPoint();
            SDPoint maxLoc = new SDPoint();

            CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

            if (maxVal >= 0.7)
            {
                var messageBox = new CommonMessageBox(
                    "Imagem encontrada",
                    $"Confiança: {maxVal:P2}\n" +
                    $"X: {maxLoc.X}\n" +
                    $"Y: {maxLoc.Y}");

                messageBox.ShowDialog();
            }
            else
            {
                var messageBox = new CommonMessageBox(
                    "Imagem  não encontrada",
                    $"Confiança: {maxVal:P2}\n");

                messageBox.ShowDialog();
            }
        }
    }
}
