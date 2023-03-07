using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Enums;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Extensions;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;

namespace TwitchLib.Communication.Services
{

    internal class ThrottlerService
    {
        #region variables private
        private long sentMessageCount = 0;
        private long sentWhisperCount = 0;
        #endregion variables private



        #region properties private
        private ILogger LOGGER { get; }
        private IDictionary<MessageType, ISendOptions> Options { get; } = new Dictionary<MessageType, ISendOptions>();
        private IDictionary<MessageType, ConcurrentQueue<Tuple<DateTime, string>>> Queues { get; } = new Dictionary<MessageType, ConcurrentQueue<Tuple<DateTime, string>>>();
        private AClientBase Client { get; }
        private CancellationToken Token => Client.Token;
        private Timer ResetThrottlingWindowTimer { get; set; }
        /// <summary>
        ///     get is never used, cause the <see cref="Task"/> is canceled by the <see cref="Token"/>
        /// </summary>
        [SuppressMessage("Style", "IDE0052")]
        private Task SendTask { get; set; }
        #endregion properties private


        #region ctors
        internal ThrottlerService(AClientBase client,
                                  ISendOptions messageSendOptions,
                                  ISendOptions whisperSendOptions,
                                  ILogger logger = null)
        {
            LOGGER = logger;
            Client = client;
            // just to have a default value in there;
            // its never used for ByPass
            // but it avoids TryGet etsy within the code
            // has to be the value greater than zero
            // otherwise ByPass Messages are always throttled
            // sending ByPass-Messages is not counted by this ThrottlerService,
            // the value has no impact,
            // as long as it is greater than zero
            Queues.Add(MessageType.ByPass, new ConcurrentQueue<Tuple<DateTime, string>>());
            //
            Options.Add(MessageType.Whisper, whisperSendOptions);
            Queues.Add(MessageType.Whisper, new ConcurrentQueue<Tuple<DateTime, string>>());
            //
            Options.Add(MessageType.Message, messageSendOptions);
            Queues.Add(MessageType.Message, new ConcurrentQueue<Tuple<DateTime, string>>());
        }
        #endregion ctors


        #region methods internal
        internal void Start()
        {
            LOGGER?.TraceMethodCall(GetType());
            StartResetWindowTimer();
            StartSendTask();
        }
        internal void Stop()
        {
            LOGGER?.TraceMethodCall(GetType());
            // the clients CancellationTokenSource.Token cancels the ListenTask
            // so we only have to Dispose the timer
            ResetThrottlingWindowTimer?.Dispose();
        }
        // to restrict access to the queues to this class/instances of this class
        internal bool Enqueue(string message, MessageType messageType)
        {
            LOGGER?.TraceMethodCall(GetType());
            try
            {
                ConcurrentQueue<Tuple<DateTime, string>> queue = Queues[messageType];
                ISendOptions sendOptions = Options[messageType];
                if (!Client.IsConnected || queue.Count >= sendOptions.QueueCapacity)
                {
                    return false;
                }
                Queues[messageType].Enqueue(new Tuple<DateTime, string>(DateTime.UtcNow, message));
                return true;
            }
            catch (Exception ex)
            {
                LOGGER?.LogExceptionAsError(GetType(), ex);
                Client.RaiseError(new OnErrorEventArgs { Exception = ex });
                throw;
            }
        }
        #endregion methods internal


        #region Actions for NetworkServices: Messages
        private void StartResetWindowTimer()
        {
            LOGGER?.TraceMethodCall(GetType());
            ResetThrottlingWindowTimer = new Timer(ResetCallback,
                                                   null,
                                                   TimeSpan.FromSeconds(0),
                                                   TimeSpan.FromSeconds(20));
        }
        [SuppressMessage("Style", "IDE0058")]
        private void ResetCallback(object state)
        {
            LOGGER?.TraceMethodCall(GetType());
            Interlocked.Exchange(ref sentMessageCount, 0);
            Interlocked.Exchange(ref sentWhisperCount, 0);
        }
        private void StartSendTask()
        {
            LOGGER?.TraceMethodCall(GetType());
            SendTask = Task.Run(SendTaskAction, Token);
        }
        private void SendTaskAction()
        {
            LOGGER?.TraceMethodCall(GetType());
            while (Client.IsConnected && !Token.IsCancellationRequested)
            {
                MessageType[] messageTypes = (MessageType[]) Enum.GetValues(typeof(MessageType));
                foreach (MessageType messageType in messageTypes)
                {
                    TrySend(messageType);
                    Task.Delay(Client.Options.SendDelay).GetAwaiter().GetResult();
                }
            }
        }

        private void TrySend(MessageType messageType)
        {
            // to be able to access msg within catch
            Tuple<DateTime, string> msg = null;

            ConcurrentQueue<Tuple<DateTime, string>> queue = Queues[messageType];
            ISendOptions options = Options[messageType];
            long localSentCount = ReadSentCount(messageType);
            try
            {
                if (localSentCount >= options.SendsAllowedInPeriod)
                {
                    Throttle(messageType,
                             msg?.Item2,
                             options,
                             localSentCount);
                    return;
                }
                bool taken = queue.TryDequeue(out msg);
                if (!taken || msg == null)
                {
                    return;
                }
                if (msg.Item1.Add(options.CacheItemTimeout) < DateTime.UtcNow)
                {
                    return;
                }

                Client.SendIRC(msg.Item2);

                IncrementSentCount(messageType);
            }
            catch (Exception ex) when (ex.GetType() == typeof(TaskCanceledException) || ex.GetType() == typeof(OperationCanceledException))
            {
                // occurs if the Tasks are canceled by the CancelationTokenSource.Token
                LOGGER?.LogExceptionAsInformation(GetType(), ex);
            }
            catch (Exception ex)
            {
                LOGGER?.LogExceptionAsError(GetType(), ex);
                // msg may be null
                Client.RaiseSendFailed(new OnSendFailedEventArgs { Data = msg?.Item2, Exception = ex });
            }
        }

        private long ReadSentCount(MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Message:
                    return Interlocked.Read(ref sentMessageCount);
                case MessageType.Whisper:
                    return Interlocked.Read(ref sentWhisperCount);
                default:
                    return 0;
            }
        }

        [SuppressMessage("Style", "IDE0058")]
        private void IncrementSentCount(MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Message:
                    Interlocked.Increment(ref sentMessageCount);
                    break;
                case MessageType.Whisper:
                    Interlocked.Increment(ref sentWhisperCount);
                    break;
                default:
                    break;
            }
        }

        private void Throttle(MessageType messageType,
                              string itemNotSent,
                              ISendOptions options,
                              long sentCount)
        {
            LOGGER?.TraceMethodCall(GetType());
            LOGGER?.TraceAction(GetType(), "Message throttled");
            string msg = $"{messageType} Throttle Occured. Too Many {messageType}s within the period specified in WebsocketClientOptions.";
            IOnThrottledEventArgs onThrottledEventArgs = new OnThrottledEventArgs
            {
                ItemNotSent = itemNotSent,
                Reason = msg,
                AllowedInPeriod = options.SendsAllowedInPeriod,
                Period = Client.Options.ThrottlingPeriod,
                SentCount = sentCount
            };
            if (MessageType.Message == messageType)
            {
                Client.RaiseMessageThrottled(new OnMessageThrottledEventArgs(onThrottledEventArgs));
            }
            else if (MessageType.Whisper == messageType)
            {
                Client.RaiseWhisperThrottled(new OnWhisperThrottledEventArgs(onThrottledEventArgs));
            }
        }
        #endregion Actions for NetworkServices: Messages
    }
}