using Microsoft.Extensions.Logging;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Extensions;

namespace TwitchLib.Communication.Services;

/// <summary>
///     Service that checks connection state.
/// </summary>
internal class ConnectionWatchDog<T> where T : IDisposable
{
    private readonly ILogger? _logger;
    private readonly ClientBase<T> _client;

    /// <summary>
    ///     <list>
    ///         <item>
    ///             should only be set to a new instance in <see cref="StartMonitorTaskAsync()"/>
    ///         </item>
    ///         <item>
    ///             should only be set to <see langword="null"/> in <see cref="StopAsync()"/>
    ///         </item>
    ///     </list>
    /// </summary>
    private CancellationTokenSource? _cancellationTokenSource;

    private const int MonitorTaskDelayInMilliseconds = 200;

    public bool IsRunning { get; private set; }

    internal ConnectionWatchDog(
        ClientBase<T> client,
        ILogger? logger = null)
    {
        _logger = logger;
        _client = client;
    }

    internal Task StartMonitorTaskAsync()
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

        IsRunning = true;
        return Task.Run(MonitorTaskActionAsync, _cancellationTokenSource.Token);
    }

    internal async Task StopAsync()
    {
        IsRunning = false;
        _logger?.TraceMethodCall(GetType());
        _cancellationTokenSource?.Cancel();
        // give MonitorTaskAction a chance to catch cancellation
        // otherwise it may result in an Exception
        await Task.Delay(MonitorTaskDelayInMilliseconds * 2);
        _cancellationTokenSource?.Dispose();
        // set it to null for the check within this.StartMonitorTask()
        _cancellationTokenSource = null;
    }

    private async Task MonitorTaskActionAsync()
    {
        _logger?.TraceMethodCall(GetType());
        try
        {
            while (_cancellationTokenSource != null &&
                   !_cancellationTokenSource.Token.IsCancellationRequested)
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

                    var connected = await _client.ReconnectInternalAsync();
                    if (!connected)
                    {
                        _logger?.TraceAction(GetType(), "Client couldn't reconnect");
                        // if the ReconnectionPolicy is set up to be finite
                        // and no connection could be established
                        // a call to Client.Close() is made
                        // that public Close() also shuts down this ConnectionWatchDog
                        await _client.CloseAsync();
                        break;
                    }

                    _logger?.TraceAction(GetType(), "Client reconnected");
                }

                await Task.Delay(MonitorTaskDelayInMilliseconds);
            }
        }
        catch (TaskCanceledException)
        {
            // Swallow any cancellation exceptions
        }
        catch (OperationCanceledException ex)
        {
            // Occurs if the Tasks are canceled by the CancellationTokenSource.Token
            _logger?.LogExceptionAsInformation(GetType(), ex);
        }
        catch (Exception ex)
        {
            _logger?.LogExceptionAsError(GetType(), ex);
            await _client.RaiseError(new OnErrorEventArgs(ex));
            await _client.RaiseFatal();

            // To ensure CancellationTokenSource is set to null again call Stop();
            await StopAsync();
        }
    }
}
