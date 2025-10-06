using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Files.Shares;
using System.Net;
using System.Text.Json;

namespace ABCRetails.Functions;

public class FileShareFunction
{
    private readonly ILogger<FileShareFunction> _logger;
    private readonly ShareServiceClient _shareServiceClient;

    public FileShareFunction(ILogger<FileShareFunction> logger, ShareServiceClient shareServiceClient)
    {
        _logger = logger;
        _shareServiceClient = shareServiceClient;
    }

    [Function("UploadToFileShare")]
    public async Task<HttpResponseData> UploadToFileShare(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string shareName = query["shareName"] ?? "files";
            string directoryName = query["directoryName"] ?? "uploads";

            var formData = await req.ReadFromJsonAsync<Dictionary<string, string>>();

            if (formData == null || !formData.ContainsKey("fileName") || !formData.ContainsKey("fileData"))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            string fileName = formData["fileName"];
            string fileData = formData["fileData"];

            var shareClient = _shareServiceClient.GetShareClient(shareName);
            await shareClient.CreateIfNotExistsAsync();

            var directoryClient = shareClient.GetDirectoryClient(directoryName);
            await directoryClient.CreateIfNotExistsAsync();

            var fileClient = directoryClient.GetFileClient(fileName);

            // Convert base64 string back to bytes
            var fileBytes = Convert.FromBase64String(fileData);
            await fileClient.CreateAsync(fileBytes.Length);
            await using var stream = new MemoryStream(fileBytes);
            await fileClient.UploadAsync(stream);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"File uploaded successfully: {fileName}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to file share");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }
}