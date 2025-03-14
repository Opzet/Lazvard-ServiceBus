using Microsoft.Azure.Amqp;

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
                        SendMessage();
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


        static void SendMessage()
        {
            var message = AmqpMessage.Create();
            Console.WriteLine("Sending Message.");

            message.Properties.MessageId = Guid.NewGuid().ToString();
            message.Properties.To = "amqps://127.0.0.1";
            message.Properties.Subject = "test";
            message.Properties.CreationTime = DateTime.UtcNow;
            message.Properties.ContentType = "application/json";
            message.Properties.ContentEncoding = "utf-8";

            message.PrepareForSend();

            // Send the message
            

            //Get Errors
            Console.WriteLine("Message sent.");
        }
    }
}
