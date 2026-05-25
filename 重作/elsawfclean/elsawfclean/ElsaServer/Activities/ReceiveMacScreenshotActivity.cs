using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Microsoft.AspNetCore.Http;

namespace ElsaServer.Activities;

public class MacScreenshotData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] ImageStream { get; set; } = default!;
}

[Activity(Category = "RPA.Vision", DisplayName = "Receive Mac Screenshot", Description = "負責解析傳入的 HTTP 請求，提取出 Mac 截圖工具發送的『相對座標』與『二進制圖片流』。")]
public class ReceiveMacScreenshotActivity : CodeActivity
{
    [Output(Description = "解析後的截圖資料與座標物件")]
    public Output<MacScreenshotData> OutputData { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var httpContextAccessor = context.GetRequiredService<IHttpContextAccessor>();
        var request = httpContextAccessor.HttpContext?.Request;

        if (request == null)
        {
            throw new Exception("無法取得 HTTP Request 內容。");
        }

        // 解析 Query String 上的相對座標 (如網址帶有 ?x=100&y=100&w=200&h=200)
        int x = int.TryParse(request.Query["x"], out var parsedX) ? parsedX : 0;
        int y = int.TryParse(request.Query["y"], out var parsedY) ? parsedY : 0;
        int width = int.TryParse(request.Query["w"], out var parsedW) ? parsedW : 100;
        int height = int.TryParse(request.Query["h"], out var parsedH) ? parsedH : 100;

        // 讀取 Body 內的純二進制圖片流 (Binary Stream, application/octet-stream)
        using var memoryStream = new MemoryStream();
        if (request.Body != null && request.Body.CanRead)
        {
            await request.Body.CopyToAsync(memoryStream);
        }
        var imageBytes = memoryStream.ToArray();

        var macData = new MacScreenshotData
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            ImageStream = imageBytes
        };

        // 做法一：封裝成結構化物件輸出 (需搭配 Elsa UI 綁定變數才能被下個節點接收)
        context.Set(OutputData, macData);

        // 做法二：放入共用記憶體 (與 SharedPage 概念相同)，下個節點不需在 UI 設定即可直接讀取！
        context.WorkflowExecutionContext.TransientProperties["SharedMacData"] = macData;
    }
}
