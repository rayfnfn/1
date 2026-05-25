using System.Threading.Tasks;

namespace RegionSelector.Services;

public interface IScreenCaptureService
{
    /// <summary>
    /// Captures a specific region of the screen and saves it as a PNG file.
    /// </summary>
    Task CaptureRegionAsync(int x, int y, int width, int height, string outputPath);

    /// <summary>
    /// Captures the full primary screen and saves it as a PNG file.
    /// </summary>
    Task CaptureFullScreenAsync(string outputPath);
}
