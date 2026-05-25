using System.Net;
using System.Net.Mail;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace ElsaServer.Activities;

[Activity(Category = "RPA.Notification", DisplayName = "Send Verify Alert Email", Description = "發送比對結果通知信。會自動抓取上一個 Visual Verify 節點的結果，不需要任何 UI 變數綁定。")]
public class SendVerifyAlertEmailActivity : CodeActivity
{
    [Input(Description = "收件人信箱 (例如 boss@company.com)")]
    public Input<string> To { get; set; } = default!;

    [Input(Description = "SMTP 伺服器 (選填)。如果沒有填寫，僅會在 Console 印出預覽信件，不會真正寄出。")]
    public Input<string> SmtpHost { get; set; } = default!;

    [Input(Description = "SMTP Port (例如 587)")]
    public Input<int> SmtpPort { get; set; } = new(587);

    [Input(Description = "寄件人信箱與登入帳號")]
    public Input<string> SmtpUser { get; set; } = default!;

    [Input(Description = "SMTP 密碼 或 App Password")]
    public Input<string> SmtpPassword { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<SendVerifyAlertEmailActivity>>();
        
        // 1. 完全自動！不必綁定，直接從共用區掏出結果！
        if (!context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedMatchResult", out var matchObj) || !(matchObj is VisualMatchResult result))
        {
            logger.LogWarning("找不到 SharedMatchResult！請確保這個節點之前有連接著 VisualVerifyActivity。");
            return;
        }

        // 2. 依照結果組裝信件內容
        var subject = result.IsMatch ? "✅ [RPA] 畫面比對成功通知" : "🚨 [RPA] 畫面比對失敗警報";
        var body = result.IsMatch 
            ? $"您好：\n\n系統已順利完成比對！\n目前相似度：{result.Confidence * 100:F2}%\n發現位置：({result.MatchX}, {result.MatchY})"
            : $"您好：\n\n系統發現畫面不一致，請盡速檢查環境狀態！";

        var toEmail = To?.Get(context);
        var host = SmtpHost?.Get(context);

        // 3. 如果沒填寫 SMTP，就進入「模擬發信模式 (Dry Run)」
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation("===============================================");
            logger.LogInformation("✉️ [模擬發信模式] 沒有配置 SMTP，信件內容預覽如下：");
            logger.LogInformation("To: {ToEmail}", toEmail);
            logger.LogInformation("Subject: {Subject}", subject);
            logger.LogInformation("Body: \n{Body}", body);
            logger.LogInformation("===============================================");
            context.SetResult(true);
            return;
        }

        // 4. 有填寫 SMTP，執行真實發信 (使用 System.Net.Mail)
        try
        {
            var port = SmtpPort?.Get(context) ?? 587;
            var user = SmtpUser?.Get(context);
            var pass = SmtpPassword?.Get(context);

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, pass),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(user),
                Subject = subject,
                Body = body
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            logger.LogInformation("✅ 信件已成功透過 {Host} 寄出至 {ToEmail}", host, toEmail);
            context.SetResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "🚨 發信失敗：{Message}", ex.Message);
            context.JournalData.Add("Error", ex.Message);
            context.SetResult(false);
        }
    }
}
