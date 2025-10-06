using System.Text.Json;
using ABCRetails.Models;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;

namespace ABCRetails.Services;

public class FunctionStorageService : IAzureStorageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FunctionStorageService> _logger;

    public FunctionStorageService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FunctionStorageService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private string FunctionBaseUrl => _configuration["AzureFunctions:BaseUrl"];
    private string FunctionKey => _configuration["AzureFunctions:Key"];

    public async Task<T> AddEntityAsync<T>(T entity) where T : class, ITableEntity
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            var tableName = typeof(T).Name switch
            {
                nameof(Customer) => "Customers",
                nameof(Product) => "Products",
                nameof(Order) => "Orders",
                _ => typeof(T).Name + "s"
            };

            // For orders, use queue instead of direct table write
            if (typeof(T) == typeof(Order))
            {
                await SendMessageAsync("orders", JsonSerializer.Serialize(entity));
                return entity;
            }

            var requestData = new
            {
                tableName = tableName,
                entity = entity
            };

            var response = await httpClient.PostAsJsonAsync(
                $"{FunctionBaseUrl}/api/AddEntity?code={FunctionKey}",
                requestData);

            if (response.IsSuccessStatusCode)
            {
                return entity;
            }

            throw new Exception($"Failed to add entity: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding entity via function");
            throw;
        }
    }

    public async Task<List<T>> GetAllEntitiesAsync<T>() where T : class, ITableEntity, new()
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            var tableName = typeof(T).Name switch
            {
                nameof(Customer) => "Customers",
                nameof(Product) => "Products",
                nameof(Order) => "Orders",
                _ => typeof(T).Name + "s"
            };

            var response = await httpClient.GetAsync(
                $"{FunctionBaseUrl}/api/GetEntities?code={FunctionKey}&tableName={tableName}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<T>>(content) ?? new List<T>();
            }

            throw new Exception($"Failed to get entities: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entities via function");
            throw;
        }
    }

    public async Task<string> UploadImageAsync(IFormFile file, string containerName)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            using var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(containerName), "containerName");

            if (file != null)
            {
                formData.Add(new StreamContent(file.OpenReadStream()), "file", file.FileName);
            }

            var response = await httpClient.PostAsync(
                $"{FunctionBaseUrl}/api/UploadBlob?code={FunctionKey}",
                formData);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            throw new Exception($"Failed to upload image: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image via function");
            throw;
        }
    }

    public async Task SendMessageAsync(string queueName, string message)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            var response = await httpClient.PostAsync(
                $"{FunctionBaseUrl}/api/SendQueueMessage?code={FunctionKey}&queueName={queueName}",
                new StringContent(message));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to send message: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message via function");
            throw;
        }
    }

    public async Task<string> UploadToFileShareAsync(IFormFile file, string shareName, string directoryName = "")
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            using var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(shareName), "shareName");
            formData.Add(new StringContent(directoryName), "directoryName");

            if (file != null)
            {
                formData.Add(new StreamContent(file.OpenReadStream()), "file", file.FileName);
            }

            var response = await httpClient.PostAsync(
                $"{FunctionBaseUrl}/api/UploadToFileShare?code={FunctionKey}",
                formData);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            throw new Exception($"Failed to upload to file share: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading to file share via function");
            throw;
        }
    }

    // Implement remaining interface methods with function calls
    public Task<T?> GetEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity, new()
    {
        // Implementation would query all and filter
        throw new NotImplementedException();
    }

    public Task<T> UpdateEntityAsync<T>(T entity) where T : class, ITableEntity
    {
        throw new NotImplementedException();
    }

    public Task DeleteEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity, new()
    {
        throw new NotImplementedException();
    }

    public Task<string> UploadFileAsync(IFormFile file, string containerName)
    {
        return UploadImageAsync(file, containerName);
    }

    public Task DeleteBlobAsync(string blobName, string containerName)
    {
        throw new NotImplementedException();
    }

    public Task<string?> ReceiveMessageAsync(string queueName)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> DownloadFromFileShareAsync(string shareName, string fileName, string directoryName = "")
    {
        throw new NotImplementedException();
    }
}