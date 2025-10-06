using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure.Data.Tables;
using System.Net;
using ABCRetails.Models;

namespace ABCRetails.Functions;

public class TableStorageFunction
{
    private readonly ILogger<TableStorageFunction> _logger;
    private readonly TableServiceClient _tableServiceClient;

    public TableStorageFunction(ILogger<TableStorageFunction> logger, TableServiceClient tableServiceClient)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
    }

    [Function("AddEntity")]
    public async Task<HttpResponseData> AddEntity(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var requestData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(requestBody);

            if (requestData == null || !requestData.ContainsKey("tableName"))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            string tableName = requestData["tableName"].GetString() ?? string.Empty;
            var entityJson = requestData["entity"].ToString();

            var tableClient = _tableServiceClient.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();

            // Dynamic entity creation based on table name
            switch (tableName.ToLower())
            {
                case "customers":
                    var customer = JsonSerializer.Deserialize<Customer>(entityJson);
                    if (customer != null)
                    {
                        await tableClient.AddEntityAsync(customer);
                    }
                    break;
                case "products":
                    var product = JsonSerializer.Deserialize<Product>(entityJson);
                    if (product != null)
                    {
                        await tableClient.AddEntityAsync(product);
                    }
                    break;
                case "orders":
                    var order = JsonSerializer.Deserialize<Order>(entityJson);
                    if (order != null)
                    {
                        await tableClient.AddEntityAsync(order);
                    }
                    break;
                default:
                    return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Entity added successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding entity to table");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }

    [Function("GetEntities")]
    public async Task<HttpResponseData> GetEntities(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string tableName = query["tableName"] ?? string.Empty;

            if (string.IsNullOrEmpty(tableName))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var tableClient = _tableServiceClient.GetTableClient(tableName);
            var entities = new List<object>();

            // Query based on table type
            switch (tableName.ToLower())
            {
                case "customers":
                    await foreach (var entity in tableClient.QueryAsync<Customer>())
                    {
                        entities.Add(entity);
                    }
                    break;
                case "products":
                    await foreach (var entity in tableClient.QueryAsync<Product>())
                    {
                        entities.Add(entity);
                    }
                    break;
                case "orders":
                    await foreach (var entity in tableClient.QueryAsync<Order>())
                    {
                        entities.Add(entity);
                    }
                    break;
                default:
                    return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            var jsonResponse = JsonSerializer.Serialize(entities);
            await response.WriteStringAsync(jsonResponse);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entities from table");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }
}