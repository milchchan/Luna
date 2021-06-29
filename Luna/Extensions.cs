using System;

namespace Luna
{
    public static class Extensions
    {
        public static System.Windows.Media.Imaging.BitmapSource ToBitmapSource(this System.Drawing.Bitmap bitmap)
        {
            var hBitmap = bitmap.GetHbitmap();
            System.Windows.Media.Imaging.BitmapSource bitmapSource;

            try
            {
                bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                NativeMethods.DeleteObject(hBitmap);
            }

            return bitmapSource;
        }

        public static System.Drawing.Bitmap ToBitmap(this System.Windows.Media.Imaging.BitmapSource bitmapSource, System.Drawing.Imaging.PixelFormat pixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppPArgb)
        {
            var bitmap = new System.Drawing.Bitmap(bitmapSource.PixelWidth, bitmapSource.PixelHeight, pixelFormat);
            var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, pixelFormat);

            bitmapSource.CopyPixels(System.Windows.Int32Rect.Empty, bitmapData.Scan0, bitmapData.Height * bitmapData.Stride, bitmapData.Stride);
            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }
    }
}
