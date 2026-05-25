using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using RegionSelector.Services;

namespace RegionSelector;

public partial class MainWindow : Window
{
    private Point _startPoint;
    private bool _isDragging;
    private readonly IScreenCaptureService _screenCaptureService = ScreenCaptureFactory.CreateService();
    private readonly IWorkflowApiService _workflowApiService = new WorkflowApiService("https://localhost:7238/api/workflows/screenshot");

    public MainWindow()
    {
        InitializeComponent();
    }

    // ====== 加入這一段：視窗開啟時，手動覆蓋全螢幕 ======
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 取得目前的主要螢幕
        var screen = Screens.Primary;
        if (screen != null)
        {
            // 將視窗移動到螢幕的最左上角 (0, 0)
            Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);
            
            // 將視窗的寬高設定為螢幕的實際寬高
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;
        }
    }
    // ===================================================

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pointerPoint = e.GetCurrentPoint(MainCanvas);
        if (pointerPoint.Properties.IsLeftButtonPressed)
        {
            _startPoint = pointerPoint.Position;
            _isDragging = true;
            SelectionBox.IsVisible = true;

            Canvas.SetLeft(SelectionBox, _startPoint.X);
            Canvas.SetTop(SelectionBox, _startPoint.Y);
            SelectionBox.Width = 0;
            SelectionBox.Height = 0;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(MainCanvas);

        var x = Math.Min(currentPoint.X, _startPoint.X);
        var y = Math.Min(currentPoint.Y, _startPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionBox, x);
        Canvas.SetTop(SelectionBox, y);
        SelectionBox.Width = width;
        SelectionBox.Height = height;
    }

    private async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            
            int x = (int)Canvas.GetLeft(SelectionBox);
            int y = (int)Canvas.GetTop(SelectionBox);
            int width = (int)SelectionBox.Width;
            int height = (int)SelectionBox.Height;

            string result = $"{x},{y},{width},{height}";

            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(result);
            }

            try
            {
                var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                var folderPath = Path.Combine(picturesPath, "RegionSelector");
                Directory.CreateDirectory(folderPath);
                
                var fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filePath = Path.Combine(folderPath, fileName);

                // Hide the window so the transparent selection mask is not inside the screenshot
                Hide();
                await Task.Delay(100);

                await _screenCaptureService.CaptureRegionAsync(x, y, width, height, filePath);
                await _workflowApiService.UploadImageAsync(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to capture region: {ex.InnerException}");
            }
        }
    }
}