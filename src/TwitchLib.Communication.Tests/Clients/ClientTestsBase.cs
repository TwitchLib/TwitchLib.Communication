using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Tests.Helpers;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace TwitchLib.Communication.Tests.Clients;

/// <summary>
///     bundles <see cref="IClient"/>-Tests in one container
/// </summary>
/// <typeparam name="T">
///     <list type="bullet">
///         <see cref="Clients.TcpClient"/>
///     </list>
///     <list type="bullet">
///         <see cref="Clients.WebSocketClient"/>
///     </list>
/// </typeparam>
public abstract class ClientTestsBase<T> where T : IClient
{
    private static TimeSpan WaitOneDuration => TimeSpan.FromSeconds(5);
    private readonly IClientOptions? _options;

    protected ClientTestsBase(IClientOptions? options = null)
    {
        _options = options;
    }
    
    [Fact]
    public async Task Client_Raises_OnConnected_EventArgs()
    {
        // create one logger per test-method! - cause one file per test-method is generated
        var logger = TestLogHelper.GetLogger<T>();
        var client = GetClient(logger, _options);
        Assert.NotNull(client);
        try
        {
            var pauseConnected = new ManualResetEvent(false);

            await MyAssert.RaisesAsync<OnConnectedEventArgs>(
                h => client.OnConnected += h,
                h => client.OnConnected -= h,
                async () =>
                {
                    client.OnConnected += async(sender, e) =>
                    {
                        pauseConnected.Set();
                    };

                    await client.OpenAsync();
                    Assert.True(pauseConnected.WaitOne(WaitOneDuration));
                });
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
            Assert.Fail(e.ToString());
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Client_Raises_OnDisconnected_EventArgs()
    {
        // create one logger per test-method! - cause one file per test-method is generated
        var logger = TestLogHelper.GetLogger<T>();
        var client = GetClient(logger, _options);
        Assert.NotNull(client);
        try
        {
            var pauseDisconnected = new ManualResetEvent(false);

            await MyAssert.RaisesAsync<OnDisconnectedEventArgs>(
                h => client.OnDisconnected += h,
                h => client.OnDisconnected -= h,
                async () =>
                {
                    client.OnConnected += async (sender, e) =>
                    {
                        await client.CloseAsync();
                    };

                    client.OnDisconnected += async (sender, e) =>
                    {
                        pauseDisconnected.Set();
                    };
                    await client.OpenAsync();

                    Assert.True(pauseDisconnected.WaitOne(WaitOneDuration));
                });
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
            Assert.Fail(e.ToString());
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Client_Raises_OnReconnected_EventArgs()
    {
        // create one logger per test-method! - cause one file per test-method is generated
        var logger = TestLogHelper.GetLogger<T>();
        var client = GetClient(logger, _options);
        Assert.NotNull(client);
        try
        {
            var pauseReconnected = new ManualResetEvent(false);

            await MyAssert.RaisesAsync<OnConnectedEventArgs>(
                h => client.OnReconnected += h,
                h => client.OnReconnected -= h,
                async () =>
                {
                    client.OnConnected += async (s, e) => await client.ReconnectAsync();

                    client.OnReconnected += async (s, e) =>
                    {
                        pauseReconnected.Set();
                    };
                    await client.OpenAsync();
                    Assert.True(pauseReconnected.WaitOne(WaitOneDuration));
                });
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
            Assert.Fail(e.ToString());
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public void Dispose_Client_Before_Connecting_IsOK()
    {
        // create one logger per test-method! - cause one file per test-method is generated
        var logger = TestLogHelper.GetLogger<T>();
        IClient? client = null;
        try
        {
            client = GetClient(logger, _options);
            Assert.NotNull(client);
            client.Dispose();
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
            Assert.Fail(e.ToString());
        }
        finally
        {
            client?.Dispose();
        }
    }

    private static TClient? GetClient<TClient>(ILogger<TClient> logger, IClientOptions? options = null)
    {
        var constructorParameterTypes = new Type[]
        {
            typeof(IClientOptions),
            typeof(ILogger<TClient>)
        };
        
        var constructor = typeof(TClient).GetConstructor(constructorParameterTypes);
        var constructorParameters = new object[]
        {
            options ?? new ClientOptions(),
            logger
        };
        
        return (TClient?)constructor?.Invoke(constructorParameters);
    }
}
