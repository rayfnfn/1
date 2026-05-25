using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace RegionSelector.Services;

public class WorkflowApiService : IWorkflowApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _workflowApiUrl;

    public WorkflowApiService(string apiUrl)
    {
        var handler = new HttpClientHandler
        {
            // 無論憑證發生什麼錯誤 (sslPolicyErrors)，都強制回傳 true 代表驗證通過
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };
        _httpClient = new HttpClient(handler);
        _workflowApiUrl = apiUrl;
    }

    public async Task UploadImageAsync(string imageFilePath)
    {
        if (!File.Exists(imageFilePath))
        {
            throw new FileNotFoundException("Image file not found.", imageFilePath);
        }

        // Elsa 3.0 workflow accepting binary stream.
        // We will send it as a raw binary POST request.
        using var fileStream = File.OpenRead(imageFilePath);
        using var streamContent = new StreamContent(fileStream);

        // You might need to adjust the Content-Type based on the explicit Elsa trigger requirement
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        try
        {
            
        }
        catch (System.Exception)
        {
            
            throw;
        }
        var response = await _httpClient.PostAsync(_workflowApiUrl, streamContent);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to upload to workflow API. Status: {response.StatusCode}. Response: {content}");
        }
    }
}
