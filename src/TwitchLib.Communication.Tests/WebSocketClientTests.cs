using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;
using Xunit;

namespace TwitchLib.Communication.Tests
{
    public class WebSocketClientTests
    {
        [Fact]
        public void Client_Raises_OnConnected_EventArgs()
        {
            var client = new WebSocketClient();
            var pauseConnected = new ManualResetEvent(false);

            Assert.Raises<ConnectedEventArgs>(
                h => client.Connected += h,
                h => client.Connected -= h,
                () =>
                {
                    client.Connected += (sender, e) => { pauseConnected.Set(); };
                    client.Open();
                    Assert.True(pauseConnected.WaitOne(5000));
                });
        }

        [Fact]
        public void Client_Raises_OnDisconnected_EventArgs()
        {
            var client = new WebSocketClient(new ClientOptions(disconnectWait: 5000));
            var pauseDisconnected = new ManualResetEvent(false);

            Assert.Raises<DisconnectedEventArgs>(
                h => client.Disconnected += h,
                h => client.Disconnected -= h,
                () =>
                {
                    client.Connected += async (sender, e) =>
                    {
                        await Task.Delay(2000);
                        client.Close();
                    };
                    client.Disconnected += (sender, e) =>
                    {
                        pauseDisconnected.Set();
                    };
                    client.Open();
                    Assert.True(pauseDisconnected.WaitOne(200000));
                });
        }

        [Fact]
        public void Client_Raises_OnReconnected_EventArgs()
        {
            var client = new WebSocketClient(new ClientOptions());
            var pauseReconnected = new ManualResetEvent(false);

            Assert.Raises<ReconnectedEventArgs>(
                h => client.Reconnected += h,
                h => client.Reconnected -= h,
                () =>
                {
                    client.Connected += async (s, e) =>
                    {
                        await Task.Delay(2000);
                        client.Reconnect();
                    };

                    client.Reconnected += (s, e) => { pauseReconnected.Set(); };
                    client.Open();

                    Assert.True(pauseReconnected.WaitOne(20000));
                });
        }

        [Fact]
        public void Dispose_Client_Before_Connecting_IsOK()
        {
            var client = new WebSocketClient();
            client.Dispose();
        }

       
        [Fact]
        public void Client_Can_SendAndReceive_Messages()
        {
            var client = new WebSocketClient();
            var pauseConnected = new ManualResetEvent(false);
            var pauseReadMessage = new ManualResetEvent(false);

            Assert.Raises<MessageEventArgs>(
                h => client.Message += h,
                h => client.Message -= h,
                () =>
                {
                    client.Connected += (sender, e) => { pauseConnected.Set(); };

                    client.Message += (sender, e) =>
                    {
                        pauseReadMessage.Set();
                        Assert.Equal("PONG :tmi.twitch.tv", e.Message);
                    };

                    client.Open();
                    client.Send("PING");
                    Assert.True(pauseConnected.WaitOne(5000));
                    Assert.True(pauseReadMessage.WaitOne(5000));
                });
        }
    }
}
