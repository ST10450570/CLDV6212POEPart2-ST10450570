using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System.Net;
using System.Text.Json;

namespace ABCRetails.Functions;

public class BlobStorageFunction
{
    private readonly ILogger<BlobStorageFunction> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageFunction(ILogger<BlobStorageFunction> logger, BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
    }

    [Function("UploadBlob")]
    public async Task<HttpResponseData> UploadBlob(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string containerName = query["containerName"] ?? "uploads";

            // For isolated worker, we need to handle multipart form data differently
            var formData = await req.ReadFromJsonAsync<Dictionary<string, string>>();

            if (formData == null || !formData.ContainsKey("fileName") || !formData.ContainsKey("fileData"))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            string fileName = formData["fileName"];
            string fileData = formData["fileData"];

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            var blobName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
            var blobClient = containerClient.GetBlobClient(blobName);

            // Convert base64 string back to bytes
            var fileBytes = Convert.FromBase64String(fileData);
            await using var stream = new MemoryStream(fileBytes);
            await blobClient.UploadAsync(stream, overwrite: true);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(blobClient.Uri.ToString());
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading blob");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }
}