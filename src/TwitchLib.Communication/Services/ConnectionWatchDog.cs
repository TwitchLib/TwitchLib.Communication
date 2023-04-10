using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Extensions;

namespace TwitchLib.Communication.Services
{

    /// <summary>
    ///     just to check connection state
    /// </summary>
    internal class ConnectionWatchDog<T> where T : IDisposable
    {

        #region properties private
        private ILogger Logger { get; }
        private ClientBase<T> Client { get; }
        /// <summary>
        ///     <list>
        ///         <item>
        ///             should only be set to a new instance in <see cref="StartMonitorTask()"/>
        ///         </item>
        ///         <item>
        ///             should only be set to <see langword="null"/> in <see cref="Stop()"/>
        ///         </item>
        ///     </list>
        /// </summary>
        private CancellationTokenSource CancellationTokenSource { get; set; }
        private CancellationToken Token => CancellationTokenSource.Token;
        private static int MonitorTaskDelayInMilliseconds => 200;
        #endregion properties private


        #region ctors
        internal ConnectionWatchDog(ClientBase<T> client,
                                    ILogger logger = null)
        {
            Logger = logger;
            Client = client;
        }
        #endregion ctors


        #region methods internal

        internal Task StartMonitorTask()
        {
            Logger?.TraceMethodCall(GetType());
            // we dont want to start more than one WatchDog
            if (CancellationTokenSource != null)
            {
                Exception ex = new InvalidOperationException("Monitor Task cant be started more than once!");
                Logger?.LogExceptionAsError(GetType(), ex);
                throw ex;
            }
            // this should be the only place where a new instance of CancellationTokenSource is set
            CancellationTokenSource = new CancellationTokenSource();

            return Task.Run(MonitorTaskAction, Token);
        }

        internal void Stop()
        {
            Logger?.TraceMethodCall(GetType());
            CancellationTokenSource?.Cancel();
            // give MonitorTaskAction a chance to catch cancellation
            // otherwise it may result in an Exception
            Task.Delay(MonitorTaskDelayInMilliseconds * 2, Token).GetAwaiter().GetResult();
            CancellationTokenSource?.Dispose();
            // set it to null for the check within this.StartMonitorTask()
            CancellationTokenSource = null;
        }
        #endregion methods internal


        #region methods private
        private void MonitorTaskAction()
        {
            Logger?.TraceMethodCall(GetType());
            try
            {
                while (!Token.IsCancellationRequested)
                {
                    // we expect the client is connected,
                    // when this monitor task starts
                    // cause BaseClient.Open() starts NetworkServices after a connection could be established
                    if (!Client.IsConnected)
                    {
                        Logger?.TraceAction(GetType(), "Client isn't connected anymore");
                        // no call to close needed,
                        // ReconnectInternal() calls the correct Close-Method within the Client
                        // ReconnectInternal() makes attempts to reconnect according to the ReconnectionPolicy within the IClientOptions
                        Logger?.TraceAction(GetType(), "Try to reconnect");
                        bool connected = Client.ReconnectInternal();
                        if (!connected)
                        {
                            Logger?.TraceAction(GetType(), "Client couldn't reconnect");
                            // if the ReconnectionPolicy is set up to be finite
                            // and no connection could be established
                            // a call to Client.Close() is made
                            // that public Close() also shuts down this ConnectionWatchDog
                            Client.Close();
                            break;
                        }
                        Logger?.TraceAction(GetType(), "Client reconnected");
                    }
                    Task.Delay(MonitorTaskDelayInMilliseconds, Token).GetAwaiter().GetResult();
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
                Client.RaiseError(new ErrorEventArgs(ex));
                Client.RaiseFatal();

                // to ensure CancellationTokenSource is set to null again
                // call Stop();
                Stop();
            }
        }
        #endregion methods private
    }
}
