using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NEXA.Abstractions;
using OpenCvSharp;
using Serilog;

namespace NEXA.Adapters.Output;

/// <summary>
/// Concrete Windows OS implementation of <see cref="IScreenshotSink"/> utilizing native Win32 GDI BitBlt, OpenCvSharp image encoders, and Windows toast notifications.
/// <para>
/// <b>What it is:</b> Hardware-accelerated screen grabber for Windows desktops.
/// </para>
/// <para>
/// <b>What it does:</b>
/// <list type="number">
/// <item><description>Captures desktop pixels using GDI BitBlt.</description></item>
/// <item><description>Puts the captured bitmap onto the Windows system clipboard (<c>CF_BITMAP</c>).</description></item>
/// <item><description>Saves a high-resolution lossless PNG file with a timestamp in the target output directory.</description></item>
/// <item><description>Dispatches an asynchronous Windows desktop toast notification to alert the user.</description></item>
/// </list>
/// </para>
/// </summary>
public class Win32ScreenshotSink : IScreenshotSink
{
    private static readonly ILogger _log = Log.ForContext<Win32ScreenshotSink>();

    private const int SRCCOPY = 0x00CC0020;
    private const uint CF_BITMAP = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, IntPtr lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    /// <inheritdoc/>
    public bool CaptureScreenRegion(int screenX, int screenY, int width, int height, string outputDirectory, out string savedFilePath)
    {
        savedFilePath = string.Empty;

        int captureW = Math.Max(20, width);
        int captureH = Math.Max(20, height);

        IntPtr hdcScreen = GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
            return false;

        IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, captureW, captureH);
        IntPtr hOld = SelectObject(hdcMem, hBitmap);

        bool success = BitBlt(hdcMem, 0, 0, captureW, captureH, hdcScreen, screenX, screenY, SRCCOPY);

        SelectObject(hdcMem, hOld);

        if (success)
        {
            // 1. Copy to System Clipboard (CF_BITMAP)
            try
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    EmptyClipboard();
                    SetClipboardData(CF_BITMAP, hBitmap);
                    CloseClipboard();
                }
            }
            catch (ExternalException ex)
            {
                // Clipboard may be locked by another process – non-fatal, file write still proceeds
                _log.Warning(ex, "Clipboard operation failed (Win32 error {ErrorCode}). Screenshot will still be saved to disk.", ex.ErrorCode);
            }

            // 2. Extract Bitmap Pixels into OpenCvSharp Mat & Save PNG
            try
            {
                BITMAPINFO bmi = new();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = captureW;
                bmi.bmiHeader.biHeight = -captureH; // Top-down DIB
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0; // BI_RGB

                byte[] pixelBuffer = new byte[captureW * captureH * 4];
                GCHandle pinned = GCHandle.Alloc(pixelBuffer, GCHandleType.Pinned);

                try
                {
                    GetDIBits(hdcMem, hBitmap, 0, (uint)captureH, pinned.AddrOfPinnedObject(), ref bmi, 0);

                    using Mat bgraMat = Mat.FromPixelData(captureH, captureW, MatType.CV_8UC4, pinned.AddrOfPinnedObject());
                    using Mat bgrMat = new();
                    Cv2.CvtColor(bgraMat, bgrMat, ColorConversionCodes.BGRA2BGR);

                    if (!Directory.Exists(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    string fileName = $"NEXA_Screenshot_{timestamp}.png";
                    savedFilePath = Path.Combine(outputDirectory, fileName);

                    Cv2.ImWrite(savedFilePath, bgrMat);

                    // 3. Dispatch native Windows Desktop Toast Notification
                    ShowWindowsToastNotification("N.E.X.A. Screenshot", $"Vollbild-Screenshot gespeichert:\n{fileName}\nIn Zwischenablage kopiert!");
                }
                finally
                {
                    pinned.Free();
                }
            }
            catch (Exception ex) when (ex is COMException or IOException or ExternalException)
            {
                _log.Error(ex, "Screenshot file write failed for directory '{OutputDirectory}'.", outputDirectory);
            }
        }

        DeleteDC(hdcMem);
        ReleaseDC(IntPtr.Zero, hdcScreen);

        return success;
    }

    /// <summary>
    /// Displays a native Windows Action Center toast notification asynchronously without blocking the camera frame pipeline.
    /// </summary>
    private static void ShowWindowsToastNotification(string title, string message)
    {
        Task.Run(() =>
        {
            try
            {
                string escapedTitle = title.Replace("\"", "`\"");
                string escapedMessage = message.Replace("\"", "`\"");
                string script = $"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null; " +
                                $"$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02); " +
                                $"$textNodes = $template.GetElementsByTagName('text'); " +
                                $"$textNodes.Item(0).AppendChild($template.CreateTextNode('{escapedTitle}')) | Out-Null; " +
                                $"$textNodes.Item(1).AppendChild($template.CreateTextNode('{escapedMessage}')) | Out-Null; " +
                                $"$toast = [Windows.UI.Notifications.ToastNotification]::new($template); " +
                                $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('NEXA Hand Control').Show($toast);";

                ProcessStartInfo psi = new()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                // Non-critical: toast notifications are cosmetic only
                _log.Debug(ex, "Windows toast notification dispatch failed. This is non-critical.");
            }
        });
    }
}
