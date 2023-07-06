using System.Drawing;
using System.Runtime.InteropServices;

namespace MagicSpeller;

public static class ScreenShooter
{
    public static Image CaptureScreen() => CaptureWindow(User32.GetDesktopWindow());

    public static Image CaptureWindow(IntPtr handle)
    {
        // get te hDC of the target window
        var hdcSrc = User32.GetWindowDC(handle);
        // get the size
        var windowRect = new User32.RECT();
        User32.GetWindowRect(handle, ref windowRect);
        var width = windowRect.right - windowRect.left;
        var height = windowRect.bottom - windowRect.top;
        // create a device context we can copy to
        var hdcDest = Gdi32.CreateCompatibleDC(hdcSrc);
        // create a bitmap we can copy it to,
        // using GetDeviceCaps to get the width/height
        var hBitmap = Gdi32.CreateCompatibleBitmap(hdcSrc, width, height);
        // select the bitmap object
        var hOld = Gdi32.SelectObject(hdcDest, hBitmap);
        // bitblt over
        Gdi32.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, Gdi32.SrcCopy);
        // restore selection
        Gdi32.SelectObject(hdcDest, hOld);
        // clean up
        Gdi32.DeleteDC(hdcDest);
        User32.ReleaseDC(handle, hdcSrc);

        // get a .NET image object for it
        Image img = Image.FromHbitmap(hBitmap);
        // free up the Bitmap object
        Gdi32.DeleteObject(hBitmap);

        return img;
    }

    private class Gdi32
    {
        public const int SrcCopy = 0x00CC0020; // BitBlt dwRop parameter

        [DllImport("gdi32.dll")]
        public extern static bool BitBlt(IntPtr hObject, int nXDest, int nYDest,
            int nWidth, int nHeight, IntPtr hObjectSource,
            int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        public extern static IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth,
            int nHeight);

        [DllImport("gdi32.dll")]
        public extern static IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        public extern static bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        public extern static bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public extern static IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    }

    private class User32
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        public extern static IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public extern static IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public extern static IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        public extern static IntPtr GetWindowRect(IntPtr hWnd, ref RECT rect);
    }
}
