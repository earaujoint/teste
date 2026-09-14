using Emgu.CV;
using Emgu.CV.CvEnum;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace Macro.Utils
{
    public static class ImagesUtils
    {
        public static Mat BitmapToMat(Bitmap bitmap)
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            try
            {
                Mat mat = new Mat(bitmap.Height, bitmap.Width, DepthType.Cv8U, 3, data.Scan0, data.Stride);

                return mat.Clone();
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
