using Emgu.CV;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.IO;
using static Macro.Utils.ImagesUtils;
namespace Macro.Services
{
    class ScreenCaptureService
    {
        public static Mat CaptureScreen()
        {
            using Bitmap screenshot = new Bitmap((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);

            using (Graphics graphics = Graphics.FromImage(screenshot))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, screenshot.Size);
            }

            screenshot.Save(Path.Combine(AppContext.BaseDirectory, "screenshot.png"), ImageFormat.Png);

            Mat screen = BitmapToMat(screenshot);

            return screen;
        }
    }
}
