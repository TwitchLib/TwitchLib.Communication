using System;
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
using Xunit.Sdk;

namespace TwitchLib.Communication.Tests.Clients
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
    public abstract class AClientTestsBase<T> where T : IClient
    {
        private static readonly uint waitAfterDispose = 3;
        public AClientTestsBase() { }
        [Fact]
        public void Client_Raises_OnConnected_EventArgs()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            T? client = GetClient(logger);
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
                TheFinally(client);
            }
        }
        [Fact]
        public void Client_Raises_OnDisconnected_EventArgs()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            T? client = GetClient(logger);
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
                TheFinally(client);
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
            T? client = GetClient(logger);
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
                TheFinally(client);
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
                client = GetClient(logger);
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
                TheFinally((T?) client);
            }
        }
        [Fact]
        public void Client_Can_SendAndReceive_Messages()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            T? client = GetClient(logger);
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
                TheFinally(client);
            }
        }
        /// <summary>
        ///     checks <see cref="ClientOptions.MessageSendOptions"/>
        ///     <br></br>
        ///     <see cref="ISendOptions.SendsAllowedInPeriod"/> = 0
        ///     <br></br>
        ///     sending <see cref="Enums.MessageType.Message"/>
        /// </summary>
        [Fact]
        public void Client_Raises_OnThrottled_YES()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            ISendOptions messageSendOptions = new SendOptions(0);
            IClientOptions clientOptions = new ClientOptions(messageSendOptions: messageSendOptions);
            T? client = GetClient(logger, clientOptions);
            Assert.NotNull(client);
            try
            {
                ManualResetEvent pauseConnected = new ManualResetEvent(false);
                ManualResetEvent pauseAssertion = new ManualResetEvent(false);

                Assert.Raises<OnMessageThrottledEventArgs>(
                    h => client.OnMessageThrottled += h,
                    h => client.OnMessageThrottled -= h,
                     () =>
                     {
                         client.OnConnected += (sender, e) =>
                         {
                             client.Send("PING");
                             pauseConnected.Set();
                         };

                         client.OnMessageThrottled += (sender, e) =>
                         {
                             Assert.NotNull(e);
                             Assert.NotNull(e.ItemNotSent);
                             Assert.Equal(0, e.SentCount);
                             Assert.Equal((uint) 0, e.AllowedInPeriod);
                             pauseAssertion.Set();
                         };

                         client.Open();
                         Assert.True(pauseConnected.WaitOne(5_000));
                         Assert.True(pauseAssertion.WaitOne(5_000));
                     });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                TheFinally(client);
            }
        }
        /// <summary>
        ///     checks <see cref="ClientOptions.MessageSendOptions"/>
        ///     <br></br>
        ///     <see cref="ISendOptions.SendsAllowedInPeriod"/> = 0
        ///     <br></br>
        ///     sending <see cref="Enums.MessageType.ByPass"/> via
        ///     <br></br>
        ///     <see cref="IClient.SendPONG"/>
        /// </summary>
        [Fact]
        public void Client_Raises_OnThrottled_NO()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            ISendOptions messageSendOptions = new SendOptions(0);
            IClientOptions clientOptions = new ClientOptions(messageSendOptions: messageSendOptions);
            T? client = GetClient(logger, clientOptions);
            Assert.NotNull(client);
            try
            {
                ManualResetEvent pauseConnected = new ManualResetEvent(false);

                Assert.Raises<OnMessageThrottledEventArgs>(
                    h => client.OnMessageThrottled += h,
                    h => client.OnMessageThrottled -= h,
                     () =>
                     {
                         client.OnConnected += (sender, e) =>
                         {
                             client.SendPONG();
                             pauseConnected.Set();
                         };

                         client.OnMessageThrottled += (sender, e) =>
                         {
                             Assert.NotNull(e);
                             Assert.Fail(e.ToString());
                         };

                         client.Open();
                         Assert.True(pauseConnected.WaitOne(5_000));
                     });
            }
            catch (Exception e)
            {
                // this is good in this case
                Assert.IsType<RaisesException>(e);
                Assert.StartsWith("(No event was raised)", e.Message);
            }
            finally
            {
                TheFinally(client);
            }
        }
        private static void TheFinally(T? client)
        {
            client?.Dispose();
            Task.Delay(TimeSpan.FromSeconds(waitAfterDispose)).GetAwaiter().GetResult();
        }
        private static TClient? GetClient<TClient>(ILogger<TClient> logger, IClientOptions? options = null)
        {
            Type[] constructorParameterTypes = new Type[] {
                typeof(IClientOptions),
                typeof(ILogger<TClient>)
            };
            ConstructorInfo? constructor = typeof(TClient).GetConstructor(constructorParameterTypes);
            object[] constructorParameters = new object[] {
                options??new ClientOptions(),
                logger
            };
            return (TClient?) constructor?.Invoke(constructorParameters);

        }
    }
}