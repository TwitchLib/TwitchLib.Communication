using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Tests.Helpers;
using Xunit;

namespace TwitchLib.Communication.Tests.Clients;

/// <summary>
///     The client must keep reading and keep reconnecting, whatever the server or the subscribers do.
///     A client that stops doing either still reports <see cref="IClient.IsConnected"/>
///     or just goes quiet, so the application never learns that it lost the connection.
/// </summary>
public class WebSocketClientConnectionLossTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Client_Keeps_Reading_When_An_OnMessage_Subscriber_Throws()
    {
        using var server = new LocalWebSocketServer();
        var client = CreateClient(server, new ReconnectionPolicy(100));
        var errors = new ConcurrentQueue<Exception>();
        var secondMessage = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnMessage += (_, e) =>
        {
            if (e.Message == "first") throw new InvalidOperationException("subscriber failed");
            if (e.Message == "second") secondMessage.TrySetResult(true);
            return Task.CompletedTask;
        };
        client.OnError += (_, e) =>
        {
            errors.Enqueue(e.Exception);
            return Task.CompletedTask;
        };

        try
        {
            Assert.True(await client.OpenAsync());
            Assert.True(await server.WaitForConnectionAsync(Timeout));

            await server.SendAsync("first");
            await server.SendAsync("second");

            Assert.True(await CompletesAsync(secondMessage.Task), "the message after the failing one was not read");
            Assert.Contains(errors, e => e is InvalidOperationException { Message: "subscriber failed" });
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Client_Reconnects_When_The_Server_Closes_The_Connection()
    {
        using var server = new LocalWebSocketServer();
        var client = CreateClient(server, new ReconnectionPolicy(100));
        var reconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnReconnected += (_, _) =>
        {
            reconnected.TrySetResult(true);
            return Task.CompletedTask;
        };

        try
        {
            Assert.True(await client.OpenAsync());
            Assert.True(await server.WaitForConnectionAsync(Timeout));

            await server.CloseAsync();

            Assert.True(await CompletesAsync(reconnected.Task), "the client did not reconnect");
            Assert.Equal(2, server.ConnectionCount);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Client_Reconnects_After_More_Connection_Losses_Than_MaxAttempts()
    {
        // two attempts per connection loss, the connection is lost three times
        using var server = new LocalWebSocketServer();
        var client = CreateClient(server, new ReconnectionPolicy(100, maxAttempts: 2));
        var reconnects = new SemaphoreSlim(0);
        client.OnReconnected += (_, _) =>
        {
            reconnects.Release();
            return Task.CompletedTask;
        };

        try
        {
            Assert.True(await client.OpenAsync());
            Assert.True(await server.WaitForConnectionAsync(Timeout));

            for (var loss = 1; loss <= 3; loss++)
            {
                server.Drop();
                Assert.True(await reconnects.WaitAsync(Timeout), $"the client did not reconnect after connection loss {loss}");
            }
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Client_Keeps_Reconnecting_When_An_OnReconnected_Subscriber_Throws()
    {
        using var server = new LocalWebSocketServer();
        var client = CreateClient(server, new ReconnectionPolicy(100));
        var reconnects = new SemaphoreSlim(0);
        client.OnReconnected += (_, _) =>
        {
            reconnects.Release();
            throw new InvalidOperationException("subscriber failed");
        };

        try
        {
            Assert.True(await client.OpenAsync());
            Assert.True(await server.WaitForConnectionAsync(Timeout));

            for (var loss = 1; loss <= 2; loss++)
            {
                server.Drop();
                Assert.True(await reconnects.WaitAsync(Timeout), $"the client did not reconnect after connection loss {loss}");
            }
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Client_Opens_A_Single_Connection_For_Every_Connection_Loss()
    {
        using var server = new LocalWebSocketServer();
        var client = CreateClient(server, new ReconnectionPolicy(100));
        var reconnects = new SemaphoreSlim(0);
        client.OnReconnected += (_, _) =>
        {
            reconnects.Release();
            return Task.CompletedTask;
        };

        try
        {
            Assert.True(await client.OpenAsync());
            Assert.True(await server.WaitForConnectionAsync(Timeout));

            for (var loss = 1; loss <= 10; loss++)
            {
                server.Drop();
                Assert.True(await reconnects.WaitAsync(Timeout), $"the client did not reconnect after connection loss {loss}");
                // give a second, concurrent reconnect the time to show up
                await Task.Delay(500);
                Assert.Equal(loss + 1, server.ConnectionCount);
            }

            Assert.Equal(1, server.OpenConnectionCount);
        }
        finally
        {
            client.Dispose();
        }
    }

    private static LocalClient CreateClient(LocalWebSocketServer server, ReconnectionPolicy reconnectionPolicy)
    {
        return new LocalClient(server.Url, new ClientOptions(reconnectionPolicy, disconnectWait: 0));
    }

    private static async Task<bool> CompletesAsync(Task task)
    {
        return await Task.WhenAny(task, Task.Delay(Timeout)) == task;
    }

    private sealed class LocalClient(string url, IClientOptions options) : WebSocketClient(options)
    {
        protected override string Url => url;
    }
}
