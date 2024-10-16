using Microsoft.AspNetCore.Mvc;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using System.Text.Json;
using InventoryService.Models;
using OrderService.Context;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("[controller]")]
public class OrderController : ControllerBase
{
    private readonly IAmazonSQS _sqs;
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IAmazonEventBridge _eventBridge;
    private OrderDbContext _db;
    private readonly string _queueUrl = "https://sqs.eu-north-1.amazonaws.com/637423341661/joaquinOrderQueue"; // Format of https://.*
    private readonly string _topicArn = "arn:aws:sns:eu-north-1:637423341661:joaquinOrderCreatedTopic"; // Format of arn:aws.*

    public OrderController(OrderDbContext db)
    {
        // Instantiate clients with default configuration
        _sqs = new AmazonSQSClient();
        _sns = new AmazonSimpleNotificationServiceClient();
        _eventBridge = new AmazonEventBridgeClient();
        _db = db;
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

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var response = await _sqs.ReceiveMessageAsync(request);

        foreach (var message in response.Messages)
        {
            // Define the order variable
            Order? order = null;
            //The message we want is nested in the queue request.
            using(JsonDocument document = JsonDocument.Parse(message.Body))
            {
                string innerMessage = document.RootElement.GetProperty("Message").GetString();

                //Desereialize the inner message
                order = JsonSerializer.Deserialize<Order>(innerMessage);
            }

            // Process order (e.g., update inventory)
            if(order != null)
            {
                var existingOrder = await _db.Orders.FindAsync(order.OrderId);
                if (existingOrder != null)
                {
                    //Calculate the total and change processed flag to true
                    existingOrder.Total = existingOrder.Quantity * existingOrder.Amount;
                    existingOrder.Processed = true;
                    // Update the order on RDS
                    _db.Entry(existingOrder).State = EntityState.Modified;
                    await _db.SaveChangesAsync();
                }
            }

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
        // Save order to RDS
        order.Processed = false; //Set processed to false, as we are just adding it to the database
        await _db.Orders.AddAsync(order);
        await _db.SaveChangesAsync();


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