using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Communication.Enums;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Services;

namespace TwitchLib.Communication.Clients
{
    public class WebSocketClient : IClient
    {
        #region Private Variables

        private readonly Throttlers _throttlers;
        private readonly string _connectionUri;

        private Task _monitorService;
        private Task[] _networkServices;

        private WebSocketState Status => Client?.State ?? WebSocketState.None;

        #endregion

        #region Public Variables

        public ClientWebSocket Client { get; private set; }
        public CancellationTokenSource TokenSource { get; set; }
        public IClientOptions Options { get; }

        public bool IsConnected => Status == WebSocketState.Open;

        public event EventHandler<OnConnectedEventArgs> OnConnected;
        public event EventHandler<OnReconnectedEventArgs> OnReconnected;
        public event EventHandler<OnDisconnectedEventArgs> OnDisconnected;
        public event EventHandler<OnMessageEventArgs> OnMessage;
        public event EventHandler<OnMessageThrottledEventArgs> OnMessageThrottled;
        public event EventHandler<OnWhisperThrottledEventArgs> OnWhisperThrottled;
        public event EventHandler<OnStateChangedEventArgs> OnStateChanged;
        public event EventHandler<OnSendFailedEventArgs> OnSendFailed;
        public event EventHandler<OnErrorEventArgs> OnError;
        public event EventHandler<OnFatalErrorEventArgs> OnFatality;

        #endregion

        public WebSocketClient(IClientOptions options = null, CancellationTokenSource tokenSource = null)
        {
            TokenSource = tokenSource ?? new CancellationTokenSource();
            Options = options ?? new ClientOptions();

            switch (Options.ClientType)
            {
                case ClientType.Chat:
                    _connectionUri = Options.UseSsl 
                        ? "wss://irc-ws.chat.twitch.tv:443" 
                        : "ws://irc-ws.chat.twitch.tv:80";
                    break;
                
                case ClientType.PubSub:
                    _connectionUri = Options.UseSsl
                        ? "wss://pubsub-edge.twitch.tv:443"
                        : "ws://pubsub-edge.twitch.tv:80";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _throttlers = new Throttlers(this) { TokenSource = TokenSource };
        }

        /// <summary>
        /// Initialize the Websocket Client
        /// </summary>
        private void Initialize()
        {
            Client = new ClientWebSocket();
            TokenSource = new CancellationTokenSource();
            _throttlers.TokenSource = TokenSource;

            if (_monitorService == null || _monitorService.IsCompleted)
                _monitorService = StartMonitorService();
        }

        /// <summary>
        /// Connect to Websocket (Alternative to Connect())
        /// </summary>
        /// <returns></returns>
        public bool Open()
        {
            return Connect();
        }

        /// <summary>
        /// Connect to Websocket
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            try
            {
                if (IsConnected != null && IsConnected)
                    return true;

                Initialize();
                Client.ConnectAsync(new Uri(_connectionUri), TokenSource.Token).Wait(10000);
                StartNetworkServices();
                return true;
            }
            catch
            {
                CleanupServices();
                return false;
            }
        }

        /// <summary>
        /// Reconnect to Websocket if needed
        /// </summary>
        public void Reconnect()
        {
            Disconnect();
            if (Connect())
                OnReconnected?.Invoke(this, new OnReconnectedEventArgs());
        }

        /// <summary>
        /// Disconnect from Websocket (Alternative to Disconnect())
        /// </summary>
        public void Close()
        {
            Disconnect();
        }

        /// <summary>
        /// Disconnect from Websocket
        /// </summary>
        public void Disconnect()
        {
            Client.Abort();
            CleanupServices();
        }

        /// <summary>
        /// Start all Network Services
        /// </summary>
        private void StartNetworkServices()
        {
            _networkServices = new[]
            {
                StartListenerService(),
                _throttlers.StartMessageSenderTask(),
                _throttlers.StartWhisperSenderTask()
            };

            if (!_networkServices.Any(service => service.IsFaulted))
                return;

            CleanupServices();
        }

        /// <summary>
        /// Invokes OnMessage Event when message received from Twitch
        /// </summary>
        /// <returns></returns>
        private Task StartListenerService()
        {
            return Task.Run(async () =>
            {
                var buffer = new ArraySegment<byte>(new byte[8192]);

                do
                {
                    WebSocketReceiveResult result;
                    var memory = new MemoryStream();

                    do
                    {
                        result = await Client.ReceiveAsync(buffer, TokenSource.Token);

                        if (buffer.Array != null)
                            memory.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage && !TokenSource.IsCancellationRequested);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Disconnect();
                        break;
                    }

                    memory.Seek(0, SeekOrigin.Begin);

                    var reader = new StreamReader(memory, Encoding.UTF8);

                    OnMessage?.Invoke(this, new OnMessageEventArgs { Message = await reader.ReadToEndAsync() });
                    
                    memory.Dispose();
                    reader.Dispose();
                } while (!TokenSource.IsCancellationRequested);
            });
        }

