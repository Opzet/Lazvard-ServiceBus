﻿using Azure.Messaging.ServiceBus;

namespace Lazvard.Message.Amqp.Server.IntegrationTests;

public sealed class ClientFixture : IAsyncDisposable
{
    public readonly ServiceBusClient Client;

    public ClientFixture()
    {
        string connectionString = "Endpoint=sb://127.0.0.1:5671/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=1;UseDevelopmentEmulator=false;";
        Client = new(connectionString);
    }

    public ValueTask DisposeAsync()
    {
        return Client.DisposeAsync();
    }
}

public interface IClientFixture : IClassFixture<ClientFixture>
{
}
