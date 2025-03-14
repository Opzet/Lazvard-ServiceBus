﻿using Lazvard.Message.Amqp.Server;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Sasl;
using Microsoft.Azure.Amqp.Transport;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Lazvard.Message.Cli;

public sealed class Server
{
    private readonly ILoggerFactory loggerFactory;
    private readonly NodeFactory nodeFactory;

    public Server(NodeFactory nodeFactory, ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        this.nodeFactory = nodeFactory;
    }
  
    public Broker Start(CliConfig config, X509Certificate2? certificate)
    {
        var amqpSettings = new AmqpSettings();
        var version = new AmqpVersion(1, 0, 0);
        var saslProvider = new SaslTransportProvider();

        saslProvider.Versions.Add(version);
        saslProvider.AddHandler(new SaslAnonymousHandler());
        saslProvider.AddHandler(new SaslAnonymousHandler("MSSBCBS"));

        amqpSettings.TransportProviders.Add(saslProvider);

        var amqpProvider = new AmqpTransportProvider();
        amqpProvider.Versions.Add(version);

        amqpSettings.TransportProviders.Add(amqpProvider);

        var listeners = new TransportListener[1];

        var tcpSettings = new TcpTransportSettings() { Host = config.IP, Port = config.Port };

        if (config.UseHttps && certificate != null)
        {
            var tlsSettings = new TlsTransportSettings(tcpSettings) { Certificate = certificate, IsInitiator = false };
            listeners[0] = tlsSettings.CreateListener();
        }
        else
        {
            listeners[0] = tcpSettings.CreateListener();
        }


        var broker = new Broker(config, nodeFactory.Create(config), listeners, amqpSettings, loggerFactory);
        broker.Start();

        return broker;
    }
}