        /// <summary>
        /// Monitor the status of the Websocket Client
        /// Call Connect, Disconnect
        /// Reconnect if disconnect is not requested
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private Task StartMonitorService()
        {
            return Task.Run(() =>
            {
                try
                {
                    var lastState = IsConnected;
                    var needsReconnect = false;

                    while (!TokenSource.IsCancellationRequested && !needsReconnect)
                    {
                        Thread.Sleep(200);

                        if (lastState == IsConnected)
                            continue;

                        switch (Status)
                        {
                            case WebSocketState.Open:
                                OnConnected?.Invoke(this, new OnConnectedEventArgs());

                                if (Options.ReconnectionPolicy.GetAttempt() != 0)
                                    Options.ReconnectionPolicy.Reset();
                                break;

                            case WebSocketState.Aborted when TokenSource.IsCancellationRequested:
                                OnDisconnected?.Invoke(this, new OnDisconnectedEventArgs());
                                break;

                            case WebSocketState.Aborted when !TokenSource.IsCancellationRequested:
                                var tryAgainIn = Options.ReconnectionPolicy.GetTimeout();
                                Thread.Sleep(tryAgainIn);
                                Reconnect();
                                needsReconnect = true;
                                break;
                            case WebSocketState.Closed:
                                break;
                            case WebSocketState.CloseReceived:
                                break;
                            case WebSocketState.CloseSent:
                                break;
                            case WebSocketState.Connecting:
                                break;
                            case WebSocketState.None:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        OnStateChanged?.Invoke(this,
                            new OnStateChangedEventArgs { IsConnected = IsConnected, WasConnected = lastState });

                        lastState = IsConnected;
                    }
                }
                catch (Exception e)
                {
                    OnError?.Invoke(this, new OnErrorEventArgs { Exception = e });
                }

                return Task.CompletedTask;
            }, TokenSource.Token);
        }

        /// <summary>
        /// Sending a message using the Throttler (alternative to SendMessage())
        /// </summary>
        /// <param name="message"></param>
        /// <returns>True if message send successfully</returns>
        public bool Send(string message)
        {
            return SendMessage(message);
        }

        /// <summary>
        /// Sending a message using the Throttler
        /// </summary>
        /// <param name="message"></param>
        /// <returns>True if message send successfully</returns>
        public bool SendMessage(string message)
        {
            try
            {
                if (!IsConnected || _throttlers.MessageQueue.Count >= Options.MessageQueueCapacity)
                    return false;

                _throttlers.MessageQueue.Add(new Tuple<DateTime, string>(DateTime.UtcNow, message));

                return true;
            }
            catch (Exception e)
            {
                OnError?.Invoke(this, new OnErrorEventArgs { Exception = e });
                throw;
            }
        }

        /// <summary>
        /// Sending a whisper using the Throttler
        /// </summary>
        /// <param name="message"></param>
        /// <returns>True if whisper send successfully</returns>
        public bool SendWhisper(string message)
        {
            try
            {
                if (!IsConnected || _throttlers.WhisperQueue.Count >= Options.WhisperQueueCapacity)
                    return false;

                _throttlers.WhisperQueue.Add(new Tuple<DateTime, string>(DateTime.UtcNow, message));

                return true;
            }
            catch (Exception e)
            {
                OnError?.Invoke(this, new OnErrorEventArgs { Exception = e });
                throw;
            }
        }

        /// <summary>
        /// Mainly for Throttler, Send bytes to TwitchIrc
        /// </summary>
        /// <param name="message">message as bytes</param>
        public Task SendAsync(byte[] message)
        {
            return Client.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Text, true,
                TokenSource.Token);
        }

        /// <summary>
        /// Clean up Services
        /// </summary>
        private void CleanupServices()
        {
            Dispose();

            if (_networkServices != null &&
                _networkServices.Any(task => task.IsCanceled || task.IsCompleted || task.IsFaulted))
                return;

            OnFatality?.Invoke(this, new OnFatalErrorEventArgs
            {
                Reason = "Fatal network error. Network services fail to shut down."
            });
        }

        public void MessageThrottled(OnMessageThrottledEventArgs eventArgs)
        {
            OnMessageThrottled?.Invoke(this, eventArgs);
        }

        public void WhisperThrottled(OnWhisperThrottledEventArgs eventArgs)
        {
            OnWhisperThrottled?.Invoke(this, eventArgs);
        }

        public void SendFailed(OnSendFailedEventArgs eventArgs)
        {
            OnSendFailed?.Invoke(this, eventArgs);
        }

        public void Error(OnErrorEventArgs eventArgs)
        {
            OnError?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Cancel Token, Dispose Token and Client and collect Garbage
        /// </summary>
        public void Dispose()
        {
            TokenSource.Cancel();
            Thread.Sleep(200);
            TokenSource.Dispose();
            Client.Dispose();
            GC.Collect();
        }
    }
}