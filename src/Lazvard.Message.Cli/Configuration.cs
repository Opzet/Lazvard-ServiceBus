using Iso8601DurationHelper;
using Lazvard.Message.Amqp.Server;
using Lazvard.Message.Amqp.Server.Helpers;
using System.Xml.Linq;
using Tommy;

namespace Lazvard.Message.Cli;

public class CliConfig : BrokerConfig
{
    public string CertificatePath { get; set; } = string.Empty;
    public string CertificatePassword { get; set; } = string.Empty;
    public bool UseHttps { get; set; } = false;

    // Self-test settings
    public bool SelfTestEnabled { get; set; } = false;
    public int SelfTestMessageCount { get; set; } = 10;
    public string SelfTestTopicName { get; set; } = "topic-1";
    public string SelfTestSubscriptionName { get; set; } = "topic-1-subscription-a";
}

internal static class ConfigurationSections
{
    public const string Server = nameof(Server);
    public const string AMQP = nameof(AMQP);
    public const string Queues = nameof(Queues);
}

public sealed class Configuration
{
    private const string defaultName = "config.toml";
    private static readonly string userConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lazvard");

    public static async Task WriteAsync(CliConfig config, string path)
    {
        using StreamWriter writer = File.CreateText(path);

        var toml = new TomlTable()
        {
            [ConfigurationSections.Server] =
            {
                [nameof(BrokerConfig.IP)] = new TomlString
                {
                    Value = config.IP,
                    Comment = "IP to listen on"
                },
                [nameof(BrokerConfig.Port)] = new TomlInteger
                {
                    Value = config.Port,
                    Comment = "Port to listen on"
                },
                [nameof(CliConfig.UseHttps)] = new TomlBoolean
                {
                    Value = config.UseHttps,
                    Comment = "use Https with a valid certificate, default is false"
                },
                [nameof(CliConfig.CertificatePath)] = new TomlString
                {
                    Value = config.CertificatePath,
                    Comment = "The path to trusted X.509 certificate (PFX - PKCS #12), required only when UseBuiltInCertificateManager is false"
                },
                [nameof(CliConfig.CertificatePassword)] = new TomlString
                {
                    Value = config.CertificatePassword,
                    Comment = "The certificate password, optional"
                },
            },
            [ConfigurationSections.AMQP] =
            {
                [nameof(BrokerConfig.ConnectionIdleTimeOut)] =  new TomlInteger
                {
                    Value = config.ConnectionIdleTimeOut,
                    Comment = "Connection idle timeout in milliseconds for AMQP connection, default is 4 minutes"
                },
                [nameof(BrokerConfig.MaxFrameSize)] =  new TomlInteger
                {
                    Value = config.MaxFrameSize,
                    Comment = "Max AMQP frame size in byte, default is 64Kib"
                },
                [nameof(BrokerConfig.MaxMessageSize)] =  new TomlInteger
                {
                    Value = config.MaxMessageSize,
                    Comment = "Max AMQP message size in byte, default is 64MiB"
                },
            },
            ["SelfTest"] =
            {
                [nameof(CliConfig.SelfTestEnabled)] = new TomlBoolean
                {
                    Value = config.SelfTestEnabled,
                    Comment = "Enable or disable self-test"
                },
                [nameof(CliConfig.SelfTestMessageCount)] = new TomlInteger
                {
                    Value = config.SelfTestMessageCount,
                    Comment = "Number of messages to send during self-test"
                },
                [nameof(CliConfig.SelfTestTopicName)] = new TomlString
                {
                    Value = config.SelfTestTopicName,
                    Comment = "Topic name for self-test"
                },
                [nameof(CliConfig.SelfTestSubscriptionName)] = new TomlString
                {
                    Value = config.SelfTestSubscriptionName,
                    Comment = "Subscription name for self-test"
                },
            }
        };

        toml.WriteTo(writer);
        writer.WriteLine();

        var queues = new TomlArray
        {
            IsTableArray = true,
        };
        var topics = new TomlArray
        {
            IsTableArray = true,
        };
        toml = new TomlTable
        {
            [nameof(ConfigurationSections.Queues)] = queues,
            [nameof(BrokerConfig.Topics)] = topics,
        };

        foreach (var topic in config.Topics)
        {
            var isQueue = topic.Subscriptions.Count() == 1 && string.IsNullOrEmpty(topic.Subscriptions.First().Name);

            if (isQueue)
            {
                var queue = BuildSubscription(topic.Subscriptions.First());
                queue[nameof(TopicConfig.Name)] = new TomlString
                {
                    Value = topic.Name,
                    Comment = "Queue name",
                };

                queues.Add(queue);
            }
            else
            {
                var subscriptionsArray = new TomlArray
                {
                    IsTableArray = true,
                };
                subscriptionsArray.AddRange(topic.Subscriptions.Select(BuildSubscription));

                topics.Add(new TomlTable
                {
                    [nameof(TopicConfig.Name)] = new TomlString
                    {
                        Value = topic.Name,
                        Comment = "Topic name",
                    },
                    [nameof(TopicConfig.Subscriptions)] = subscriptionsArray,
                });
            }
        }
        ;

        toml.WriteTo(writer);
        await writer.FlushAsync();
    }

