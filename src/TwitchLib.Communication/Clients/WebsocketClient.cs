using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace TwitchLib.Communication
{
    public class WebSocketClient : IClient, IDisposable
    {
        private string Url { get; }
        private ClientWebSocket _ws;
        private readonly IClientOptions _options;
        private bool _disconnectCalled;
        private bool _listenerRunning;
        private bool _senderRunning;
        private bool _whisperSenderRunning;
        private bool _monitorRunning;
        private bool _reconnecting;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private Task _monitor;
        private Task _listener;
        private Task _sender;
        private Task _whisperSender;
        private readonly Throttlers _throttlers;
        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;
        public int SendQueueLength => _throttlers.SendQueue.Count;
        public int WhisperQueueLength => _throttlers.WhisperQueue.Count;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public TimeSpan DefaultKeepAliveInterval
        {
            get => _ws.Options.KeepAliveInterval;
            set => _ws.Options.KeepAliveInterval = value;
        }

        #region Events
        public event EventHandler<OnDataEventArgs> OnData;
        public event EventHandler<OnMessageEventArgs> OnMessage;
        public event EventHandler<OnStateChangedEventArgs> OnStateChanged;
        public event EventHandler<OnConnectedEventArgs> OnConnected;
        public event EventHandler<OnDisconnectedEventArgs> OnDisconnected;
        public event EventHandler <OnErrorEventArgs> OnError;
        public event EventHandler <OnSendFailedEventArgs> OnSendFailed;
        public event EventHandler<OnFatalErrorEventArgs> OnFatality;
        public event EventHandler<OnMessageThrottledEventArgs> OnMessageThrottled;
        public event EventHandler<OnWhisperThrottledEventArgs> OnWhisperThrottled;
        public event EventHandler<OnReconnectedEventArgs> OnReconnected;
        #endregion

        public WebSocketClient(IClientOptions options = null)
        {
            _options = options ?? new ClientOptions();

            switch (_options.ClientType)
            {
                case ClientType.Chat:
                    Url = _options.UseSsl ? "wss://irc-ws.chat.twitch.tv:443" : "ws://irc-ws.chat.twitch.tv:80";
                    break;
                case ClientType.PubSub:
                    Url = _options.UseSsl ? "wss://pubsub-edge.twitch.tv:443" : "ws://pubsub-edge.twitch.tv:80";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _throttlers = new Throttlers(_options.ThrottlingPeriod, _options.WhisperThrottlingPeriod) { TokenSource = _tokenSource };

            InitializeClient();
            StartMonitor();
        }

        private void InitializeClient()
        {
            _ws = new ClientWebSocket();

            DefaultKeepAliveInterval = Timeout.InfiniteTimeSpan;

            if (_options.Headers == null) return;

            foreach (var h in _options.Headers)
            {
                try
                {
                    _ws.Options.SetRequestHeader(h.Item1, h.Item2);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public bool Open()
        {
            try
            {
                _disconnectCalled = false;
                _ws.ConnectAsync(new Uri(Url), _tokenSource.Token).Wait(15000);
                StartListener();
                StartSender();
                StartWhisperSender();
                _throttlers.StartThrottlingWindowReset();
                _throttlers.StartWhisperThrottlingWindowReset();

                Task.Run(() =>
                {
                    while (_ws.State != WebSocketState.Open)
                    { }
                }).Wait(15000);
                return _ws.State == WebSocketState.Open;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                throw;
            }
        }

        public bool Send(string message)
        {
            try
            {
                if (!IsConnected || SendQueueLength >= _options.SendQueueCapacity)
                {
                    return false;
                }

                Task.Run(() =>
                {
                    _throttlers.SendQueue.Add(new Tuple<DateTime, string>(DateTime.UtcNow, message));
                }).Wait(100, _tokenSource.Token);

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                throw;
            }
        }

        public bool SendWhisper(string message)
        {
            try
            {
                if (!IsConnected || WhisperQueueLength >= _options.WhisperQueueCapacity)
                {
                    return false;
                }

                Task.Run(() =>
                {
                    _throttlers.WhisperQueue.Add(new Tuple<DateTime, string>(DateTime.UtcNow, message));
                }).Wait(100, _tokenSource.Token);

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                throw;
            }
        }

        private void StartMonitor()
        {
            _monitor = Task.Run(() =>
            {
                _monitorRunning = true;
                var needsReconnect = false;
                try
                {
                    var lastState = IsConnected;
                    while (_ws != null && !_disposedValue && !_reconnecting)
                    {
                        if (lastState == IsConnected)
                        {
                            Thread.Sleep(200);
                            continue;
                        }
                        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { IsConnected = _ws.State == WebSocketState.Open, WasConnected = lastState});

                        if (IsConnected)
                            OnConnected?.Invoke(this, new OnConnectedEventArgs());

                        if (!IsConnected && !_reconnecting)
                        {
                            if (lastState && !_disconnectCalled && _options.ReconnectionPolicy != null && !_options.ReconnectionPolicy.AreAttemptsComplete())
                            {
                                needsReconnect = true;
                                break;
                            }
                            OnDisconnected?.Invoke(this, new OnDisconnectedEventArgs());
                            if (_ws.CloseStatus != null && _ws.CloseStatus != WebSocketCloseStatus.NormalClosure)
                                OnError?.Invoke(this, new OnErrorEventArgs { Exception = new Exception(_ws.CloseStatus + " " + _ws.CloseStatusDescription) });
                        }

                        lastState = IsConnected;
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                }
                if (needsReconnect && !_reconnecting && !_disconnectCalled)
                    Reconnect();
                _monitorRunning = false;
            }, _tokenSource.Token);
        }

        public void Reconnect()
        {
            Task.Run(() =>
            {
                _tokenSource.Cancel();
                _reconnecting = true;
                _throttlers.Reconnecting = true;

                if (!Task.WaitAll(new[] {_monitor, _listener, _sender, _whisperSender }, 15000))
                {
                    OnFatality?.Invoke(this, new OnFatalErrorEventArgs { Reason = "Fatal network error. Network services fail to shut down." });
                    _reconnecting = false;
                    _throttlers.Reconnecting = false;
                    _disconnectCalled = true;
                    _tokenSource.Cancel();
                    return;
                }

                OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { IsConnected = false, WasConnected = false });

                _tokenSource = new CancellationTokenSource();
                _throttlers.TokenSource = _tokenSource;

                while (!_disconnectCalled && !_disposedValue && !IsConnected && !_tokenSource.IsCancellationRequested)
                    try
                    {
                        InitializeClient();
                        if (!_monitorRunning)
                        {
                            StartMonitor();
                        }

                        _ws.ConnectAsync(new Uri(Url), _tokenSource.Token).Wait(15000);
                    }
                    catch
                    {
                        _ws.Dispose();
                        Thread.Sleep(_options.ReconnectionPolicy.GetReconnectInterval());
                        _options.ReconnectionPolicy.ProcessValues();
                        if (!_options.ReconnectionPolicy.AreAttemptsComplete()) continue;

                        OnFatality?.Invoke(this, new OnFatalErrorEventArgs { Reason = "Fatal network error. Max reconnect attemps reached." });
                        _reconnecting = false;
                        _throttlers.Reconnecting = false;
                        _disconnectCalled = true;
                        _tokenSource.Cancel();
                        return;
                    }

                if (!IsConnected) return;

                _reconnecting = false;
                _throttlers.Reconnecting = false;
                if (!_monitorRunning)
                    StartMonitor();
                if (!_listenerRunning)
                    StartListener();
                if (!_senderRunning)
                    StartSender();
                if (!_whisperSenderRunning)
                    StartWhisperSender();
                if (!_throttlers.ResetThrottlerRunning)
                    _throttlers.StartThrottlingWindowReset();
                if (_throttlers.ResetWhisperThrottlerRunning)
                    _throttlers.StartWhisperThrottlingWindowReset();

                OnReconnected?.Invoke(this, new OnReconnectedEventArgs());
            });
        }

        private void StartListener()
        {
            _listener = Task.Run(async () =>
            {
                _listenerRunning = true;
                try
                {
                    while (_ws.State == WebSocketState.Open && !_disposedValue && !_reconnecting)
                    {
                        var message = "";
                        var binary = new List<byte>();

                        READ:

                        var buffer = new byte[1024];
                        WebSocketReceiveResult res = null;

                        try
                        {
                            res = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _tokenSource.Token);
                        }
                        catch
                        {
                            _ws.Abort();
                            break;
                        }

                        if (res == null)
                            goto READ;

                        if (res.MessageType == WebSocketMessageType.Close)
                        {
                            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "SERVER REQUESTED CLOSE", _tokenSource.Token);
                        }

                        if (res.MessageType == WebSocketMessageType.Text)
                        {
                            if (!res.EndOfMessage)
                            {
                                message += Encoding.UTF8.GetString(buffer).TrimEnd('\0');
                                goto READ;
                            }
                            message += Encoding.UTF8.GetString(buffer).TrimEnd('\0');

                            if (message.Trim() == "ping")
                                Send("pong");
                            else
                            {
                                Task.Run(() => OnMessage?.Invoke(this, new OnMessageEventArgs { Message = message })).Wait(50);
                            }
                        }
                        else
                        {
                            if (!res.EndOfMessage)
                            {
                                binary.AddRange(buffer.Where(b => b != '\0'));
                                goto READ;
                            }

                            binary.AddRange(buffer.Where(b => b != '\0'));
                            Task.Run(() => OnData?.Invoke(this, new OnDataEventArgs { Data = binary.ToArray() })).Wait(50);
                        }
                        buffer = null;
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                }
                _listenerRunning = false;
                return Task.CompletedTask;
            });
        }

        private void StartSender()
        {
            _sender = Task.Run(async () =>
            {
                _senderRunning = true;
                try
                {
                    while (!_disposedValue && !_reconnecting)
                    {
                        await Task.Delay(_options.SendDelay);

                        if (_throttlers.SentCount == _options.MessagesAllowedInPeriod)
                        {
                            OnMessageThrottled?.Invoke(this, new OnMessageThrottledEventArgs
                            {
                                Message = "Message Throttle Occured. Too Many Messages within the period specified in WebsocketClientOptions.",
                                AllowedInPeriod = _options.MessagesAllowedInPeriod,
                                Period = _options.ThrottlingPeriod,
                                SentMessageCount = Interlocked.CompareExchange(ref _throttlers.SentCount, 0, 0)
                            });

                            continue;
                        }

                        if (_ws.State != WebSocketState.Open || _reconnecting) continue;

                        var msg = _throttlers.SendQueue.Take(_tokenSource.Token);
                        if (msg.Item1.Add(_options.SendCacheItemTimeout) < DateTime.UtcNow)
                        {
                            continue;
                        }
                        var buffer = Encoding.UTF8.GetBytes(msg.Item2);
                        try
                        {
                            await _ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _tokenSource.Token);
                            _throttlers.IncrementSentCount();
                        }
                        catch (Exception ex)
                        {
                            OnSendFailed?.Invoke(this, new OnSendFailedEventArgs { Data = msg.Item2, Exception = ex });
                            _ws.Abort();
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnSendFailed?.Invoke(this, new OnSendFailedEventArgs { Data = "", Exception = ex });
                    OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                }
                _senderRunning = false;
                return Task.CompletedTask;
            });
        }

        private void StartWhisperSender()
        {
            _whisperSender = Task.Run(async () =>
            {
                _whisperSenderRunning = true;
                try
                {
                    while (!_disposedValue && !_reconnecting)
                    {
                        await Task.Delay(_options.SendDelay);

                        if (_throttlers.WhispersSent == _options.WhispersAllowedInPeriod)
                        {
                            OnWhisperThrottled?.Invoke(this, new OnWhisperThrottledEventArgs
                            {
                                Message = "Whisper Throttle Occured. Too Many Whispers within the period specified in WebsocketClientOptions.",
                                AllowedInPeriod = _options.WhispersAllowedInPeriod,
                                Period = _options.WhisperThrottlingPeriod,
                                SentWhisperCount = Interlocked.CompareExchange(ref _throttlers.WhispersSent, 0, 0)
                            });

                            continue;
                        }

                        if (_ws.State != WebSocketState.Open || _reconnecting) continue;

                        var msg = _throttlers.WhisperQueue.Take(_tokenSource.Token);
                        if (msg.Item1.Add(_options.SendCacheItemTimeout) < DateTime.UtcNow)
                        {
                            continue;
                        }
                        var buffer = Encoding.UTF8.GetBytes(msg.Item2);
                        try
                        {
                            await _ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _tokenSource.Token);
                            _throttlers.IncrementWhisperCount();
                        }
                        catch (Exception ex)
                        {
                            OnSendFailed?.Invoke(this, new OnSendFailedEventArgs { Data = msg.Item2, Exception = ex });
                            _ws.Abort();
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnSendFailed?.Invoke(this, new OnSendFailedEventArgs { Data = "", Exception = ex });
                    OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                }
                _whisperSenderRunning = false;
                return Task.CompletedTask;
            });
        }
        
        public void Close(bool callDisconnect = true)
        {
            try
            {
                _disconnectCalled = callDisconnect;
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NORMAL SHUTDOWN", _tokenSource.Token).Wait(_options.DisconnectWait);
            }
            catch
            {
                // ignored
            }
        }

        #region IDisposable Support

        private bool _disposedValue;

        protected virtual void Dispose(bool disposing, bool waitForSendsToComplete)
        {
            if (_disposedValue) return;

            if (disposing)
            {
                if (_throttlers.SendQueue.Count > 0 && _senderRunning)
                {
                    var i = 0;
                    while (_throttlers.SendQueue.Count > 0 && _senderRunning)
                    {
                        i++;
                        Task.Delay(1000).Wait();
                        if(i > 25)
                            break;
                    }
                }
                if (_throttlers.WhisperQueue.Count > 0 && _whisperSenderRunning)
                {
                    var i = 0;
                    while (_throttlers.WhisperQueue.Count > 0 && _whisperSenderRunning)
                    {
                        i++;
                        Task.Delay(1000).Wait();
                        if (i > 25)
                            break;
                    }
                }
                Close();
                _tokenSource.Cancel();
                Thread.Sleep(500);
                _tokenSource.Dispose();
                _ws.Dispose();
                GC.Collect();
            }

            _disposedValue = true;
            _throttlers.ShouldDispose = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }


        public void Dispose(bool waitForSendsToComplete)
        {
            Dispose(true, waitForSendsToComplete);
        }

        #endregion
    }
}