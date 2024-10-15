# C# Cloud AWS - Day Four
## Commands to Set Up SQS, SNS, and EventBridge

Note in Every command and URL being called their are fields to be replaced.
`{studentName}` - example `ajdewilzin`
`{region}` - example `eu-north-1`

### Steps
1. Create an SQS Queue:

`aws sqs create-queue --queue-name {studentName}OrderQueue`
`aws sns subscribe --topic-arn arn:aws:sns:{region}:637423341661:{studentName}OrderCreatedTopic --protocol sqs --notification-endpoint arn:aws:sqs:{region}:637423341661:{studentName}OrderQueue`
Replace the above `QueueUrl` with _queueUrl in your controller

2. Create an SNS Topic:

`aws sns create-topic --name {studentName}OrderCreatedTopic`
Replace the above `TopicArn` with _topicArn in your controller

3. Create an EventBridge Event Bus:

`aws events create-event-bus --name {StudentName}CustomEventBus --region {region}`

4. Create an EventBridge Rule:

`aws events put-rule --name {StudentName}OrderProcessedRule --event-pattern '{\"source\": [\"order.service\"]}' --event-bus-name {StudentName}CustomEventBus`


5. Subscribe the SQS Queue to the SNS Topic

`aws sqs get-queue-attributes --queue-url https://sqs.{region}.amazonaws.com/637423341661/{studentName}OrderQueue --attribute-name QueueArn --region {region}`
`aws sns subscribe --topic-arn arn:aws:sns:{region}:637423341661:{studentName}OrderCreatedTopic --protocol sqs --notification-endpoint arn:aws:sqs:{region}:637423341661:{studentName}OrderQueue --region {region}`

6. Grant SNS Permissions to SQS

`aws sqs set-queue-attributes --queue-url https://sqs.{region}.amazonaws.com/637423341661/{studentName}OrderQueue --attributes '{"Policy":"{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"AWS\":\"*\"},\"Action\":\"SQS:SendMessage\",\"Resource\":\"arn:aws:sqs:{region}:637423341661:{studentName}OrderQueue\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"arn:aws:sns:{region}:637423341661:{studentName}OrderCreatedTopic\"}}}]}}"}' --region {region}`


## Core Exercise
Create a few orders using a RDS database. Orders to be saved in Database.
Update Process flag to false

Process orders and update the Total amount from QTY * AMOUNT
Update Process flag to true

## Extension Exercise
