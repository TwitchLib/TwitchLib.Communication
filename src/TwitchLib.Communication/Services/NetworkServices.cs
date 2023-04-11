using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Extensions;
using TwitchLib.Communication.Helpers;

namespace TwitchLib.Communication.Services
{
    /// <summary>
    ///     <see langword="class"/> to bundle Network-Service-<see cref="Task"/>s
    /// </summary>
    internal class NetworkServices<T> where T : IDisposable
    {
        // Each Task is held in its own variable to be more precise
        private Task _listenTask;
        private Task _monitorTask;
        private readonly ClientBase<T> _client;
        private readonly ILogger _logger;
        private readonly ConnectionWatchDog<T> _connectionWatchDog;

        private CancellationToken Token => _client.Token;

        internal NetworkServices(
            ClientBase<T> client,
            ILogger logger = null)
        {
            _logger = logger;
            _client = client;
            _connectionWatchDog = new ConnectionWatchDog<T>(_client, logger);
        }

        internal void Start()
        {
            _logger?.TraceMethodCall(GetType());
            if (_monitorTask == null || !TaskHelper.IsTaskRunning(_monitorTask))
            {
                // this task is probably still running
                // may be in case of a network connection loss
                // all other Tasks haven't been started or have been canceled!
                // ConnectionWatchDog is the only one, that has a seperate CancellationTokenSource!
                _monitorTask = _connectionWatchDog.StartMonitorTask();
            }

            _listenTask = Task.Run(_client.ListenTaskAction, Token);
        }

        internal void Stop()
        {
            _logger?.TraceMethodCall(GetType());
            _connectionWatchDog.Stop();
        }
    }
}