using System;
using System.IO;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using TwitchLib.Communication.Events;
using TwitchLib.Communication.Extensions;
using TwitchLib.Communication.Interfaces;

using ErrorEventArgs = TwitchLib.Communication.Events.ErrorEventArgs;

namespace TwitchLib.Communication.Clients
{

    public class TcpClient : ClientBase<System.Net.Sockets.TcpClient>
    {
        #region properties protected
        protected override string Url => "irc.chat.twitch.tv";
        #endregion properties protected


        #region properties private
        private int Port => Options.UseSsl ? 6697 : 6667;
        private StreamReader Reader { get; set; }
        private StreamWriter Writer { get; set; }
        #endregion properties private


        #region properties public
        public override bool IsConnected => Client?.Connected ?? false;
        #endregion properties public


        #region ctors

        public TcpClient(IClientOptions options = null, ILogger logger = null) : base(options, logger) { }
        
        #endregion ctors

        #region methods internal
        internal override void ListenTaskAction()
        {
            Logger?.TraceMethodCall(GetType());
            if (Reader == null)
            {
                Exception ex = new InvalidOperationException($"{nameof(Reader)} was null!");
                Logger?.LogExceptionAsError(GetType(), ex);
                RaiseFatal(ex);
                throw ex;
            }
            while (IsConnected)
            {
                try
                {
                    string input = Reader.ReadLine();
                    if (input is null)
                    {
                        continue;
                    }

                    RaiseMessage(new MessageEventArgs(input));
                }
                catch (Exception ex) when (ex.GetType() == typeof(TaskCanceledException) || ex.GetType() == typeof(OperationCanceledException))
                {
                    // occurs if the Tasks are canceled by the CancellationTokenSource.Token
                    Logger?.LogExceptionAsInformation(GetType(), ex);
                }
                catch (Exception ex)
                {
                    Logger?.LogExceptionAsError(GetType(), ex);
                    RaiseError(new ErrorEventArgs(ex));
                    break;
                }
            }
        }
        #endregion methods internal


        #region methods protected
        protected override void SpecificClientSend(string message)
        {
            Logger?.TraceMethodCall(GetType());

            // this is not thread safe
            // this method should only be called from 'AClientBase.Send()'
            // where its call gets synchronized/locked
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream?view=netstandard-2.0#remarks
            if (Writer == null)
            {
                Exception ex = new InvalidOperationException($"{nameof(Writer)} was null!");
                Logger?.LogExceptionAsError(GetType(), ex);
                RaiseFatal(ex);
                throw ex;
            }
            Writer.WriteLine(message);
            Writer.Flush();
        }
        protected override void SpecificClientConnect()
        {
            Logger?.TraceMethodCall(GetType());
            if (Client == null)
            {
                Exception ex = new InvalidOperationException($"{nameof(Client)} was null!");
                Logger?.LogExceptionAsError(GetType(), ex);
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

            Task connectTask = Client.ConnectAsync(URL,
                                                   Port);
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

                using (CancellationTokenSource delayTaskCancellationTokenSource = new CancellationTokenSource())
                {
                    Task connectTask = Client.ConnectAsync(Url, Port);
                    Task delayTask = Task.Delay((int) TimeOutEstablishConnection.TotalMilliseconds,
                                                delayTaskCancellationTokenSource.Token);
                    Task<Task> task = Task.WhenAny(connectTask, delayTask);
                    // though 'theTaskThatCompletedFirst' is unused, just to be precise...
                    var theTaskThatCompletedFirst = task.GetAwaiter().GetResult();
                    delayTaskCancellationTokenSource.Cancel();
                }
#endif
                if (!Client.Connected)
                {
                    Logger?.TraceAction(GetType(), "Client couldn't establish connection");
                    return;
                }
                Logger?.TraceAction(GetType(), "Client established connection successfully");
                if (Options.UseSsl)
                {
                    SslStream ssl = new SslStream(Client.GetStream(), false);
                    ssl.AuthenticateAsClient(Url);
                    Reader = new StreamReader(ssl);
                    Writer = new StreamWriter(ssl);
                }
                else
                {
                    Reader = new StreamReader(Client.GetStream());
                    Writer = new StreamWriter(Client.GetStream());
                }
            }
            catch (Exception ex) when (ex.GetType() == typeof(TaskCanceledException) || ex.GetType() == typeof(OperationCanceledException))
            {
                // occurs if the Tasks are canceled by the CancellationTokenSource.Token
                Logger?.LogExceptionAsInformation(GetType(), ex);
            }
            catch (Exception ex)
            {
                Logger?.LogExceptionAsError(GetType(), ex);
            }
        }
        protected override System.Net.Sockets.TcpClient NewClient()
        {
            Logger?.TraceMethodCall(GetType());
            System.Net.Sockets.TcpClient tcpClient = new System.Net.Sockets.TcpClient
            {
                // https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient.lingerstate?view=netstandard-2.0#remarks
                LingerState = new System.Net.Sockets.LingerOption(true, 0)
            };
            return tcpClient;
        }
        protected override void SpecificClientClose()
        {
            Logger?.TraceMethodCall(GetType());
            Reader?.Close();
            Reader?.Dispose();
            Writer?.Close();
            Writer?.Dispose();
            Client?.Close();
            Client?.Dispose();
        }
        #endregion methods protected
    }
}
