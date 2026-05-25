using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ElsaServer.Activities;

[Activity(Category = "RPA.Vision", DisplayName = "Start Playwright Session", Description = "開啟 Playwright 瀏覽器並將供後續節點操作的分頁存放於共用狀態中。")]
public class StartPlaywrightSessionActivity : CodeActivity<bool>
{
    [Input(Description = "欲前往的初始網址")]
    public Input<string> Url { get; set; } = default!;

    [Input(Description = "Playwright 逾時時間 (毫秒)，預設 30000ms")]
    public Input<int> TimeoutMs { get; set; } = new(30000);

    [Input(Description = "是否以 Headless 背景模式執行。預設為 true")]
    public Input<bool> Headless { get; set; } = new(false);
    [Input(Description = "接收從 ReceiveMacScreenshotActivity 傳遞過來的截圖資料 (選填)")]
    public Input<MacScreenshotData> ScreenshotData { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var url = Url.Get(context);
        var timeoutMs = TimeoutMs.Get(context);
        var headless = Headless.Get(context);
        var logger = context.GetRequiredService<ILogger<StartPlaywrightSessionActivity>>();
        
        // 先嘗試從 UI 綁定的 Input 變數中取得
        var macData = ScreenshotData?.Get(context);
        
        // 如果 UI 上沒有綁定而取得 null，則嘗試從共用記憶體中撈取
        if (macData == null && context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedMacData", out var sharedDataObj) && sharedDataObj is MacScreenshotData sharedMacData)
        {
            macData = sharedMacData;
        }
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogError("Url is empty.");
            context.SetResult(false);
            return;
        }

        try
        {
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless,
                Timeout = timeoutMs
            });

            var browserContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1600, Height = 1024 }
            });
            var page = await browserContext.NewPageAsync();
            page.SetDefaultTimeout(timeoutMs);

            logger.LogInformation("Start Session - 正在前往: {Url}", url);

            await page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = timeoutMs,
                WaitUntil = WaitUntilState.Load
            });

            // 存放至共用記憶體供後續節點使用
            context.WorkflowExecutionContext.TransientProperties["SharedPlaywright"] = playwright;
            context.WorkflowExecutionContext.TransientProperties["SharedBrowser"] = browser;
            context.WorkflowExecutionContext.TransientProperties["SharedPage"] = page;
            
            logger.LogInformation("Start Session 成功");
            context.SetResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Start Session 失敗: {Message}", ex.Message);
            context.JournalData.Add("Error", ex.Message);
            context.SetResult(false);
            
            // 例外發生立即嘗試把已開啟的資源清掉，但可能有些已經 null
            if (context.WorkflowExecutionContext.TransientProperties.ContainsKey("SharedPage"))
            {
                try { await ((IPage)context.WorkflowExecutionContext.TransientProperties["SharedPage"]).CloseAsync(); } catch { }
            }
            if (context.WorkflowExecutionContext.TransientProperties.ContainsKey("SharedBrowser"))
            {
                try { await ((IBrowser)context.WorkflowExecutionContext.TransientProperties["SharedBrowser"]).DisposeAsync(); } catch { }
            }
        }
    }
}
