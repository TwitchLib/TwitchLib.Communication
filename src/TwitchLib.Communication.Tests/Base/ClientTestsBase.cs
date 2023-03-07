﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using TwitchLib.Communication.Events;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Tests.Helper;

using Xunit;

namespace TwitchLib.Communication.Tests.Base
{
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
    [SuppressMessage("Style", "IDE0058")]
    [SuppressMessage("Style", "CA2254")]
    public abstract class ClientTestsBase<T> where T : IClient
    {
        public ClientTestsBase() { }
        [Fact]
        public void Client_Raises_OnConnected_EventArgs()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            T? client = GetClient<T>(logger);
            Assert.NotNull(client);
            try
            {
                ManualResetEvent pauseConnected = new ManualResetEvent(false);

                Assert.Raises<OnConnectedEventArgs>(
                    h => client.OnConnected += h,
                    h => client.OnConnected -= h,
                    () =>
                    {
                        client.OnConnected += (sender, e) => pauseConnected.Set();
                        client.Open();
                        Assert.True(pauseConnected.WaitOne(15000));
                    });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                client?.Dispose();
                Task.Delay(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            }
        }
        [Fact]
        public void Client_Raises_OnDisconnected_EventArgs()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            T? client = GetClient<T>(logger);
            Assert.NotNull(client);
            try
            {
                ManualResetEvent pauseDisconnected = new ManualResetEvent(false);

                Assert.Raises<OnDisconnectedEventArgs>(
                    h => client.OnDisconnected += h,
                    h => client.OnDisconnected -= h,
                    () =>
                    {
                        client.OnConnected += (sender, e) =>
                        {
                            Task.Delay(3000).GetAwaiter().GetResult();
                            client.Close();
                        };
                        client.OnDisconnected += (sender, e) => pauseDisconnected.Set();
                        client.Open();
                        Assert.True(pauseDisconnected.WaitOne(200000));
                    });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                client?.Dispose();
                Task.Delay(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            }
        }
        [Fact]
        public void Client_Raises_OnReconnected_EventArgs()
        {
            //WebSocketClient client = new WebSocketClient(new ClientOptions()
            // TODO: revert: see "TODO: handle Options.ReconnectionPolicy == null; how to act in that case?"
            //{ ReconnectionPolicy = null }
            //);
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            T? client = GetClient<T>(logger);
            Assert.NotNull(client);
            try
            {
                ManualResetEvent pauseReconnected = new ManualResetEvent(false);

                Assert.Raises<OnReconnectedEventArgs>(
                    h => client.OnReconnected += h,
                    h => client.OnReconnected -= h,
                     () =>
                     {
                         client.OnConnected += (s, e) => client.Reconnect();

                         client.OnReconnected += (s, e) => pauseReconnected.Set();
                         client.Open();

                         Assert.True(pauseReconnected.WaitOne(20000));
                     });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                client?.Dispose();
                Task.Delay(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            }
        }
        [Fact]
        public void Dispose_Client_Before_Connecting_IsOK()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            IClient? client = null;
            try
            {
                client = GetClient<T>(logger);
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
                Task.Delay(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            }
        }
        [Fact]
        public void Client_Can_SendAndReceive_Messages()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            T? client = GetClient<T>(logger);
            Assert.NotNull(client);
            try
            {

                ManualResetEvent pauseConnected = new ManualResetEvent(false);
                ManualResetEvent pauseReadMessage = new ManualResetEvent(false);

                Assert.Raises<OnMessageEventArgs>(
                    h => client.OnMessage += h,
                    h => client.OnMessage -= h,
                     () =>
                     {
                         client.OnConnected += (sender, e) => pauseConnected.Set();

                         client.OnMessage += (sender, e) =>
                         {
                             Assert.NotNull(e.Message);
                             string msg = e.Message;
                             Assert.StartsWith("PONG :tmi.twitch.tv", e.Message);
                             pauseReadMessage.Set();
                         };

                         client.Open();
                         client.Send("PING");
                         Assert.True(pauseConnected.WaitOne(120_000));
                         Assert.True(pauseReadMessage.WaitOne(120_000));
                     });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                client?.Dispose();
                Task.Delay(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            }
        }

        private static TClient? GetClient<TClient>(ILogger<TClient> logger)
        {
            Type[] constructorParameterTypes = new Type[] {
            typeof(IClientOptions),
            typeof(ILogger<TClient>)
        };
            ConstructorInfo? constructor = typeof(TClient).GetConstructor(constructorParameterTypes);
            object[] constructorParameters = new object[] {
            new ClientOptions(),
            logger
        };
            return (TClient?) constructor?.Invoke(constructorParameters);

        }
    }
}