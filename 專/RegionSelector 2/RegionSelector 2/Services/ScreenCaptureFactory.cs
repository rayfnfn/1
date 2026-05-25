using System;

namespace RegionSelector.Services;

public static class ScreenCaptureFactory
{
    public static IScreenCaptureService CreateService()
    {
        if (OperatingSystem.IsMacOS())
        {
            return new MacScreenCaptureService();
        }
        else if (OperatingSystem.IsWindows())
        {
            return new WindowsScreenCaptureService();
        }

        throw new PlatformNotSupportedException("Currently only macOS and Windows are supported.");
    }
}
