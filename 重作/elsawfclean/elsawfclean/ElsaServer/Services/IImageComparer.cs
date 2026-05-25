using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ElsaServer.Activities; // 為了使用 VisualMatchResult 類別

namespace ElsaServer.Services;

/// <summary>
/// 影像比對服務介面
/// </summary>
public interface IImageComparer
{
    /// <summary>
    /// 比較兩張圖片位元組並計算相似度
    /// </summary>
    /// <param name="targetBytes">目標圖片</param>
    /// <param name="sourceBytes">來源圖片 (截圖)</param>
    /// <param name="threshold">信心閾值</param>
    /// <returns>回傳比對結果物件</returns>
    VisualMatchResult Compare(byte[] targetBytes, byte[] sourceBytes, double threshold);
}

/// <summary>
/// 使用 ImageSharp 進行影像比對的實作
/// </summary>
public class ImageSharpComparer : IImageComparer
{
    public VisualMatchResult Compare(byte[] targetBytes, byte[] sourceBytes, double threshold)
    {
        try
        {
            using var targetStream = new System.IO.MemoryStream(targetBytes);
            using var sourceStream = new System.IO.MemoryStream(sourceBytes);

            // 進行比對
            var result = Codeuctivity.ImageSharpCompare.ImageSharpCompare.CalcDiff(targetStream, sourceStream);

            // Codeuctivity.ImageSharpCompare 沒有內建提供 SSIM 屬性，
            // 它提供的是 PixelErrorPercentage (0%~100%，0代表完全相同)。
            // 我們可以將它轉換成 0~1 的相似度分數。
            double similarity = 1.0 - (result.PixelErrorPercentage / 100.0);

            bool isMatch = similarity >= threshold;

            return new VisualMatchResult
            {
                IsMatch = isMatch,
                Confidence = similarity,
                MatchX = 0,
                MatchY = 0
            };
        }
        catch (Codeuctivity.ImageSharpCompare.ImageSharpCompareException)
        {
            // 例如長寬不符等錯誤
            return new VisualMatchResult { IsMatch = false, Confidence = 0 };
        }
        catch (Exception)
        {
            return new VisualMatchResult { IsMatch = false, Confidence = 0 };
        }
    }
}
