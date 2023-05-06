using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Extensions;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Services;

namespace TwitchLib.Communication.Clients
{
    /// <summary>
    ///     This <see langword="class"/> bundles almost everything that <see cref="TcpClient"/> and <see cref="WebSocketClient"/> have in common
    ///     to be able to 
    ///     <list>
    ///         <item>
    ///             pass instances of this <see langword="class"/> to <see cref="NetworkServices{T}"/> and <see cref="Services.ThrottlerService"/>
    ///         </item>
    ///         <item>
    ///             and to access Methods of this instance within <see cref="NetworkServices{T}"/> and <see cref="Services.ThrottlerService"/>
    ///         </item>
    ///     </list>
    /// </summary>
    public abstract class ClientBase<T> : IClient
        where T : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly NetworkServices<T> _networkServices;
        private CancellationTokenSource _cancellationTokenSource;
        
        /// <summary>
        ///     This <see cref="_cancellationTokenSource"/> is used for <see cref="_networkServices.ListenTask"/>
        ///     whenever a call to <see cref="_cancellationTokenSource.Cancel()"/> is made
        /// </summary>
        internal CancellationToken Token => _cancellationTokenSource.Token;
        
        internal static TimeSpan TimeOutEstablishConnection => TimeSpan.FromSeconds(15);

        protected ILogger? Logger { get; }
        
        protected abstract string Url { get; }
        
        /// <summary>
        ///     The underlying <see cref="T"/> client.
        /// </summary>
        public T? Client { get; private set; }

        public abstract bool IsConnected { get; }
        
        public IClientOptions Options { get; }

        public event AsyncEventHandler<OnConnectedEventArgs>? OnConnected;
        public event AsyncEventHandler<OnDisconnectedEventArgs>? OnDisconnected;
        public event AsyncEventHandler<OnErrorEventArgs>? OnError;
        public event AsyncEventHandler<OnFatalErrorEventArgs>? OnFatality;
        public event AsyncEventHandler<OnMessageEventArgs>? OnMessage;
        public event AsyncEventHandler<OnSendFailedEventArgs>? OnSendFailed;
        public event AsyncEventHandler<OnConnectedEventArgs>? OnReconnected;

        internal ClientBase(
            IClientOptions? options,
            ILogger? logger)
        {
            Logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();
            Options = options ?? new ClientOptions();
            _networkServices = new NetworkServices<T>(this, logger);
        }

