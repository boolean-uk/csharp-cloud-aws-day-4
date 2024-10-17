using Microsoft.AspNetCore.Mvc;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using System.Text.Json;
using InventoryService.Models;
using Npgsql;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

[ApiController]
[Route("[controller]")]
public class OrderController : ControllerBase
{
    private readonly IAmazonSQS _sqs;
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IAmazonEventBridge _eventBridge;
    private readonly string _queueUrl = "https://sqs.eu-north-1.amazonaws.com/637423341661/MalteStengardOrderQueue"; // Format of https://.*
    private readonly string _topicArn = "arn:aws:sns:eu-north-1:637423341661:MalteStengardOrderCreatedTopic"; // Format of arn:aws.*
    private readonly string _connectionString = "your database info";

    public OrderController()
    {
        // Instantiate clients with default configuration
        _sqs = new AmazonSQSClient();
        _sns = new AmazonSimpleNotificationServiceClient();
        _eventBridge = new AmazonEventBridgeClient();

    }

    [HttpGet]
    public async Task<IActionResult> GetOrdersAndProcess()
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 20
        };
        var response = await _sqs.ReceiveMessageAsync(request);

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            foreach (var message in response.Messages)
            {
                try
                {
                    var order = JsonSerializer.Deserialize<Order>(message.Body);

                    // Update order in the database
                    var query = $@"UPDATE orders SET processed = @Processed, total = @Total WHERE orderid = @OrderId";
                    await using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Processed", true);
                        command.Parameters.AddWithValue("@Total", order.Quantity * order.Amount);
                        command.Parameters.AddWithValue("@OrderId", order.OrderId);
                        await command.ExecuteNonQueryAsync();
                    }

                    // Delete message from SQS after processing
                    await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle);
                }
                catch (Exception ex)
                {
                    // Log the exception (consider using a logging framework)
                    Console.WriteLine($"Error processing order {message.Body}: {ex.Message}");
                }
            }
        }
        return Ok(new { Status = $"{response.Messages.Count()} Order(s) have been processed" });
    }


    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] Order order)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var query = "INSERT INTO Orders (product, quantity, amount, processed, total) VALUES (@Product, @Quantity, @Amount, @Processed, @Total)";
            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@Product", order.Product);
                command.Parameters.AddWithValue("@Quantity", order.Quantity);
                command.Parameters.AddWithValue("@Amount", order.Amount);
                command.Parameters.AddWithValue("@Processed", order.Processed);
                command.Parameters.AddWithValue("@Total", order.Total);
                await command.ExecuteNonQueryAsync();
            }
        }
        /*
         * AmazonSimpleNotificationServiceClient
         * 
         */
        // Publish to SNS
        var message = JsonSerializer.Serialize(order);
        var publishRequest = new PublishRequest
        {
            TopicArn = _topicArn,
            Message = message
        };

        await _sns.PublishAsync(publishRequest);


        /*
         * AmazonEventBridgeClient
         * 
         */
        // Publish to EventBridge
        var eventEntry = new PutEventsRequestEntry
        {
            Source = "order.service",
            DetailType = "OrderCreated",
            Detail = JsonSerializer.Serialize(order),
            EventBusName = "CustomEventBus"
        };

        var putEventsRequest = new PutEventsRequest
        {
            Entries = new List<PutEventsRequestEntry> { eventEntry }
        };

        await _eventBridge.PutEventsAsync(putEventsRequest);

        return Ok(new { Status = "Order Created, Message Published to SNS and Event Emitted to EventBridge" });
    }
}