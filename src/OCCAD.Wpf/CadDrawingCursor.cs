using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;

namespace OCCAD.Wpf;

internal static class CadDrawingCursor
{
    private const int Size = 64;
    private const int Center = Size / 2;
    private const int CenterGap = 7;
    private const int Margin = 2;

    private static readonly SafeCursorHandle CursorHandle = CreateHandle();

    public static Cursor Instance { get; } =
        CursorInteropHelper.Create(CursorHandle);

    private static SafeCursorHandle CreateHandle()
    {
        using var bitmap = new Bitmap(
            Size,
            Size,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.None;

            using var outline = new Pen(Color.Black, 3.0f);
            using var foreground = new Pen(Color.White, 1.0f);
            DrawCross(graphics, outline);
            DrawCross(graphics, foreground);
        }

        var iconHandle = bitmap.GetHicon();
        try
        {
            if (!GetIconInfo(iconHandle, out var info))
                throw new InvalidOperationException(
                    "Unable to create the CAD drawing cursor.");

            try
            {
                info.IsIcon = false;
                info.HotspotX = Center;
                info.HotspotY = Center;
                var cursorHandle = CreateIconIndirect(ref info);
                if (cursorHandle == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "Unable to create the CAD drawing cursor.");

                return new SafeCursorHandle(cursorHandle);
            }
            finally
            {
                if (info.MaskBitmap != IntPtr.Zero)
                    DeleteObject(info.MaskBitmap);
                if (info.ColorBitmap != IntPtr.Zero)
                    DeleteObject(info.ColorBitmap);
            }
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static void DrawCross(Graphics graphics, Pen pen)
    {
        graphics.DrawLine(
            pen,
            Margin,
            Center,
            Center - CenterGap,
            Center);
        graphics.DrawLine(
            pen,
            Center + CenterGap,
            Center,
            Size - Margin - 1,
            Center);
        graphics.DrawLine(
            pen,
            Center,
            Margin,
            Center,
            Center - CenterGap);
        graphics.DrawLine(
            pen,
            Center,
            Center + CenterGap,
            Center,
            Size - Margin - 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;
        public int HotspotX;
        public int HotspotY;
        public IntPtr MaskBitmap;
        public IntPtr ColorBitmap;
    }

    private sealed class SafeCursorHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeCursorHandle(IntPtr handle)
            : base(ownsHandle: true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle() =>
            DestroyIcon(handle);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(
        IntPtr icon,
        out IconInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(
        ref IconInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(
        IntPtr icon);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(
        IntPtr objectHandle);
}
