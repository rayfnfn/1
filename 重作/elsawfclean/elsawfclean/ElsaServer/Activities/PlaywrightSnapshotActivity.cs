using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.IO;
using System.Xml.Linq;

namespace ElsaServer.Activities;

[Activity(Category = "RPA.Vision", DisplayName = "Playwright Snapshot", Description = "使用現有的 Playwright Session 截取指定 DOM 元素或全畫面的圖片，並回傳位元組陣列 (byte[])。此節點不負責驗證。")]
public class PlaywrightSnapshotActivity : CodeActivity<byte[]>
{
    [Input(Description = "想要擷取的 DOM 元素 (例如 CSS 或 XPath 選擇器)，如果沒有則擷取全畫面或指定座標。")]
    public Input<string> Selector { get; set; } = default!;

    [Input(Description = "設定擷圖的 X 座標 (選填，搭配 Y, Width, Height 使用。如果有給定 Selector，則忽略此欄位。)", DefaultValue = 0)]
    public Input<int> ClipX { get; set; } = new(0);

    [Input(Description = "設定擷圖的 Y 座標 (選填)", DefaultValue = 0)]
    public Input<int> ClipY { get; set; } = new(0);

    [Input(Description = "設定擷圖的寬度 (選填，若有給大於 0 的寬高才會進行局部截圖，否則全頁面擷取)", DefaultValue = 0)]
    public Input<int> ClipWidth { get; set; } = new(0);

    [Input(Description = "設定擷圖的高度 (選填)", DefaultValue = 0)]
    public Input<int> ClipHeight { get; set; } = new(0);
    [Output(Description = "爬取後的截圖資料")]
    public Output<byte[]> OutputData { get; set; } = default!;


    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<PlaywrightSnapshotActivity>>();
        // 防呆: 確保如果 Selector 本身未被初始化或未填寫，不會擲出例外，並安全地收到 null
        var selector = Selector?.Get(context);
        var screenshotData = new MacScreenshotData();
        if (context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedMacData", out var sharedDataObj) && sharedDataObj is MacScreenshotData sharedMacData)
        {
            screenshotData = sharedMacData;
        }

        // 當欄位取出來是 null，或者取出來的數字為 0 時，一律Fallback到 screenshotData 裡的值
        var clipX = ClipX?.Get(context) is int x && x != 0 ? x : screenshotData.X;
        var clipY = (ClipY?.Get(context) is int y && y != 0 ? y : screenshotData.Y)-85;
        var clipW = ClipWidth?.Get(context) is int w && w != 0 ? w : screenshotData.Width;
        var clipH = ClipHeight?.Get(context) is int h && h != 0 ? h : screenshotData.Height;

        if (!context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedPage", out var pageObj) || !(pageObj is IPage page))
        {
            var msg = "找不到已建立的 Playwright Page。請確保之前已執行 Start Playwright Session 節點。";
            logger.LogError(msg);
            context.JournalData.Add("Error", msg);
            context.SetResult(new byte[0]);
            return;
        }

        byte[] crawledImageBytes;

        try
        {
            // 截圖邏輯
            if (!string.IsNullOrWhiteSpace(selector))
            {
                var locatorScreenshotOptions = new LocatorScreenshotOptions
                {
                    Type = ScreenshotType.Png
                };
                crawledImageBytes = await page.Locator(selector).ScreenshotAsync(locatorScreenshotOptions);
            }
            else
            {
                var screenshotOptions = new PageScreenshotOptions
                {
                    Type = ScreenshotType.Png
                };

                if (clipW > 0 && clipH > 0)
                {
                    screenshotOptions.Path= GetPath();
                    screenshotOptions.Clip = new Clip
                    {
                        X = clipX,
                        Y = clipY,
                        Width = clipW,
                        Height = clipH
                    };
                }
                else
                {
                    screenshotOptions.FullPage = true;
                }

                crawledImageBytes = await page.ScreenshotAsync(screenshotOptions);
            }
            logger.LogInformation("成功截取圖片。大小: {Length} bytes", crawledImageBytes.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Playwright 爬取截圖失敗: {Message}", ex.Message);
            context.JournalData.Add("Error", $"Playwright 爬取截圖失敗: {ex.Message}");
            context.SetResult(new byte[0]);
            return;
        }
        // 1. 保留原本的正規輸出，以應付有人想手動綁定的情況
        context.Set(OutputData, crawledImageBytes);
        context.SetResult(crawledImageBytes);

        // 2. 放入隱藏共用區！讓後面的 VisualVerify 節點不用您在 UI 拉線就可以自己直接拿
        context.WorkflowExecutionContext.TransientProperties["SharedPlaywrightImage"] = crawledImageBytes;
    }

    private static string GetPath()
    {
        var folderPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "playwright_crawler");
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }

        string fileName = $"{System.DateTime.Now:yyyyMMddHHmmssfff}.png";
        string filePath = Path.Combine(folderPath, fileName);
        return filePath;
    }
}
public class CrawlerScreenshotData
{
   //

}
