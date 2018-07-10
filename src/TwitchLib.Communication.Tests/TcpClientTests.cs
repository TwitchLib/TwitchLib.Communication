using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;
using Xunit;

namespace TwitchLib.Communication.Tests
{
    public class TcpClientTests
    {
        [Fact]
        public void ClientRaisesOnConnectedEventArgs()
        {

            var client = new TcpClient();
            var pauseConnected = new ManualResetEvent(false);

            Assert.Raises<OnConnectedEventArgs>(
                h => client.OnConnected += h,
                h => client.OnConnected -= h,
                () =>
                {
                    client.OnConnected += (sender, e) => { pauseConnected.Set(); };
                    client.Open();
                    Assert.True(pauseConnected.WaitOne(5000));
                });
        }

        [Fact]
        public void ClientRaisesOnDisconnected()
        {
            var client = new TcpClient(new ClientOptions() {DisconnectWait = 5000});
            var pauseDisconnected = new ManualResetEvent(false);

            Assert.Raises<OnDisconnectedEventArgs>(
                h => client.OnDisconnected += h,
                h => client.OnDisconnected -= h,
                () =>
                {
                    client.OnConnected += (sender, e) => { client.Close(); };
                    client.OnDisconnected += (sender, e) => { pauseDisconnected.Set(); };
                    client.Open();
                    Assert.True(pauseDisconnected.WaitOne(5000));
                });
        }

        [Fact]
        public void ClientRaisesOnReconnectedEventArgs()
        {
            var client = new TcpClient();
            var pauseConnected = new ManualResetEvent(false);
            var pauseReconnected = new ManualResetEvent(false);

            Assert.Raises<OnReconnectedEventArgs>(
                h => client.OnReconnected += h,
                h => client.OnReconnected -= h,
                () =>
                {
                    client.OnConnected += (s, e) =>
                    {
                        Task.Run(async () =>
                        {
                            pauseConnected.Set();
                            await Task.Delay(1000);
                            client.Close(false);
                        });
                    };

                    client.OnReconnected += (s, e) => { pauseReconnected.Set(); };
                    client.Open();

                    Assert.True(pauseConnected.WaitOne(5000));
                    Assert.True(pauseReconnected.WaitOne(20000));
                });
        }

        [Fact]
        public void DisposeClientBeforeConnecting_IsOK()
        {
            var tcpClient = new TcpClient();
            tcpClient.Dispose();
        }

        [Fact]
        public void ClientCanSendAndReceiveMessages()
        {
            var client = new TcpClient();
            var pauseConnected = new ManualResetEvent(false);
            var pauseReadMessage = new ManualResetEvent(false);

            Assert.Raises<OnMessageEventArgs>(
                h => client.OnMessage += h,
                h => client.OnMessage -= h,
                () =>
                {
                    client.OnConnected += (sender, e) => { pauseConnected.Set(); };

                    client.OnMessage += (sender, e) =>
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
