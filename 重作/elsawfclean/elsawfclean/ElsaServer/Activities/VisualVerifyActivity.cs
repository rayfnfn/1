using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using ElsaServer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ElsaServer.Activities;

public class VisualMatchResult
{
    public bool IsMatch { get; set; }
    public double Confidence { get; set; }
    public int MatchX { get; set; }
    public int MatchY { get; set; }
}

[Activity(Category = "RPA.Vision", DisplayName = "Visual Verify", Description = "驗證傳入的截圖流是否包含目標圖片，或是否與目標圖片吻合。")]
public class VisualVerifyActivity : CodeActivity
{
    [Output(Description = "比對結果")]
    public Output<VisualMatchResult> OutputData { get; set; } = default!;

    [Input(Description = "截圖工具回傳的二進制圖片流 (Byte Array 或 Base64 字串)")]
    public Input<byte[]> SourceImageStream { get; set; } = default!;

    [Input(Description = "要比對的目標圖片 (Template Image)")]
    public Input<byte[]> TargetImageStream { get; set; } = default!;

    [Input(Description = "截圖時的相對座標 (X, Y, Width, Height) - 供同事紀錄與後續確認使用")]
    public Input<string> RelativeCoordinates { get; set; } = default!;

    [Input(Description = "信心閾值 (0.0 ~ 1.0)，例如 0.95 代表需要 95% 相似度(SSIM)才算成功")]
    public Input<double> Threshold { get; set; } = new(0.95);

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var targetObj = TargetImageStream?.Get(context);
        var sourceObj = SourceImageStream?.Get(context);
        var threshold = Threshold?.Get(context) ?? 0.95;

        var targetBytes = GetBytes(targetObj);
        var sourceBytes = GetBytes(sourceObj);

        // 防呆：如果沒有從 UI 綁定拿到 Target 或 Source 圖片，自動從共用記憶體裡抓！
        // 抓取 Mac 傳來的 App 截圖
        if (context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedMacData", out var sharedObj) && sharedObj is MacScreenshotData macData)
        {
            if (targetBytes == null) targetBytes = macData.ImageStream;
            else if (sourceBytes == null) sourceBytes = macData.ImageStream;
        }

        // 抓取 Playwright 的爬蟲截圖
        if (context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedPlaywrightImage", out var pwObj) && pwObj is byte[] pwImage)
        {
            if (targetBytes == null) targetBytes = pwImage;
            else if (sourceBytes == null) sourceBytes = pwImage;
        }

        if (targetBytes == null || sourceBytes == null)
        {
            SetExpectedResult(context, false, 0);
            return;
        }

        var imageComparer = context.GetRequiredService<IImageComparer>();

        try
        {
            // 委派給獨立的 Image Comparer 服務 (例如 ImageSharpComparer 或未來的 OpenCV 實作)
            var matchResult = imageComparer.Compare(targetBytes, sourceBytes, threshold);
            
            // 直接將返回的結果回填至輸出
            context.Set(OutputData, matchResult);
            // 放進共用暫存區，讓下一個自訂的發信節點直接抓取，免除 UI 綁定困擾
            context.WorkflowExecutionContext.TransientProperties["SharedMatchResult"] = matchResult;
        }
        catch (Exception)
        {
            context.Set(OutputData, new VisualMatchResult { IsMatch = false, Confidence = 0 });
        }
    }

    private void SetExpectedResult(ActivityExecutionContext context, bool isMatch, double similarity)
    {
        context.Set(OutputData, new VisualMatchResult 
        { 
            IsMatch = isMatch, 
            Confidence = similarity,
            MatchX = 0,
            MatchY = 0
        });
    }

    private byte[]? GetBytes(object? obj)
    {
        if (obj is byte[] bytes) return bytes;
        if (obj is string base64)
        {
            if (base64.Contains(",")) base64 = base64.Split(',')[1];
            try { return Convert.FromBase64String(base64); } catch { return null; }
        }
        return null;
    }
}
