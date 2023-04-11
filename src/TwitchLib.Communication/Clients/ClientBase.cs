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
    ///             pass instances of this <see langword="class"/> to <see cref="Services.NetworkServices"/> and <see cref="Services.ThrottlerService"/>
    ///         </item>
    ///         <item>
    ///             and to access Methods of this instance within <see cref="Services.NetworkServices"/> and <see cref="Services.ThrottlerService"/>
    ///         </item>
    ///     </list>
    /// </summary>
    public abstract class ClientBase<T> : IClient where T : IDisposable
    {
        private static readonly object Lock = new object();
        private readonly NetworkServices<T> _networkServices;
        private CancellationTokenSource _cancellationTokenSource;
        
        /// <summary>
        ///     This <see cref="_cancellationTokenSource"/> is used for <see cref="_networkServices.ListenTask"/>
        ///     whenever a call to <see cref="_cancellationTokenSource.Cancel()"/> is made
        /// </summary>
        internal CancellationToken Token => _cancellationTokenSource.Token;
        
        internal static TimeSpan TimeOutEstablishConnection => TimeSpan.FromSeconds(15);

        protected ILogger Logger { get; }
        
        protected abstract string Url { get; }
        
        /// <summary>
        ///     The underlying <see cref="T"/> client.
        /// </summary>
        public T Client { get; private set; }

        public abstract bool IsConnected { get; }
        
        public IClientOptions Options { get; }

        public event EventHandler<OnConnectedEventArgs> OnConnected;
        public event EventHandler<OnDisconnectedEventArgs> OnDisconnected;
        public event EventHandler<OnErrorEventArgs> OnError;
        public event EventHandler<OnFatalErrorEventArgs> OnFatality;
        public event EventHandler<OnMessageEventArgs> OnMessage;
        public event EventHandler<OnSendFailedEventArgs> OnSendFailed;
        public event EventHandler<OnConnectedEventArgs> OnReconnected;

        internal ClientBase(
            IClientOptions options = null,
            ILogger logger = null)
        {
            Logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();
            Options = options ?? new ClientOptions();
            _networkServices = new NetworkServices<T>(this, logger);
        }

        /// <summary>
        ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        private void RaiseSendFailed(OnSendFailedEventArgs eventArgs)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }

            OnSendFailed?.Invoke(this, eventArgs);
        }

        /// <summary>
        ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseError(OnErrorEventArgs eventArgs)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }

            OnError?.Invoke(this, eventArgs);
        }

        /// <summary>
        ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        private void RaiseReconnected()
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }

            OnReconnected?.Invoke(this, new OnConnectedEventArgs());
        }

        /// <summary>
        ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseMessage(OnMessageEventArgs eventArgs)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }

            OnMessage?.Invoke(this, eventArgs);
        }

        /// <summary>
        ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseFatal(Exception e = null)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }

            var onFatalErrorEventArgs = new OnFatalErrorEventArgs("Fatal network error.");
            if (e != null)
            {
                onFatalErrorEventArgs = new OnFatalErrorEventArgs(e);
            }

            OnFatality?.Invoke(this, onFatalErrorEventArgs);
        }

        private void RaiseDisconnected()
        {
            Logger?.TraceMethodCall(GetType());
            OnDisconnected?.Invoke(this, new OnDisconnectedEventArgs());
        }

        private void RaiseConnected()
        {
            Logger?.TraceMethodCall(GetType());
            OnConnected?.Invoke(this, new OnConnectedEventArgs());
        }

        public bool Send(string message)
        {
            Logger?.TraceMethodCall(GetType());
            try
            {
                lock (Lock)
                {
                    ClientSend(message);
                    return true;
                }
            }
            catch (Exception e)
            {
                RaiseSendFailed(new OnSendFailedEventArgs() { Exception = e, Data = message });
                return false;
            }
        }

        public bool Open()
        {
            Logger?.TraceMethodCall(GetType());
            return OpenPrivate(false);
        }

        public void Close()
        {
            Logger?.TraceMethodCall(GetType());
            
            // Network services has to be stopped first so that it wont reconnect
            _networkServices.Stop();
            
            // ClosePrivate() also handles IClientOptions.DisconnectWait
            ClosePrivate();
        }

        /// <summary>
        ///     <inheritdoc cref="Close"/>
        /// </summary>
        public void Dispose()
        {
            Logger?.TraceMethodCall(GetType());
            Close();
            GC.SuppressFinalize(this);
        }

        public bool Reconnect()
        {
            Logger?.TraceMethodCall(GetType());
            
            // Stops everything (including NetworkServices)
            if (IsConnected)
            {
                Close();
            }

            // interface IClient doesnt declare a return value for Reconnect()
            // so we can suppress IDE0058 of ReconnectInternal()
            return ReconnectInternal();
        }

        private bool OpenPrivate(bool isReconnect)
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
                while (!IsConnected
                       && !Options.ReconnectionPolicy.AreAttemptsComplete())
                {
                    Logger?.TraceAction(GetType(), "try to connect");
                    if (!first)
                    {
                        Task.Delay(Options.ReconnectionPolicy.GetReconnectInterval()).GetAwaiter().GetResult();
                    }

                    ConnectClient();
                    Options.ReconnectionPolicy.ProcessValues();
                    first = false;
                }

                if (!IsConnected)
                {
                    Logger?.TraceAction(GetType(), "Client couldnt establish a connection");
                    RaiseFatal();
                    return false;
                }

                Logger?.TraceAction(GetType(), "Client established a connection");
                _networkServices.Start();
                if (!isReconnect)
                {
                    RaiseConnected();
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger?.LogExceptionAsError(GetType(), ex);
                RaiseError(new OnErrorEventArgs() { Exception = ex });
                RaiseFatal();
                return false;
            }
        }

        /// <summary>
        ///     Stops <see cref="_networkServices.ListenTask"/>
        ///     by calling <see cref="_cancellationTokenSource.Cancel()"/>
        ///     <br></br>
        ///     and enforces the <see cref="CloseClient"/>
        ///     <br></br>
        ///     afterwards it waits for the via <see cref="IClientOptions.DisconnectWait"/> given amount of milliseconds
        ///     <br></br>
        ///     <br></br>
        ///     <see cref="ConnectionWatchDog"/> will keep running,
        ///     because itself issued this call by calling <see cref="ReconnectInternal()"/>
        /// </summary>
        private void ClosePrivate()
        {
            Logger?.TraceMethodCall(GetType());
            
            // This cancellation traverse up to NetworkServices.ListenTask
            _cancellationTokenSource.Cancel();
            Logger?.TraceAction(GetType(),
                $"{nameof(_cancellationTokenSource)}.{nameof(_cancellationTokenSource.Cancel)} is called");

            CloseClient();
            RaiseDisconnected();
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Delay(TimeSpan.FromMilliseconds(Options.DisconnectWait)).GetAwaiter().GetResult();
        }

        /// <summary>
        ///     specific client send method
        /// </summary>
        /// <param name="message">
        ///     IRC-Messsage
        /// </param>
        /// <returns>
        ///     <see langword="true"/>, if the message should be sent
        ///     <br></br>
        ///     <see langword="false"/> otherwise
        /// </returns>
        protected abstract void ClientSend(string message);

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
        ///     Connect client.
        /// </summary>
        protected abstract void ConnectClient();
        
        /// <summary>
        ///     to issue a reconnect
        ///     <br></br>
        ///     <br></br>
        ///     especially for the <see cref="ConnectionWatchDog"/>
        ///     <br></br>
        ///     <br></br>
        ///     it stops all <see cref="TwitchLib.Communication.Services.NetworkServices"/> but <see cref="ConnectionWatchDog"/>!
        ///     <br></br>
        ///     <br></br>
        ///     <br></br>
        ///     see also <seealso cref="Open()"/>:
        ///     <br></br>
        ///     <inheritdoc cref="Open()"/>
        /// </summary>
        /// <returns>
        ///     <see langword="true"/> if a connection could be established, <see langword="false"/> otherwise
        /// </returns>
        internal bool ReconnectInternal()
        {
            Logger?.TraceMethodCall(GetType());
            ClosePrivate();
            bool reconnected = OpenPrivate(true);
            if (reconnected)
            {
                RaiseReconnected();
            }

            return reconnected;
        }

        /// <summary>
        ///     just the Action that listens for new Messages
        ///     the corresponding <see cref="Task"/> is held by <see cref="Services.NetworkServices"/>
        /// </summary>
        internal abstract void ListenTaskAction();
    }
}