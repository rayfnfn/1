using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RegionSelector.Services;

public class MacScreenCaptureService : IScreenCaptureService
{
    public Task CaptureRegionAsync(int x, int y, int width, int height, string outputPath)
    {
        // MacOS screencapture uses typical screen coordinates. 
        // We use the -x flag to disable sound, -R for region.
        var args = $"-x -R {x},{y},{width},{height} \"{outputPath}\"";
        return RunProcessAsync("screencapture", args);
    }

    public Task CaptureFullScreenAsync(string outputPath)
    {
        // -m captures only the main monitor, -x disables sound
        var args = $"-x -m \"{outputPath}\"";
        return RunProcessAsync("screencapture", args);
    }

    private async Task RunProcessAsync(string filename, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0)
        {
            throw new Exception($"Screen capture failed with exit code {process.ExitCode}");
        }
    }
}
