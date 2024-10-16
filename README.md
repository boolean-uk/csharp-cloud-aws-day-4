# C# Cloud AWS - Day Four
## Commands to Set Up SQS, SNS, and EventBridge

Note in Every command and URL being called there are fields that need to be replaced.

Example replacements (make sure to replace the curly brackets as well: don't keep curly brackets)

`jonashalvorsen` - example `ajdewilzin` - this can be your name, independent of your login details

`eu-north-1` - example `eu-north-1`

### Steps
1. Create an SNS Topic:

```bash
aws sns create-topic --name jonashalvorsenOrderCreatedTopic
```
If successful, you will see in your terminal a JSON response that includes `"TopicArn": "...`.

Replace `_topicArn` in your Controller code with the generated `TopicArn` value from above.

2. Create an SQS Queue:

```bash
aws sqs create-queue --queue-name jonashalvorsenOrderQueue
```

If successful, you will see in your terminal a JSON response that includes `"QueueUrl": "some_aws_url`.

Replace `_queueUrl` in your Controller code with the generated `QueueUrl` from the above command.


```bash
aws sns subscribe --topic-arn arn:aws:sns:eu-north-1:637423341661:jonashalvorsenOrderCreatedTopic --protocol sqs --notification-endpoint arn:aws:sqs:eu-north-1:637423341661:jonashalvorsenOrderQueue
```

You don't need to save the generated SubscriptionArn.

3. Create an EventBridge Event Bus:

```bash
aws events create-event-bus --name jonashalvorsenCustomEventBus --region eu-north-1
```

4. Create an EventBridge Rule:

```bash
aws events put-rule --name jonashalvorsenOrderProcessedRule --event-pattern '{\"source\": [\"order.service\"]}' --event-bus-name jonashalvorsenCustomEventBus
```

Without \
```bash
aws events put-rule --name jonashalvorsenOrderProcessedRule --event-pattern '{"source": ["order.service"]}' --event-bus-name jonashalvorsenCustomEventBus
```

If your terminal complains about double quotes, you might need to remove the backslash `\` from the command above (and commands later on).


5. Subscribe the SQS Queue to the SNS Topic

```bash
aws sqs get-queue-attributes --queue-url https://sqs.eu-north-1.amazonaws.com/637423341661/jonashalvorsenOrderQueue --attribute-name QueueArn --region eu-north-1
```

```bash
aws sns subscribe --topic-arn arn:aws:sns:eu-north-1:637423341661:jonashalvorsenOrderCreatedTopic --protocol sqs --notification-endpoint arn:aws:sqs:eu-north-1:637423341661:jonashalvorsenOrderQueue --region eu-north-1
```

6. Grant SNS Permissions to SQS


In Bash/Unix terminals you can run this command:
```bash
aws sqs set-queue-attributes --queue-url https://sqs.eu-north-1.amazonaws.com/637423341661/jonashalvorsenOrderQueue --attributes '{"Policy":"{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"AWS\":\"*\"},\"Action\":\"SQS:SendMessage\",\"Resource\":\"arn:aws:sqs:eu-north-1:637423341661:jonashalvorsenOrderQueue\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"arn:aws:sns:eu-north-1:637423341661:jonashalvorsenOrderCreatedTopic\"}}}]}"}' --region eu-north-1
```

## Core Exercise
1. Create a few orders using a RDS database. Orders to be saved in Database.
2. Update Process flag to false
3. Process orders and update the Total amount from QTY * AMOUNT
4. Update Process flag to true

## Extension Exercise
