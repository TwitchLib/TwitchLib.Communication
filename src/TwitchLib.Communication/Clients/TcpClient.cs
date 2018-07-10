using System;
using System.ComponentModel;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Services;

namespace TwitchLib.Communication
{
    public class TcpClient : IClient, IDisposable
    {
        public bool IsConnected => _client?.Connected ?? false;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public TimeSpan DefaultKeepAliveInterval
        {
            get => TimeSpan.FromMilliseconds(_client.ReceiveTimeout);
            set => _client.ReceiveTimeout = value.Milliseconds;
        }

        public int SendQueueLength => _throttlers.SendQueue.Count;
        public int WhisperQueueLength => _throttlers.WhisperQueue.Count;
        private readonly string _server = "irc.chat.twitch.tv";
        private int Port => _options != null ? _options.UseSsl ? 443 : 80 : 0;
        private System.Net.Sockets.TcpClient _client;
        private NetworkStream _stream;
        private SslStream _ssl;
        private StreamReader _reader;
        private StreamWriter _writer;
        private readonly IClientOptions _options;
        private readonly Throttlers _throttlers;
        private Task _listener;
        private Task _monitor;
        private Task _sender;
        private Task _whisperSender;
        private bool _monitorRunning;
        private bool _listenerRunning;
        private bool _disposedValue;
        private bool _reconnecting;
        private bool _disconnectCalled;
        private bool _senderRunning;
        private bool _whisperSenderRunning;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        public event EventHandler<OnConnectedEventArgs> OnConnected;
        public event EventHandler<OnDataEventArgs> OnData;
        public event EventHandler<OnDisconnectedEventArgs> OnDisconnected;
        public event EventHandler<OnErrorEventArgs> OnError;
        public event EventHandler<OnFatalErrorEventArgs> OnFatality;
        public event EventHandler<OnMessageEventArgs> OnMessage;
        public event EventHandler<OnMessageThrottledEventArgs> OnMessageThrottled;
        public event EventHandler<OnWhisperThrottledEventArgs> OnWhisperThrottled;
        public event EventHandler<OnSendFailedEventArgs> OnSendFailed;
        public event EventHandler<OnStateChangedEventArgs> OnStateChanged;
        public event EventHandler<OnReconnectedEventArgs> OnReconnected;

        public TcpClient(IClientOptions options = null)
        {
            _options = options ?? new ClientOptions();
            _throttlers = new Throttlers(_options.ThrottlingPeriod, _options.WhisperThrottlingPeriod){TokenSource = _tokenSource};
            InitializeClient();
            StartMonitor();
        }

        private void InitializeClient()
        {
            _client = new System.Net.Sockets.TcpClient();
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
                    while (_client != null && !_disposedValue)
                    {
                        if (lastState == IsConnected)
                        {
                            Thread.Sleep(200);
                            continue;
                        }
                        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { IsConnected = IsConnected, WasConnected = lastState });

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
                        }

                        lastState = IsConnected;
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                }
                if (needsReconnect && !_reconnecting && !_disconnectCalled)
                    DoReconnect();
                _monitorRunning = false;
            });
        }

        private Task DoReconnect()
        {
            return Task.Run(() =>
            {
                _tokenSource.Cancel();
                _reconnecting = true;
                _throttlers.Reconnecting = true;

                if (!Task.WaitAll(new[] { _monitor, _listener, _sender, _whisperSender }, 15000))
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
                        Connect().Wait(15000);
                    }
                    catch
                    {
                        Close();
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

                        if (!IsConnected || _reconnecting) continue;

                        var msg = _throttlers.WhisperQueue.Take(_tokenSource.Token);
                        if (msg.Item1.Add(_options.SendCacheItemTimeout) < DateTime.UtcNow)
                        {
                            continue;
                        }
                        try
                        {
                            await SendMessage(msg.Item2);
                            _throttlers.IncrementWhisperCount();
                        }
                        catch (Exception ex)
                        {
                            OnSendFailed?.Invoke(this, new OnSendFailedEventArgs { Data = msg.Item2, Exception = ex });
                            Close();
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

                        if (!IsConnected || _reconnecting) continue;
                        var msg = _throttlers.SendQueue.Take(_tokenSource.Token);
                        if (msg.Item1.Add(_options.SendCacheItemTimeout) < DateTime.UtcNow)
                        {
                            continue;
                        }
                        try
                        {
                            await SendMessage(msg.Item2);
                            _throttlers.IncrementSentCount();
                        }
                        catch (Exception ex)
                        {
                            OnSendFailed?.Invoke(this, new OnSendFailedEventArgs { Data = msg.Item2, Exception = ex });
                            Close();
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
            });
        }

        private Task Connect()
        {
            return Task.Run(() =>
            {
                try
                {
                    _client.Connect(_server, Port);
                    _stream = _client.GetStream();
                    if (_options.UseSsl)
                    {
                        _ssl = new SslStream(_stream, false);
                        _ssl.AuthenticateAsClient(_server);
                        _reader = new StreamReader(_ssl);
                        _writer = new StreamWriter(_ssl);
                    }
                    else
                    {
                        _reader = new StreamReader(_stream);
                        _writer = new StreamWriter(_stream);
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                }
            });
        }

        private void StartListener()
        {
            _listener = Task.Run(async () =>
            {
                _listenerRunning = true;
                while (IsConnected && !_reconnecting)
                {
                    try
                    {
                        var input = await _reader.ReadLineAsync();
                        OnMessage?.Invoke(this, new OnMessageEventArgs { Message = input });

                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                    }
                }
                _listenerRunning = false;
            });
        }

        private async Task SendMessage(string message)
        {
            await _writer.WriteLineAsync(message);
            await _writer.FlushAsync();
        }

        public void Close(bool callDisconnect = true)
        {
            _disconnectCalled = callDisconnect;
            
            _client.Close();

            if (_options.UseSsl)
            {
                _stream?.Dispose();
                _ssl?.Dispose();
                _writer?.Dispose();
                _reader?.Dispose();
            }
            else
            {
                _stream?.Dispose();
                _writer?.Dispose();
                _reader?.Dispose();
            }
        }

        public bool Open()
        {
            try
            {
                _disconnectCalled = false;

                Connect().Wait(15000);
                StartListener();
                StartSender();
                StartWhisperSender();
                _throttlers.StartThrottlingWindowReset();
                _throttlers.StartWhisperThrottlingWindowReset();

                Task.Run(() =>
                {
                    while (!IsConnected)
                    { }
                }).Wait(15000);
                return IsConnected;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                throw;
            }
        }

        public bool Send(string data)
        {
            try
            {
                if (!IsConnected || SendQueueLength >= _options.SendQueueCapacity)
                {
                    return false;
                }

                Task.Run(() =>
                {
                    _throttlers.SendQueue.Add(new Tuple<DateTime, string>(DateTime.UtcNow, data));
                }).Wait(100, _tokenSource.Token);

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                throw;
            }
        }

        public bool SendWhisper(string data)
        {
            try
            {
                if (!IsConnected || WhisperQueueLength >= _options.WhisperQueueCapacity)
                {
                    return false;
                }

                Task.Run(() =>
                {
                    _throttlers.WhisperQueue.Add(new Tuple<DateTime, string>(DateTime.UtcNow, data));
                }).Wait(100, _tokenSource.Token);

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new OnErrorEventArgs { Exception = ex });
                throw;
            }
        }

        #region IDisposable Support

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
                        if (i > 25)
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