    private static TomlTable BuildSubscription(TopicSubscriptionConfig config)
    {
        return new TomlTable
        {
            [nameof(TopicSubscriptionConfig.Name)] = new TomlString
            {
                Value = config.Name,
                Comment = "Subscription name"
            },
            [nameof(TopicSubscriptionConfig.MaxDeliveryCount)] = new TomlInteger
            {
                Value = config.MaxDeliveryCount,
                Comment = "Number of maximum deliveries. The default value is 50 times"
            },
            [nameof(TopicSubscriptionConfig.LockDuration)] = new TomlString
            {
                Value = config.LockDuration.ToString(),
                Comment = "ISO 8061 lock duration for the subscription. The default value is 1 minute"
            },
        };
    }

    public static CliConfig CreateDefaultConfig()
    {
       return new CliConfig
        {
            Topics =
            [
                new TopicConfig("topic-1", new[]
                {
                    new TopicSubscriptionConfig("topic-1-subscription-a")
                }),
                new TopicConfig("topic-2", new[]
                {
                    new TopicSubscriptionConfig("topic-2-subscription-a"),
                    new TopicSubscriptionConfig("topic-2-subscription-b")
                }),
                new TopicConfig("queue-1", new[]
                {
                    new TopicSubscriptionConfig("")
                }),
            ],
        };
    }

    public static (string path, bool exists) GetConfigPath(string? inputConfigPath)
    {
        if (!string.IsNullOrEmpty(inputConfigPath))
        {
            return (inputConfigPath, File.Exists(inputConfigPath));
        }
        if (File.Exists(defaultName))
        {
            return (defaultName, true);
        }
        if (File.Exists(Path.Combine(userConfigPath, defaultName)))
        {
            return (Path.Combine(userConfigPath, defaultName), true);
        }

        return (defaultName, false);
    }

    public static bool Exists(string? configPath)
    {
        if (!string.IsNullOrEmpty(configPath))
        {
            return File.Exists(configPath);
        }

        return File.Exists(defaultName)
            || File.Exists(Path.Combine(userConfigPath, defaultName));
    }

