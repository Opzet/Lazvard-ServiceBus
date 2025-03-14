using Lazvard.Message.Amqp.Server;
using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.CommandLine;
using Azure.Messaging.ServiceBus;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Net.Sockets;

namespace Lazvard.Message.Cli;

public static class CommandHandler
{
    public static async Task Handle(string[] args, ILoggerFactory loggerFactory)
    {
        var rootCommand = new RootCommand("Lazvard Message Command Line");

        var initConfigOption = new Option<bool>(
            name: "--init-config",
            description: "Create config file");
        initConfigOption.AddAlias("-ic");
        initConfigOption.IsRequired = false;
        initConfigOption.SetDefaultValue(true);

        var silentOption = new Option<bool>(
            name: "--silent",
            description: "Suppress all user input prompt");
        silentOption.AddAlias("-s");
        silentOption.IsRequired = false;

        var configOption = new Option<string?>(
            name: "--config",
            description: "The TOML config file path");

        configOption.AddAlias("-c");
        configOption.IsRequired = false;

        var selfTestOption = new Option<bool>(
            name: "--self-test",
            description: "Run self-test to validate service functionality");
        selfTestOption.AddAlias("-st");
        selfTestOption.IsRequired = false;

        rootCommand.AddOption(configOption);
        rootCommand.AddOption(silentOption);
        rootCommand.AddOption(initConfigOption);
        rootCommand.AddOption(selfTestOption);

        rootCommand.SetHandler(async (configPath, isSilent, initConfig, selfTest) =>
        {
            // Check if config.toml exists and read it if it does
            if (string.IsNullOrEmpty(configPath) && File.Exists("config.toml"))
            {
                configPath = "config.toml";
            }

            CliConfig? config = null;
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            {
                var result = Configuration.Read(configPath);
                if (result.IsSuccess)
                {
                    config = result.Value;
                }
                else
                {
                    Console.WriteLine($"Failed to read configuration: {result.Error}");
                    return;
                }
            }

            if ((selfTest) ||(config.SelfTestEnabled))
            {
                await RunSelfTest(config, loggerFactory);
            }
            else
            {
                await RunServer(new AMQPServerParameters(configPath, isSilent, initConfig), loggerFactory);
            }
        }, configOption, silentOption, initConfigOption, selfTestOption);

        await rootCommand.InvokeAsync(args);
    }

    public static async Task RunServer(AMQPServerParameters parameters, ILoggerFactory loggerFactory)
    {
        var (config, certificate) = await AMQPServerHandler.StartAsync(parameters);

        Console.WriteLine($"Lajvard ServiceBus service is successfully listening at http://{config.IP}:{config.Port}");
        Console.WriteLine();

        var connectionStringPanel = new Panel($"ConnectionString: Endpoint=sb://{config.IP}{(!config.UseHttps ? $":{config.Port}" : string.Empty)}/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=1;UseDevelopmentEmulator=false;")
        {
            Border = BoxBorder.Double,
            Padding = new Padding(1, 1, 1, 1)
        };
        AnsiConsole.Write(connectionStringPanel);
        AnsiConsole.WriteLine();

        var source = new CancellationTokenSource();
        var exitEvent = new AsyncManualResetEvent(false);
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Lajvard ServiceBus service is closing");

            eventArgs.Cancel = true;
            exitEvent.Set();
        };

        var nodeFactory = new NodeFactory(loggerFactory, source.Token);
        var server = new Server(nodeFactory, loggerFactory);
        var broker = server.Start(config, certificate);

        Console.WriteLine();

