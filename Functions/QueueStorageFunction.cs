using System.Net;
using System.Text.Json;
using ABCRetails.Models;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ABCRetails.Functions;

public class QueueStorageFunction
{
    private readonly ILogger<QueueStorageFunction> _logger;
    private readonly TableServiceClient _tableServiceClient;
    private readonly QueueServiceClient _queueServiceClient;

    public QueueStorageFunction(
        ILogger<QueueStorageFunction> logger,
        TableServiceClient tableServiceClient,
        QueueServiceClient queueServiceClient)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
        _queueServiceClient = queueServiceClient;
    }

    [Function("ProcessOrderQueue")]
    public async Task ProcessOrderQueue(
        [QueueTrigger("orders", Connection = "AzureWebJobsStorage")] string queueMessage)
    {
        try
        {
            _logger.LogInformation($"Processing order from queue: {queueMessage}");

            var orderData = JsonSerializer.Deserialize<Order>(queueMessage);
            if (orderData != null)
            {
                var tableClient = _tableServiceClient.GetTableClient("Orders");
                await tableClient.CreateIfNotExistsAsync();

                // Ensure proper keys are set
                orderData.PartitionKey = "Order";
                orderData.RowKey = Guid.NewGuid().ToString();

                await tableClient.AddEntityAsync(orderData);
                _logger.LogInformation($"Order {orderData.RowKey} added to table storage");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order from queue");
        }
    }

    [Function("SendQueueMessage")]
    public async Task<HttpResponseData> SendQueueMessage(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string queueName = query["queueName"] ?? string.Empty;

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrEmpty(queueName))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            await queueClient.CreateIfNotExistsAsync();
            await queueClient.SendMessageAsync(requestBody);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"Message sent to queue: {queueName}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to queue");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }
}