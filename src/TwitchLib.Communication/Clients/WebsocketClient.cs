using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using TwitchLib.Communication.Enums;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Extensions;
using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Clients
{

    public class WebSocketClient : AClientBase<ClientWebSocket>
    {
        //private readonly object sync = new object();
        #region properties protected
        protected override string URL { get; }
        #endregion properties protected


        #region properties public
        public override bool IsConnected => Client?.State == WebSocketState.Open;
        #endregion properties public


        #region ctors
        public WebSocketClient(IClientOptions options = null,
                               ILogger logger = null) : base(options, logger)
        {
            switch (Options.ClientType)
            {
                case ClientType.Chat:
                    URL = Options.UseSsl ? "wss://irc-ws.chat.twitch.tv:443" : "ws://irc-ws.chat.twitch.tv:80";
                    break;
                case ClientType.PubSub:
                    URL = Options.UseSsl ? "wss://pubsub-edge.twitch.tv:443" : "ws://pubsub-edge.twitch.tv:80";
                    break;
                default:
                    Exception ex = new ArgumentOutOfRangeException(nameof(Options.ClientType));
                    LOGGER?.LogExceptionAsError(GetType(), ex);
                    throw ex;
            }

        }
        #endregion ctors


        #region methods internal
        internal override void ListenTaskAction()
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Client == null)
            {
                Exception ex = new InvalidOperationException($"{nameof(Client)} was null!");
                LOGGER?.LogExceptionAsError(GetType(), ex);
                RaiseFatal(ex);
                throw ex;
            }
            string message = "";
            while (IsConnected)
            {
                WebSocketReceiveResult result;
                byte[] buffer = new byte[1024];
                try
                {
                    result = Client.ReceiveAsync(new ArraySegment<byte>(buffer), Token).GetAwaiter().GetResult();
                    if (result == null)
                    {
                        continue;
                    }
                }
                catch (Exception ex) when (ex.GetType() == typeof(TaskCanceledException) || ex.GetType() == typeof(OperationCanceledException))
                {
                    // occurs if the Tasks are canceled by the CancelationTokenSource.Token
                    LOGGER?.LogExceptionAsInformation(GetType(), ex);
                    break;
                }
                catch (Exception ex)
                {
                    LOGGER?.LogExceptionAsError(GetType(), ex);
                    RaiseError(new OnErrorEventArgs { Exception = ex });
                    break;
                }

                switch (result.MessageType)
                {
                    case WebSocketMessageType.Close:
                        Close();
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
                        LOGGER?.LogExceptionAsError(GetType(), ex);
                        throw ex;
                }
                // clear/reset message
                message = "";
            }
        }
        #endregion methods internal


        #region methods protected
        protected override void SpecificClientSend(string message)
        {
            LOGGER?.TraceMethodCall(GetType());

            // this is not thread safe
            // this method should only be called from 'AClientBase.Send()'
            // where its call gets synchronized/locked
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream?view=netstandard-2.0#remarks

            // https://stackoverflow.com/a/59619916
            // links from within this thread:
            // the 4th point: https://www.codetinkerer.com/2018/06/05/aspnet-core-websockets.html
            // https://github.com/dotnet/corefx/blob/d6b11250b5113664dd3701c25bdf9addfacae9cc/src/Common/src/System/Net/WebSockets/ManagedWebSocket.cs#L22-L28
            //lock (this.sync) {
            if (Client == null)
            {
                Exception ex = new InvalidOperationException($"{nameof(Client)} was null!");
                LOGGER?.LogExceptionAsError(GetType(), ex);
                RaiseFatal(ex);
                throw ex;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            Task sendTask = Client.SendAsync(new ArraySegment<byte>(bytes),
                                             WebSocketMessageType.Text,
                                             true,
                                             Token);
            sendTask.GetAwaiter().GetResult();
            //}
        }
        protected override void SpecificClientConnect()
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Client == null)
            {
                Exception ex = new InvalidOperationException($"{nameof(Client)} was null!");
                LOGGER?.LogExceptionAsError(GetType(), ex);
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
            Task connectTask = Client.ConnectAsync(new Uri(URL),
                                                   Token);
            Task waitTask = connectTask.WaitAsync(TimeOutEstablishConnection,
                                                  Token);
            // GetAwaiter().GetResult() to avoid async in method-signature 'protected override void SpecificClientConnect()';
            waitTask.GetAwaiter().GetResult();
#else
                // within the following thread:
                // https://stackoverflow.com/questions/4238345/asynchronously-wait-for-taskt-to-complete-with-timeout
                // the following two answers:
                // https://stackoverflow.com/a/11191070
                // https://stackoverflow.com/a/22078975

                // avoid deletion of using decleration through code-cleanups/save-actions
                // by using the fully qualified name
                using (System.Threading.CancellationTokenSource delayTaskCancellationTokenSource = new System.Threading.CancellationTokenSource())
                {
                    Task connectTask = Client.ConnectAsync(new Uri(URL),
                                                           Token);
                    Task delayTask = Task.Delay((int) TimeOutEstablishConnection.TotalMilliseconds,
                                                delayTaskCancellationTokenSource.Token);
                    Task<Task> task = Task.WhenAny(connectTask,
                                                   delayTask);
                    // GetAwaiter().GetResult() to avoid async in method-signature 'protected override void SpecificClientConnect()';
                    Task theTaskThatCompletedFirst = task.GetAwaiter().GetResult();
                    //
                    delayTaskCancellationTokenSource?.Cancel();
                }
#endif
                if (!IsConnected)
                {
                    LOGGER?.TraceAction(GetType(), "Client couldnt establish connection");
                }
                // take care: the following closing brace belongs to 'try {'
            }
            catch (Exception ex) when (ex.GetType() == typeof(TaskCanceledException) || ex.GetType() == typeof(OperationCanceledException))
            {
                // occurs if the Tasks are canceled by the CancelationTokenSource.Token
                LOGGER?.LogExceptionAsInformation(GetType(), ex);
            }
            catch (Exception ex)
            {
                LOGGER?.LogExceptionAsError(GetType(), ex);
            }
        }
        protected override ClientWebSocket NewClient()
        {
            LOGGER?.TraceMethodCall(GetType());
            ClientWebSocket clientWebSocket = new ClientWebSocket();
            return clientWebSocket;
        }
        protected override void SpecificClientClose()
        {
            LOGGER?.TraceMethodCall(GetType());
            Client?.Abort();
            Client?.Dispose();
        }
        #endregion methods protected
    }
}
