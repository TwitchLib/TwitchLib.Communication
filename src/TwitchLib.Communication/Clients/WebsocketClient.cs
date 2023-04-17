﻿using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchLib.Communication.Enums;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Extensions;
using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Clients
{
    public class WebSocketClient : ClientBase<ClientWebSocket>
    {
        protected override string Url { get; }

        public override bool IsConnected => Client?.State == WebSocketState.Open;

        public WebSocketClient(
            IClientOptions options = null,
            ILogger logger = null)
            : base(options, logger)
        {
            switch (Options.ClientType)
            {
                case ClientType.Chat:
                    Url = Options.UseSsl ? "wss://irc-ws.chat.twitch.tv:443" : "ws://irc-ws.chat.twitch.tv:80";
                    break;
                case ClientType.PubSub:
                    Url = Options.UseSsl ? "wss://pubsub-edge.twitch.tv:443" : "ws://pubsub-edge.twitch.tv:80";
                    break;
                default:
                    var ex = new ArgumentOutOfRangeException(nameof(Options.ClientType));
                    Logger?.LogExceptionAsError(GetType(), ex);
                    throw ex;
            }
        }

        internal override async Task ListenTaskActionAsync()
        {
            Logger?.TraceMethodCall(GetType());
            if (Client == null)
            {
                var ex = new InvalidOperationException($"{nameof(Client)} was null!");
                Logger?.LogExceptionAsError(GetType(), ex);
                RaiseFatal(ex);
                throw ex;
            }

            var message = string.Empty;
            while (IsConnected)
            {
                WebSocketReceiveResult result;
                var buffer = new byte[1024];
                try
                {
                    result = await Client.ReceiveAsync(new ArraySegment<byte>(buffer), Token);
                    if (result == null)
                    {
                        continue;
                    }
                }
                catch (Exception ex) when (ex.GetType() == typeof(TaskCanceledException) ||
                                           ex.GetType() == typeof(OperationCanceledException))
                {
                    // occurs if the Tasks are canceled by the CancellationTokenSource.Token
                    Logger?.LogExceptionAsInformation(GetType(), ex);
                    break;
                }
                catch (Exception ex)
                {
                    Logger?.LogExceptionAsError(GetType(), ex);
                    RaiseError(new OnErrorEventArgs { Exception = ex });
                    break;
                }

                switch (result.MessageType)
                {
                    case WebSocketMessageType.Close:
                        await CloseAsync();
                        break;
                    case WebSocketMessageType.Text when !result.EndOfMessage:
                        message += Encoding.UTF8.GetString(buffer).TrimEnd('\0');

                        // continue while, to receive more message-parts
                        continue;

                    case WebSocketMessageType.Text:
                        message += Encoding.UTF8.GetString(buffer).TrimEnd('\0');
                        RaiseMessage(new OnMessageEventArgs() { Message = message });
                        break;
                    case WebSocketMessageType.Binary:
                        break;
                    default:
                        Exception ex = new ArgumentOutOfRangeException();
                        Logger?.LogExceptionAsError(GetType(), ex);
                        throw ex;
                }

                // clear/reset message
                message = string.Empty;
            }
        }

        protected override async Task ClientSendAsync(string message)
        {
            Logger?.TraceMethodCall(GetType());

            // this is not thread safe
            // this method should only be called from 'ClientBase.Send()'
            // where its call gets synchronized/locked
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream?view=netstandard-2.0#remarks

            // https://stackoverflow.com/a/59619916
            // links from within this thread:
            // the 4th point: https://www.codetinkerer.com/2018/06/05/aspnet-core-websockets.html
            // https://github.com/dotnet/corefx/blob/d6b11250b5113664dd3701c25bdf9addfacae9cc/src/Common/src/System/Net/WebSockets/ManagedWebSocket.cs#L22-L28
            if (Client == null)
            {
                var ex = new InvalidOperationException($"{nameof(Client)} was null!");
                Logger?.LogExceptionAsError(GetType(), ex);
                RaiseFatal(ex);
                throw ex;
            }

            var bytes = Encoding.UTF8.GetBytes(message);
            await Client.SendAsync(new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                Token);
        }

        protected override async Task ConnectClientAsync()
        {
            Logger?.TraceMethodCall(GetType());
            if (Client == null)
            {
                var ex = new InvalidOperationException($"{nameof(Client)} was null!");
                Logger?.LogExceptionAsError(GetType(), ex);
                RaiseFatal(ex);
                throw ex;
            }

            try
            {
                // https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios
#if NET6_0_OR_GREATER
            // within the following thread:
            // https://stackoverflow.com/questions/4238345/asynchronously-wait-for-taskt-to-complete-with-timeout
            // the following answer
            // NET6_0_OR_GREATER: https://stackoverflow.com/a/68998339
            var connectTask = Client.ConnectAsync(new Uri(Url), Token);
            var waitTask = connectTask.WaitAsync(TimeOutEstablishConnection, Token);
            await Task.WhenAny(connectTask, waitTask);
#else
                // within the following thread:
                // https://stackoverflow.com/questions/4238345/asynchronously-wait-for-taskt-to-complete-with-timeout
                // the following two answers:
                // https://stackoverflow.com/a/11191070
                // https://stackoverflow.com/a/22078975
                
                using (var delayTaskCancellationTokenSource = new CancellationTokenSource())
                {
                    var connectTask = Client.ConnectAsync(new Uri(Url), Token);
                    var delayTask = Task.Delay(
                        (int)TimeOutEstablishConnection.TotalMilliseconds,
                        delayTaskCancellationTokenSource.Token);

                    await Task.WhenAny(connectTask, delayTask);
                    delayTaskCancellationTokenSource.Cancel();
                }
#endif
                if (!IsConnected)
                {
                    Logger?.TraceAction(GetType(), "Client couldn't establish connection");
                }
            }
            catch (Exception ex) when (ex.GetType() == typeof(TaskCanceledException) ||
                                       ex.GetType() == typeof(OperationCanceledException))
            {
                // occurs if the Tasks are canceled by the CancellationTokenSource.Token
                Logger?.LogExceptionAsInformation(GetType(), ex);
            }
            catch (Exception ex)
            {
                Logger?.LogExceptionAsError(GetType(), ex);
            }
        }

        protected override ClientWebSocket CreateClient()
        {
            Logger?.TraceMethodCall(GetType());
            return new ClientWebSocket();
        }

        protected override void CloseClient()
        {
            Logger?.TraceMethodCall(GetType());
            Client?.Abort();
            Client?.Dispose();
        }
    }
}