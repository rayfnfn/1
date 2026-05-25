using System.Threading.Tasks;

namespace RegionSelector.Services;

public interface IWorkflowApiService
{
    /// <summary>
    /// Uploads an image file to the Elsa 3.0 workflow API.
    /// </summary>
    /// <param name="imageFilePath">The local path to the PNG image to upload.</param>
    Task UploadImageAsync(string imageFilePath);
}
