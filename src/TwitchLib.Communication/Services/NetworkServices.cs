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

        private ILogger Logger { get; }
        private CancellationToken Token => Client.Token;
        private ClientBase<T> Client { get; }

        internal ConnectionWatchDog<T> ConnectionWatchDog { get; }

        internal NetworkServices(
            ClientBase<T> client,
            ILogger logger = null)
        {
            Logger = logger;
            Client = client;
            ConnectionWatchDog = new ConnectionWatchDog<T>(Client, logger);
        }

        internal void Start()
        {
            Logger?.TraceMethodCall(GetType());
            if (_monitorTask == null || !TaskHelper.IsTaskRunning(_monitorTask))
            {
                // this task is probably still running
                // may be in case of a network connection loss
                // all other Tasks haven't been started or have been canceled!
                // ConnectionWatchDog is the only one, that has a seperate CancellationTokenSource!
                _monitorTask = ConnectionWatchDog.StartMonitorTask();
            }

            _listenTask = Task.Run(Client.ListenTaskAction, Token);
        }

        internal void Stop()
        {
            Logger?.TraceMethodCall(GetType());
            ConnectionWatchDog.Stop();
        }
    }
}