using Microsoft.AspNetCore.Mvc;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using System.Text.Json;
using InventoryService.Models;

[ApiController]
[Route("[controller]")]
public class OrderController : ControllerBase
{
    private readonly IAmazonSQS _sqs;
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IAmazonEventBridge _eventBridge;
    private readonly string _queueUrl = "https://sqs.eu-north-1.amazonaws.com/637423341661/MalteStengardOrderQueue"; // Format of https://.*
    private readonly string _topicArn = "arn:aws:sns:eu-north-1:637423341661:MalteStengardOrderCreatedTopic"; // Format of arn:aws.*

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
        /*
         * AmazonSQSClient
         * 
         */
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
            // Process order (e.g., update inventory)

            // Delete message after processing
            await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle);
        }
        return Ok(new { Status = $"{response.Messages.Count()}Order have been processed" });
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] Order order)
    {
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