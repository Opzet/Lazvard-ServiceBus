using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Framing;
using System.Text;

namespace MsgBusClientTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Connecting to Local Azure Amqp Message Bus Service...");

            bool exit = false;
            while (!exit)
            {
                Console.WriteLine("Menu:");
                Console.WriteLine("1. Send Message");
                Console.WriteLine("2. Exit");
                Console.Write("Select an option: ");
                var input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        SendMessage().GetAwaiter().GetResult();
                        break;
                    case "2":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
        }

        static async Task SendMessage()
        {
            // https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/TROUBLESHOOTING.md#handle-service-bus-exceptions
            // https://aka.ms/azsdk/net/servicebus/exceptions/troubleshoot.

            /*
                // PowerShell open Port
                New-NetFirewallRule -DisplayName "_CamcoAzureServiceBus" `
                    -Direction Inbound `
                    -Action Allow `
                    -Protocol TCP `
                    -LocalPort 5672 `
                    -Enabled True
            */

            try
            {
                var messageBody = "Hello, World!";
                var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(messageBody))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Subject = "test",
                    ContentType = "application/json",
                };

                string connectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=1;UseDevelopmentEmulator=false;";

                // topic-1/topic-1-subscription-a
                var topicName = "topic1";
                var subscriptionName = "topic-1-subscription-a"; // "Subscription1";

                var client = new ServiceBusClient(connectionString, new ServiceBusClientOptions
                {
                    TransportType = ServiceBusTransportType.AmqpTcp
                });

                var sender = client.CreateSender(topicName);
                await sender.SendMessageAsync(message);

                Console.WriteLine("Message sent successfully.");

                // Dead-lettering logic
                var maxDeliveryCount = 10; // Example value, adjust as needed
                var receiver = client.CreateReceiver(topicName, subscriptionName);

                for (var i = 0; i < maxDeliveryCount; i++)
                {
                    var receivedMessages = await receiver.ReceiveMessagesAsync(1);
                    if (receivedMessages.Any())
                    {
                        await receiver.AbandonMessageAsync(receivedMessages[0]);
                    }
                }

                var deadLetterReceiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions
                {
                    SubQueue = SubQueue.DeadLetter,
                    ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
                });

                var deadLetterMessages = await deadLetterReceiver.ReceiveMessagesAsync(1);
                if (deadLetterMessages.Any())
                {
                    Console.WriteLine("Message moved to dead-letter queue.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }
    }
}
