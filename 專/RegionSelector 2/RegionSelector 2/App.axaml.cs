using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SharpHook;
using SharpHook.Data;
using System;
using System.IO;
using System.Threading.Tasks;
using RegionSelector.Services;

namespace RegionSelector;

public partial class App : Application
{
    private IGlobalHook? _hook;
    private MainWindow? _mainWindow;
    private readonly IScreenCaptureService _screenCaptureService = ScreenCaptureFactory.CreateService();
    //將圖片傳入Elsa 3.0 Workflow API的服務實例，請替換為實際的URL
    private readonly IWorkflowApiService _workflowApiService = new WorkflowApiService("https://localhost:7238/api/workflows/screenshot"); // Please replace with your actual Elsa Workflow URL

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            // We instantiate MainWindow but don't set desktop.MainWindow so Avalonia doesn't Auto-Show it on start.
            // When we actually show it later, we can set desktop.MainWindow if needed.
            _mainWindow = new MainWindow();
            
            _hook = new TaskPoolGlobalHook();
            _hook.KeyPressed += OnKeyPressed;
            Task.Run(() => _hook.RunAsync());
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        // Ctrl + 4
        if (e.Data.KeyCode == KeyCode.Vc4 && (e.RawEvent.Mask.HasFlag(EventMask.LeftCtrl) || e.RawEvent.Mask.HasFlag(EventMask.RightCtrl)))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_mainWindow != null && !_mainWindow.IsVisible)
                {
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow == null)
                    {
                        desktop.MainWindow = _mainWindow;
                    }
                    _mainWindow.Show();
                    _mainWindow.Activate();
                }
            });
        }
        // Ctrl + 5
        else if (e.Data.KeyCode == KeyCode.Vc5 && (e.RawEvent.Mask.HasFlag(EventMask.LeftCtrl) || e.RawEvent.Mask.HasFlag(EventMask.RightCtrl)))
        {
            Dispatcher.UIThread.Post(async () =>
            {
                if (_mainWindow != null)
                {
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow == null)
                    {
                        desktop.MainWindow = _mainWindow;
                    }
                    await CaptureFullScreenAsync();
                }
            });
        }
    }

    private async Task CaptureFullScreenAsync()
    {
        var screen = _mainWindow?.Screens.Primary;
        if (screen != null)
        {
            int x = screen.Bounds.X;
            int y = screen.Bounds.Y;
            int width = screen.Bounds.Width;
            int height = screen.Bounds.Height;
            string result = $"{x},{y},{width},{height}";
            
            // To access clipboard properly, ensure the window is somewhat initialized. 
            // If it's never been shown, clipboard might work or might not. We test it first.
            var clipboard = TopLevel.GetTopLevel(_mainWindow)?.Clipboard;
        
            if (clipboard == null) 
            {
                // Fallback: silently show and hide to initialize handle
                _mainWindow?.Opacity = 0;
                _mainWindow?.Show();
                clipboard = TopLevel.GetTopLevel(_mainWindow)?.Clipboard;
                _mainWindow?.Hide();
                _mainWindow!.Opacity = 1;
            }

            if (clipboard != null)
            {
                await clipboard.SetTextAsync(result);
            }

            try
            {
                var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                var folderPath = Path.Combine(picturesPath, "RegionSelector");
                Directory.CreateDirectory(folderPath);
                
                var fileName = $"screenshot_full_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filePath = Path.Combine(folderPath, fileName);
                
                await _screenCaptureService.CaptureFullScreenAsync(filePath);
                await _workflowApiService.UploadImageAsync(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to capture and upload full screen: {ex}");
            }
        }
    }
}