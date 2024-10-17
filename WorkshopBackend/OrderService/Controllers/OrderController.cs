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

[ApiController]
[Route("[controller]")]
public class OrderController : ControllerBase
{
    private readonly IAmazonSQS _sqs;
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IAmazonEventBridge _eventBridge;
    private readonly string _queueUrl = "https://sqs.eu-north-1.amazonaws.com/637423341661/VictorChicinasOrderQueue"; // Format of https://.*
    private readonly string _topicArn = "arn:aws:sns:eu-north-1:637423341661:VictorChicinasOrderCreatedTopic"; // Format of arn:aws.*
    private readonly string _connectionString = "Host=aaaa-database-2-victorchicinas.cr4y6a6oo1h9.eu-north-1.rds.amazonaws.com;Port=5432;Database=aaaa-database-2-victorchicinas;User Id=postgres;Password=NewPasswordToAWS";

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
        try
        {
            // Receive messages from the SQS queue
            var request = new ReceiveMessageRequest
            {
                QueueUrl = _queueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 20
            };

            var response = await _sqs.ReceiveMessageAsync(request);

            foreach (var message in response.Messages)
            {
                var order = JsonSerializer.Deserialize<Order>(message.Body);

                // Process order: Calculate total and mark as processed
                order.Total = order.Quantity * order.Amount;
                order.Processed = true;

                // Update the order in the database
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "UPDATE Orders SET total = @Total, processed = @Processed WHERE orderId = @OrderId";
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Total", order.Total);
                        command.Parameters.AddWithValue("@Processed", order.Processed);
                        command.Parameters.AddWithValue("@OrderId", order.OrderId);
                        await command.ExecuteNonQueryAsync();
                    }
                }

                // Delete message after processing
                await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle);
            }

            return Ok(new { Status = $"{response.Messages.Count()} Orders have been processed" });
        }
        catch (Exception ex)
        {
            // Log the exception (you can use a logging framework here)
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] Order order)
    {
        try
        {
            // Insert the order into the database
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "INSERT INTO Orders (product, quantity, amount, processed, total) VALUES (@Product, @Quantity, @Amount, @Processed, @Total)";
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Product", order.Product);
                    command.Parameters.AddWithValue("@Quantity", order.Quantity);
                    command.Parameters.AddWithValue("@Amount", order.Amount);
                    command.Parameters.AddWithValue("@Processed", order.Processed ?? false); 
                    command.Parameters.AddWithValue("@Total", order.Quantity * order.Amount);
                    await command.ExecuteNonQueryAsync();
                }
            }

            // Publish the order to SNS
            var message = JsonSerializer.Serialize(order);
            var publishRequest = new PublishRequest
            {
                TopicArn = _topicArn,
                Message = message
            };
            await _sns.PublishAsync(publishRequest);

            // Send an event to EventBridge
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
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}