using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace FatigueDetect.Datos
{
    public static class ImageUtils
    {
        public static Bitmap? MatToBitmap(Mat mat)
        {
            try
            {
                return BitmapConverter.ToBitmap(mat);
            }
            catch
            {
                return null;
            }
        }
    }
}