    public static Result<CliConfig> Read(string path)
    {
        using var configFile = File.OpenText(path);
        var config = TOML.Parse(configFile);
        if (config is null)
        {
            return Result.Fail("file is not a valid TOML.");
        }

        try
        {

            //config.toml contents
            //{{Server = { IP = "0.0.0.0", Port = 5672, UseHttps = false, CertificatePath = "", CertificatePassword = "" },
            //AMQP = { ConnectionIdleTimeOut = 240000, MaxFrameSize = 65536, MaxMessageSize = 67108864 },
            //Queues = [ { Name = "queue-1", MaxDeliveryCount = 50, LockDuration = "PT1M" } ],
            //Topics = [ { Name = "topic-1", Subscriptions = [ { Name = "topic-1-subscription-a", MaxDeliveryCount = 50, LockDuration = "PT1M" } ] }, { Name = "topic-2", Subscriptions = [ { Name = "topic-2-subscription-a", MaxDeliveryCount = 50, LockDuration = "PT1M" }, { Name = "topic-2-subscription-b", MaxDeliveryCount = 50, LockDuration = "PT1M" } ] } ],
            //SelfTest = { Enabled = true, MessageCount = 10, TopicName = "topic-1", SubscriptionName = "topic-1-subscription-a" } }}

            var result = new CliConfig();

            result.IP = config[ConfigurationSections.Server][nameof(BrokerConfig.IP)]?.AsString ?? result.IP;
            result.Port = config[ConfigurationSections.Server][nameof(BrokerConfig.Port)]?.AsInteger ?? result.Port;
            result.UseHttps = config[ConfigurationSections.Server][nameof(CliConfig.UseHttps)].AsBoolean ?? result.UseHttps;
            result.CertificatePath = config[ConfigurationSections.Server][nameof(CliConfig.CertificatePath)].AsString;
            result.CertificatePassword = config[ConfigurationSections.Server][nameof(CliConfig.CertificatePassword)].AsString;

            result.ConnectionIdleTimeOut = (uint?)config[ConfigurationSections.AMQP][nameof(BrokerConfig.ConnectionIdleTimeOut)]?.AsInteger
                ?? result.ConnectionIdleTimeOut;

            result.MaxFrameSize = (uint?)config[ConfigurationSections.AMQP][nameof(BrokerConfig.MaxFrameSize)]?.AsInteger
                ?? result.MaxFrameSize;

            result.MaxMessageSize = (uint?)config[ConfigurationSections.AMQP][nameof(BrokerConfig.MaxMessageSize)]?.AsInteger
                ?? result.MaxMessageSize;


            // Not working - not finding SelfTestEnabled , Enabled value?

            string Enabledkey = "Enabled"; // nameof(CliConfig.SelfTestEnabled);
            var node = config["SelfTest"][Enabledkey]; ;
            result.SelfTestEnabled = node?.AsBoolean ?? result.SelfTestEnabled;

            result.SelfTestEnabled = config["SelfTest"][nameof(CliConfig.SelfTestEnabled)]?.AsBoolean ?? result.SelfTestEnabled;

            string SelfTestMessageCountkey = nameof(CliConfig.SelfTestMessageCount);
            result.SelfTestMessageCount = config["SelfTest"][nameof(CliConfig.SelfTestMessageCount)]?.AsInteger ?? result.SelfTestMessageCount;

            result.SelfTestTopicName = config["SelfTest"][nameof(CliConfig.SelfTestTopicName)]?.AsString ?? result.SelfTestTopicName;
            result.SelfTestSubscriptionName = config["SelfTest"][nameof(CliConfig.SelfTestSubscriptionName)]?.AsString ?? result.SelfTestSubscriptionName;

            var defaultTopicConf = new TopicSubscriptionConfig("");
            var queues = config[nameof(ConfigurationSections.Queues)]
                .AsArray?
                .Children
                .Select(q => new TopicConfig(q[nameof(TopicConfig.Name)].AsString,
                [
                    new TopicSubscriptionConfig(string.Empty)
                    {
                        LockDuration = Duration.Parse(q[nameof(TopicSubscriptionConfig.LockDuration)]?.AsString
                            ?? defaultTopicConf.LockDuration.ToString()),
                        MaxDeliveryCount = q[nameof(TopicSubscriptionConfig.MaxDeliveryCount)]?.AsInteger
                            ?? defaultTopicConf.MaxDeliveryCount,
                    }
                ])
                ).ToArray() ?? [];

            var topics = config[nameof(BrokerConfig.Topics)]
                .AsArray?
                .Children
                .Select(t =>
                {
                    var subscriptions = t[nameof(TopicConfig.Subscriptions)]
                        .AsArray
                        .Children
                        .Select(s => new TopicSubscriptionConfig(s[nameof(TopicSubscriptionConfig.Name)].AsString)
                        {
                            LockDuration = Duration.Parse(s[nameof(TopicSubscriptionConfig.LockDuration)]?.AsString
                                ?? defaultTopicConf.LockDuration.ToString()),
                            MaxDeliveryCount = s[nameof(TopicSubscriptionConfig.MaxDeliveryCount)]?.AsInteger
                                ?? defaultTopicConf.MaxDeliveryCount,
                        });

                    return new TopicConfig(t[nameof(TopicConfig.Name)].AsString, subscriptions);
                }).ToArray() ?? [];

            result.Topics = [.. topics, .. queues];

            return result;
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }
}
