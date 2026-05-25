using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

#pragma warning disable CA1416 // Validate platform compatibility

namespace RegionSelector.Services;

public class WindowsScreenCaptureService : IScreenCaptureService
{
    public Task CaptureRegionAsync(int x, int y, int width, int height, string outputPath)
    {
        return Task.Run(() =>
        {
            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            bitmap.Save(outputPath, ImageFormat.Png);
        });
    }

    public Task CaptureFullScreenAsync(string outputPath)
    {
        // We use a predefined full screen dimensions (which would typically be passed down)
        // Since we are running in an Avalonia context, it's better to capture the main screen bounds 
        // However, this simple implementation captures the primary monitor via System.Windows.Forms if available,
        // or we expect bounds to be handled. For simplicity, we capture a large area or require bounds.
        // Wait, since we are doing Avalonia, we can use the Primary Screen bounds. 
        // But since this method doesn't receive bounds, we will just use a large fallback or standard API.
        
        return Task.Run(() =>
        {
            // Fallback for Windows full screen capture: 
            // We can get the virtual screen size, but we will just capture the primary screen.
            // On Windows without WinForms, we can P/Invoke GetSystemMetrics or just throw if we don't have bounds.
            throw new NotImplementedException("For Windows full screen, please provide bounds via CaptureRegionAsync.");
        });
    }
}