        await exitEvent.WaitAsync(default);
        source.Cancel();
        broker.Stop();
    }

    public static async Task RunSelfTest(CliConfig? config, ILoggerFactory loggerFactory)
    {
        if (config == null)
        {
            Console.WriteLine("Configuration is required for self-test.");
            return;
        }

        var parameters = new AMQPServerParameters(configPath: null, isSilent: false, initConfigFile: false);
        var (serverConfig, certificate) = await AMQPServerHandler.StartAsync(parameters);

        Console.WriteLine($"Running self-test on Lajvard ServiceBus service at http://{serverConfig.IP}:{serverConfig.Port}");
        Console.WriteLine();

        if (!config.SelfTestEnabled)
        {
            Console.WriteLine("Self-test is disabled in the configuration.");
            return;
        }

        // serverConfig.IP
        var ip = IPAddress.TryParse(serverConfig.IP, out var serverIP) ? serverIP : LocalIPAddress();

        IsPortAvailable(ip,  serverConfig.Port);

        var messageCount = config.SelfTestMessageCount;
        var topicName = config.SelfTestTopicName;
        var subscriptionName = config.SelfTestSubscriptionName;

        string connectionString = $"Endpoint=sb://{serverConfig.IP}{(!serverConfig.UseHttps ? $":{serverConfig.Port}" : string.Empty)}/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=1;UseDevelopmentEmulator=true;";

        var client = new ServiceBusClient(connectionString, new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpTcp
        });

        var sender = client.CreateSender(topicName);
        var receiver = client.CreateReceiver(topicName, subscriptionName);

        for (int i = 0; i < messageCount; i++) // Example loop count, adjust as needed
        {
            var messageBody = $"Test message {i + 1}";
            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(messageBody))
            {
                MessageId = Guid.NewGuid().ToString(),
                Subject = "test",
                ContentType = "application/json"
            };

            await sender.SendMessageAsync(message);
            Console.WriteLine($"Message {i + 1} sent successfully.");

            var receivedMessages = await receiver.ReceiveMessagesAsync(1);
            if (receivedMessages.Any())
            {
                Console.WriteLine($"Message {i + 1} received: {Encoding.UTF8.GetString(receivedMessages[0].Body)}");
                await receiver.CompleteMessageAsync(receivedMessages[0]);
            }
            else
            {
                Console.WriteLine($"Message {i + 1} not received.");
            }
        }

        Console.WriteLine("Self-test completed.");
    }

    public static IPAddress GetLocalIPAddress()
    {
        Debug.WriteLine("Attempting to retrieve local IP Address.");
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                Debug.WriteLine($"Local IPv4 address found: {ip}");
                return ip;
            }
        }
        Debug.WriteLine("No network adapters with an IPv4 address in the system. Throwing exception.");
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }


    public static IPAddress LocalIPAddress()
    {
        //return GetLocalIPAddress();
        return new IPAddress(new byte[] { 127, 0, 0, 1 });

        //get
        //{
        //    switch (HostingEnvironment.HostingMode)
        //    {
        //        case Deployment.OLDSERVER:
        //            return new IPAddress(new byte[] { 192, 168, 100, 16 });
        //        case Deployment.PRODUCTION:
        //            return new IPAddress(new byte[] { 192, 168, 100, 171 });
        //        case Deployment.Testing:
        //            return new IPAddress(new byte[] { 192, 168, 100, 75 });
        //        case Deployment.Development_UiAndApi:
        //return new IPAddress(new byte[] { 127, 0, 0, 1 });
        //        case Deployment.Development_OnlyUi_UsingTestWebApi:
        //            return new IPAddress(new byte[] { 192, 168, 100, 75 });

        //        default:
        //            throw new Exception("Invalid Hosting Mode, cant get local IP");
        //    }
        //}
    }

    /// <summary>
    /// Checks if a given port is available on the local machine.
    /// </summary>
    /// <param name="port">The port number to check.</param>
    /// <returns><c>true</c> if the port is available; otherwise, <c>false</c>.</returns>
    public static bool IsPortAvailable(IPAddress serverIP, int port)
    {
        TcpListener tcpListener = null;
        try
        {
            tcpListener = new TcpListener(serverIP, port);
            tcpListener.Start();
            Console.WriteLine($"local {serverIP.ToString()} : port {port} is available.");
            return true;
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"local {serverIP.ToString()} : port {port} is not available. Exception: {ex.Message}");
            return false;
        }
        finally
        {
            tcpListener?.Stop();
        }
    }
}