        /// <summary>
        ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        private async Task RaiseSendFailed(OnSendFailedEventArgs eventArgs)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }

            if(OnSendFailed != null) await OnSendFailed.Invoke(this, eventArgs);
        }

        /// <summary>
        ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal async Task RaiseError(OnErrorEventArgs eventArgs)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }

            if (OnError != null) await OnError.Invoke(this, eventArgs);
        }

        /// <summary>
        ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        private async Task RaiseReconnected()
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }

            if (OnReconnected != null) await OnReconnected.Invoke(this, new OnConnectedEventArgs());
        }

        /// <summary>
        ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal async Task RaiseMessage(OnMessageEventArgs eventArgs)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }

            if (OnMessage != null) await OnMessage.Invoke(this, eventArgs);
        }

        /// <summary>
        ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal async Task RaiseFatal(Exception? ex = null)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }

            var onFatalErrorEventArgs = ex != null
                ? new OnFatalErrorEventArgs(ex)
                : new OnFatalErrorEventArgs("Fatal network error.");

            if (OnFatality != null) await OnFatality.Invoke(this, onFatalErrorEventArgs);
        }

        private async Task RaiseDisconnected()
        {
            Logger?.TraceMethodCall(GetType());
            if (OnDisconnected != null) await OnDisconnected.Invoke(this, new OnDisconnectedEventArgs());
        }

        private async Task RaiseConnected()
        {
            Logger?.TraceMethodCall(GetType());
            if (OnConnected != null) await OnConnected.Invoke(this, new OnConnectedEventArgs());
        }

        public async Task<bool> SendAsync(string message)
        {
            Logger?.TraceMethodCall(GetType());

            await _semaphore.WaitAsync(Token);
            try
            {
                await ClientSendAsync(message);
                return true;
            }
            catch (Exception e)
            {
                await RaiseSendFailed(new OnSendFailedEventArgs(e, message));
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public Task<bool> OpenAsync()
        {
            Logger?.TraceMethodCall(GetType());
            return OpenPrivateAsync(false);
        }

        public async Task CloseAsync()
        {
            Logger?.TraceMethodCall(GetType());
            
            // Network services has to be stopped first so that it wont reconnect
            await _networkServices.StopAsync();
            
            // ClosePrivate() also handles IClientOptions.DisconnectWait
            await ClosePrivateAsync();
        }

        /// <summary>
        ///     <inheritdoc cref="CloseAsync"/>
        /// </summary>
        public void Dispose()
        {
            Logger?.TraceMethodCall(GetType());
            CloseAsync().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        public async Task<bool> ReconnectAsync()
        {
            Logger?.TraceMethodCall(GetType());
            
            // Stops everything (including NetworkServices)
            if (IsConnected)
            {
                await CloseAsync();
            }

            return await ReconnectInternalAsync();
        }

        private async Task<bool> OpenPrivateAsync(bool isReconnect)
        {
            Logger?.TraceMethodCall(GetType());
            try
            {
                if (Token.IsCancellationRequested)
                {
                    return false;
                }

                if (IsConnected)
                {
                    return true;
                }

                // Always create new client when opening new connection
                Client = CreateClient();
                
                var first = true;
                Options.ReconnectionPolicy.Reset(isReconnect);
                
                while (!IsConnected &&
                       !Options.ReconnectionPolicy.AreAttemptsComplete())
                {
                    Logger?.TraceAction(GetType(), "try to connect");
                    if (!first)
                    {
                        await Task.Delay(Options.ReconnectionPolicy.GetReconnectInterval(), CancellationToken.None);
                    }

                    await ConnectClientAsync();
                    Options.ReconnectionPolicy.ProcessValues();
                    first = false;
                }

                if (!IsConnected)
                {
                    Logger?.TraceAction(GetType(), "Client couldn't establish a connection");
                    await RaiseFatal();
                    return false;
                }

                Logger?.TraceAction(GetType(), "Client established a connection");
                _networkServices.Start();
                
                if (!isReconnect)
                {
                   await RaiseConnected();
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger?.LogExceptionAsError(GetType(), ex);
                await RaiseError(new OnErrorEventArgs(ex));
                await RaiseFatal();
                return false;
            }
        }

        /// <summary>
        ///     Stops <see cref="_networkServices.ListenTaskAsync"/>
        ///     by calling <see cref="_cancellationTokenSource.Cancel()"/>
        ///     <br></br>
        ///     and enforces the <see cref="CloseClient"/>
        ///     <br></br>
        ///     afterwards it waits for the via <see cref="IClientOptions.DisconnectWait"/> given amount of milliseconds
        ///     <br></br>
        ///     <br></br>
        ///     <see cref="ConnectionWatchDog{T}"/> will keep running,
        ///     because itself issued this call by calling <see cref="ReconnectInternalAsync()"/>
        /// </summary>
        private async Task ClosePrivateAsync()
        {
            Logger?.TraceMethodCall(GetType());
            
            // This cancellation traverse up to NetworkServices.ListenTask
            _cancellationTokenSource.Cancel();
            Logger?.TraceAction(GetType(),
                $"{nameof(_cancellationTokenSource)}.{nameof(_cancellationTokenSource.Cancel)} is called");

            CloseClient();
            await RaiseDisconnected();
            _cancellationTokenSource = new CancellationTokenSource();

            await Task.Delay(TimeSpan.FromMilliseconds(Options.DisconnectWait), CancellationToken.None);
        }

        /// <summary>
        ///     Send method for the client.
        /// </summary>
        /// <param name="message">
        ///     Message to be send
        /// </param>
        protected abstract Task ClientSendAsync(string message);

        /// <summary>
        ///     Instantiate the underlying client.
        /// </summary>
        protected abstract T CreateClient();

        /// <summary>
        ///     one of the following specific methods
        ///     <list>
        ///         <item>
        ///             <see cref="System.Net.Sockets.TcpClient.Close"/>
        ///         </item>
        ///         <item>
        ///             <see cref="System.Net.WebSockets.ClientWebSocket.Abort"/>
        ///         </item>
        ///     </list>
        ///     calls to one of the methods mentioned above,
        ///     also Dispose() the respective client,
        ///     so no additional Dispose() is needed
        /// </summary>
        protected abstract void CloseClient();

        /// <summary>
        ///     Connect the client.
        /// </summary>
        protected abstract Task ConnectClientAsync();
        
        /// <summary>
        ///     To issue a reconnect
        ///     <br></br>
        ///     especially for the <see cref="ConnectionWatchDog{T}"/>
        ///     <br></br>
        ///     it stops all <see cref="NetworkServices{T}"/> but <see cref="ConnectionWatchDog{T}"/>!
        ///     <br></br>
        ///     <br></br>
        ///     see also <seealso cref="Open()"/>:
        ///     <br></br>
        ///     <inheritdoc cref="Open()"/>
        /// </summary>
        /// <returns>
        ///     <see langword="true"/> if a connection could be established, <see langword="false"/> otherwise
        /// </returns>
        internal async Task<bool> ReconnectInternalAsync()
        {
            Logger?.TraceMethodCall(GetType());
            await ClosePrivateAsync();
            var reconnected = await OpenPrivateAsync(true);
            if (reconnected)
            {
               await RaiseReconnected();
            }

            return reconnected;
        }

        /// <summary>
        ///     just the Action that listens for new Messages
        ///     the corresponding <see cref="Task"/> is held by <see cref="NetworkServices{T}"/>
        /// </summary>
        internal abstract Task ListenTaskActionAsync();
    }
}