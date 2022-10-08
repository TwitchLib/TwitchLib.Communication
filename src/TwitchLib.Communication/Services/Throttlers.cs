using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Services
{
    public class Throttlers
    {
        public readonly BlockingCollection<Tuple<DateTime, string>> MessageQueue =
            new BlockingCollection<Tuple<DateTime, string>>();

        public readonly BlockingCollection<Tuple<DateTime, string>> WhisperQueue =
            new BlockingCollection<Tuple<DateTime, string>>();

        public CancellationTokenSource TokenSource { get; set; }

        public int MessageSent = 0;
        public int WhispersSent = 0;

        public Task ResetMessageThrottler;
        public Task ResetWhisperThrottler;
        public bool ResetMessageThrottlerRunning;
        public bool ResetWhisperThrottlerRunning;
        public TimeSpan MessageThrottlingPeriod;
        public TimeSpan WhisperThrottlingPeriod;

        private readonly IClient _client;

        public Throttlers(IClient client)
        {
            _client = client;
            MessageThrottlingPeriod = _client.Options.MessageThrottlingPeriod;
            WhisperThrottlingPeriod = _client.Options.WhisperThrottlingPeriod;
        }

        public void StartThrottlingWindowReset()
        {
            ResetMessageThrottler = Task.Run(async () =>
            {
                ResetMessageThrottlerRunning = true;
                while (!TokenSource.IsCancellationRequested)
                {
                    Interlocked.Exchange(ref MessageSent, 0);
                    await Task.Delay(MessageThrottlingPeriod, TokenSource.Token);
                }

                ResetMessageThrottlerRunning = false;
                return Task.CompletedTask;
            });
        }

        public void StartWhisperThrottlingWindowReset()
        {
            ResetWhisperThrottler = Task.Run(async () =>
            {
                ResetWhisperThrottlerRunning = true;
                while (!TokenSource.IsCancellationRequested)
                {
                    Interlocked.Exchange(ref WhispersSent, 0);
                    await Task.Delay(WhisperThrottlingPeriod, TokenSource.Token);
                }

                ResetWhisperThrottlerRunning = false;
                return Task.CompletedTask;
            });
        }

        public Task StartMessageSenderTask()
        {
            StartThrottlingWindowReset();

            return Task.Run(async () =>
            {
                try
                {
                    while (!TokenSource.IsCancellationRequested)
                    {
                        await Task.Delay(_client.Options.SendDelay);

                        if (MessageSent == _client.Options.MessagesAllowedInPeriod)
                        {
                            _client.MessageThrottled(new OnMessageThrottledEventArgs
                            {
                                Message =
                                    "Message Throttle Occured. Too Many Messages within the period specified in WebsocketClientOptions.",
                                AllowedInPeriod = _client.Options.MessagesAllowedInPeriod,
                                Period = _client.Options.MessageThrottlingPeriod,
                                SentMessageCount = Interlocked.CompareExchange(ref MessageSent, 0, 0)
                            });

                            continue;
                        }

                        if (!_client.IsConnected || TokenSource.IsCancellationRequested) continue;

                        var msg = MessageQueue.Take(TokenSource.Token);
                        if (msg.Item1.Add(_client.Options.SendCacheItemTimeout) < DateTime.UtcNow) continue;

                        try
                        {
                            switch (_client)
                            {
                                case WebSocketClient ws:
                                    await ws.SendAsync(Encoding.UTF8.GetBytes(msg.Item2));
                                    break;
                                /*case TcpClient tcp:
                                    await tcp.SendAsync(msg.Item2);
                                    break;*/
                            }

                            Interlocked.Increment(ref MessageSent);
                        }
                        catch (Exception ex)
                        {
                            _client.SendFailed(new OnSendFailedEventArgs { Data = msg.Item2, Exception = ex });
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _client.SendFailed(new OnSendFailedEventArgs { Data = "", Exception = ex });
                    _client.Error(new OnErrorEventArgs { Exception = ex });
                }
            });
        }

        public Task StartWhisperSenderTask()
        {
            StartWhisperThrottlingWindowReset();

            return Task.Run(async () =>
            {
                try
                {
                    while (!TokenSource.IsCancellationRequested)
                    {
                        await Task.Delay(_client.Options.SendDelay);

                        if (WhispersSent == _client.Options.WhispersAllowedInPeriod)
                        {
                            _client.WhisperThrottled(new OnWhisperThrottledEventArgs()
                            {
                                Message =
                                    "Whisper Throttle Occured. Too Many Whispers within the period specified in ClientOptions.",
                                AllowedInPeriod = _client.Options.WhispersAllowedInPeriod,
                                Period = _client.Options.WhisperThrottlingPeriod,
                                SentWhisperCount = Interlocked.CompareExchange(ref WhispersSent, 0, 0)
                            });

                            continue;
                        }

                        if (!_client.IsConnected || TokenSource.IsCancellationRequested) continue;

                        var msg = WhisperQueue.Take(TokenSource.Token);
                        if (msg.Item1.Add(_client.Options.SendCacheItemTimeout) < DateTime.UtcNow) continue;

                        try
                        {
                            switch (_client)
                            {
                                case WebSocketClient ws:
                                    await ws.SendAsync(Encoding.UTF8.GetBytes(msg.Item2));
                                    break;
                                //case TcpClient tcp:
                                //    await tcp.SendAsync(msg.Item2);
                                //    break;
                            }

                            Interlocked.Increment(ref WhispersSent);
                        }
                        catch (Exception ex)
                        {
                            _client.SendFailed(new OnSendFailedEventArgs { Data = msg.Item2, Exception = ex });
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _client.SendFailed(new OnSendFailedEventArgs { Data = "", Exception = ex });
                    _client.Error(new OnErrorEventArgs { Exception = ex });
                }
            });
        }
    }
}