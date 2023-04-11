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
    ///     Service that checks connection state.
    /// </summary>
    internal class ConnectionWatchDog<T> where T : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ClientBase<T> _client;

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
        private CancellationTokenSource _cancellationTokenSource;

        private const int MonitorTaskDelayInMilliseconds = 200;

        internal ConnectionWatchDog(
            ClientBase<T> client,
            ILogger logger = null)
        {
            _logger = logger;
            _client = client;
        }

        internal Task StartMonitorTask()
        {
            _logger?.TraceMethodCall(GetType());
            // We dont want to start more than one WatchDog
            if (_cancellationTokenSource != null)
            {
                Exception ex = new InvalidOperationException("Monitor Task cant be started more than once!");
                _logger?.LogExceptionAsError(GetType(), ex);
                throw ex;
            }

            // This should be the only place where a new instance of CancellationTokenSource is set
            _cancellationTokenSource = new CancellationTokenSource();

            return Task.Run(MonitorTaskAction, _cancellationTokenSource.Token);
        }

        internal void Stop()
        {
            _logger?.TraceMethodCall(GetType());
            _cancellationTokenSource?.Cancel();
            // give MonitorTaskAction a chance to catch cancellation
            // otherwise it may result in an Exception
            Task.Delay(MonitorTaskDelayInMilliseconds * 2).GetAwaiter().GetResult();
            _cancellationTokenSource?.Dispose();
            // set it to null for the check within this.StartMonitorTask()
            _cancellationTokenSource = null;
        }

        private void MonitorTaskAction()
        {
            _logger?.TraceMethodCall(GetType());
            try
            {
                while (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // we expect the client is connected,
                    // when this monitor task starts
                    // cause BaseClient.Open() starts NetworkServices after a connection could be established
                    if (!_client.IsConnected)
                    {
                        _logger?.TraceAction(GetType(), "Client isn't connected anymore");
                        // no call to close needed,
                        // ReconnectInternal() calls the correct Close-Method within the Client
                        // ReconnectInternal() makes attempts to reconnect according to the ReconnectionPolicy within the IClientOptions
                        _logger?.TraceAction(GetType(), "Try to reconnect");
                        bool connected = _client.ReconnectInternal();
                        if (!connected)
                        {
                            _logger?.TraceAction(GetType(), "Client couldn't reconnect");
                            // if the ReconnectionPolicy is set up to be finite
                            // and no connection could be established
                            // a call to Client.Close() is made
                            // that public Close() also shuts down this ConnectionWatchDog
                            _client.Close();
                            break;
                        }

                        _logger?.TraceAction(GetType(), "Client reconnected");
                    }

                    Task.Delay(MonitorTaskDelayInMilliseconds).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex) when (ex.GetType() == typeof(TaskCanceledException) ||
                                       ex.GetType() == typeof(OperationCanceledException))
            {
                // Occurs if the Tasks are canceled by the CancellationTokenSource.Token
                _logger?.LogExceptionAsInformation(GetType(), ex);
            }
            catch (Exception ex)
            {
                _logger?.LogExceptionAsError(GetType(), ex);
                _client.RaiseError(new OnErrorEventArgs { Exception = ex });
                _client.RaiseFatal();

                // To ensure CancellationTokenSource is set to null again call Stop();
                Stop();
            }
        }
    }
}