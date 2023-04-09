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
    ///     this <see langword="class"/> bundles almost everything that <see cref="TcpClient"/> and <see cref="WebSocketClient"/> have in common
    ///     its not generic/it has no <see cref="Type"/>-Parameter,
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
    public abstract class AClientBase<T> : IClient where T : IDisposable
    {
        private static readonly object LOCK = new object();

        #region properties protected
        protected ILogger LOGGER { get; }
        protected abstract string URL { get; }
        #endregion properties protected


        #region properties internal
        /// <summary>
        ///     <inheritdoc cref="CancellationTokenSource"/>
        /// </summary>
        internal CancellationToken Token => CancellationTokenSource.Token;

        internal static TimeSpan TimeOutEstablishConnection => TimeSpan.FromSeconds(15);
        #endregion properties internal


        #region events public
        public event EventHandler<OnConnectedEventArgs> OnConnected;
        public event EventHandler<OnDisconnectedEventArgs> OnDisconnected;
        public event EventHandler<OnErrorEventArgs> OnError;
        public event EventHandler<OnFatalErrorEventArgs> OnFatality;
        public event EventHandler<OnMessageEventArgs> OnMessage;
        public event EventHandler<OnSendFailedEventArgs> OnSendFailed;
        public event EventHandler<OnReconnectedEventArgs> OnReconnected;
        #endregion events public


        #region properties public
        /// <summary>
        ///     the underlying
        ///     <list>
        ///         <item>
        ///             <see cref="System.Net.Sockets.TcpClient"/>
        ///         </item>
        ///         <item>or</item>
        ///         <item>
        ///             <see cref="System.Net.WebSockets.ClientWebSocket"/>
        ///         </item>
        ///     </list>
        /// </summary>
        public T Client { get; private set; }
        public abstract bool IsConnected { get; }
        public IClientOptions Options { get; }

        #endregion properties public


        #region properties private
        /// <summary>
        ///     this <see cref="CancellationTokenSource"/> is used for <see cref="NetworkServices.ListenTask"/>
        ///     whenever a call to <see cref="CancellationTokenSource.Cancel()"/> is made
        /// </summary>
        private CancellationTokenSource CancellationTokenSource { get; set; }
        private NetworkServices<T> NetworkServices { get; }
        #endregion properties private


        #region ctor(s)
        internal AClientBase(IClientOptions options = null,
                             ILogger logger = null)
        {
            LOGGER = logger;
            // INFO: Feedback by Bukk94: not to restrict the Client to those two known types
            //if (typeof(T) != typeof(System.Net.WebSockets.ClientWebSocket)
            //    && typeof(T) != typeof(System.Net.Sockets.TcpClient))
            //{
            //    throw new ArgumentOutOfRangeException(nameof(T),
            //                                          typeof(T),
            //                                          "Type-Parameter hast to be 'System.Net.Sockets.TcpClient' or 'System.Net.WebSockets.ClientWebSocket'");
            //}
            CancellationTokenSource = new CancellationTokenSource();
            Options = options ?? new ClientOptions();
            NetworkServices = new NetworkServices<T>(this,
                                                     logger);
        }
        #endregion ctor(s)


        #region invoker/raiser internal
        /// <summary>
        ///     wont rais the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseSendFailed(OnSendFailedEventArgs eventArgs)
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnSendFailed?.Invoke(this, eventArgs);
        }

        /// <summary>
        ///     wont rais the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseError(OnErrorEventArgs eventArgs)
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnError?.Invoke(this, eventArgs);
        }
        /// <summary>
        ///     wont rais the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseReconnected()
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnReconnected?.Invoke(this, new OnReconnectedEventArgs());
        }
        /// <summary>
        ///     wont rais the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseMessage(OnMessageEventArgs eventArgs)
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnMessage?.Invoke(this, eventArgs);
        }
        /// <summary>
        ///     wont rais the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseFatal(Exception e = null)
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnFatalErrorEventArgs onFatalErrorEventArgs = new OnFatalErrorEventArgs("Fatal network error.");
            if (e != null)
            {
                onFatalErrorEventArgs = new OnFatalErrorEventArgs(e);
            }
            OnFatality?.Invoke(this, onFatalErrorEventArgs);
        }
        internal void RaiseDisconnected()
        {
            LOGGER?.TraceMethodCall(GetType());
            OnDisconnected?.Invoke(this, new OnDisconnectedEventArgs());
        }
        internal void RaiseConnected()
        {
            LOGGER?.TraceMethodCall(GetType());
            OnConnected?.Invoke(this, new OnConnectedEventArgs());
        }
        #endregion invoker/raiser internal


        #region methods public
        public bool Send(string message)
        {
            LOGGER?.TraceMethodCall(GetType());
            try
            {
                lock (LOCK)
                {
                    SpecificClientSend(message);
                    return true;
                }
            }
            catch (Exception e)
            {
                RaiseSendFailed(new OnSendFailedEventArgs(message, e));
                return false;
            }
        }

        public bool Open()
        {
            LOGGER?.TraceMethodCall(GetType());
            return OpenPrivate(false);
        }
        public void Close()
        {
            LOGGER?.TraceMethodCall(GetType());
            // ConnectionWatchDog has to be stopped first
            // so that it wont reconnect
            NetworkServices.Stop();
            // ClosePrivate() also handles IClientOptions.DisconnectWait
            ClosePrivate();
        }
        /// <summary>
        ///     <inheritdoc cref="Close"/>
        /// </summary>
        public void Dispose()
        {
            LOGGER?.TraceMethodCall(GetType());
            Close();
            GC.SuppressFinalize(this);
        }

        public bool Reconnect()
        {
            LOGGER?.TraceMethodCall(GetType());
            // stops everything, ConnectionWatchDog too
            if (IsConnected)
            {
                Close();
            }
            // interface IClient doesnt declare a return value for Reconnect()
            // so we can suppress IDE0058 of ReconnectInternal()
            return ReconnectInternal();
        }
        #endregion methods public


        #region methods private
        private bool OpenPrivate(bool isReconnect)
        {
            LOGGER?.TraceMethodCall(GetType());
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
                // at this time,
                // the specific 'System.Net.WebSockets.ClientWebSocket'
                // or 'System.Net.Sockets.TcpClient'
                // is null or it is disposed
                // this has to be the only place where 'SetSpecificClient()' is called!!!
                SetSpecificClient();
                bool first = true;
                Options.ReconnectionPolicy.Reset(isReconnect);
                while (!IsConnected
                       && !Options.ReconnectionPolicy.AreAttemptsComplete())
                {
                    LOGGER?.TraceAction(GetType(), "try to connect");
                    if (!first)
                    {
                        Task.Delay(Options.ReconnectionPolicy.GetReconnectInterval()).GetAwaiter().GetResult();
                    }

                    SpecificClientConnect();
                    Options.ReconnectionPolicy.ProcessValues();
                    first = false;
                }
                if (!IsConnected)
                {
                    LOGGER?.TraceAction(GetType(), "Client couldnt establish a connection");
                    RaiseFatal();
                    return false;
                }
                LOGGER?.TraceAction(GetType(), "Client established a connection");
                NetworkServices.Start();
                if (!isReconnect)
                {
                    RaiseConnected();
                }

                return true;
            }
            catch (Exception ex)
            {
                LOGGER?.LogExceptionAsError(GetType(), ex);
                RaiseError(new OnErrorEventArgs(ex));
                RaiseFatal();
                return false;
            }
        }
        /// <summary>
        ///     stops <see cref="NetworkServices.ListenTask"/>
        ///     by calling <see cref="CancellationTokenSource.Cancel()"/>
        ///     <br></br>
        ///     <br></br>
        ///     and enforces the <see cref="SpecificClientClose()"/>
        ///     <br></br>
        ///     <br></br>
        ///     afterwards it waits for the via <see cref="IClientOptions.DisconnectWait"/> given amount of milliseconds
        ///     <br></br>
        ///     <br></br>
        ///     <see cref="ConnectionWatchDog"/> will keep running,
        ///     because itself issued this call by calling <see cref="ReconnectInternal()"/>
        /// </summary>
        private void ClosePrivate()
        {
            LOGGER?.TraceMethodCall(GetType());
            // this call to Cancel stops
            // NetworkServices.ListenTask
            CancellationTokenSource.Cancel();
            LOGGER?.TraceAction(GetType(), $"{nameof(CancellationTokenSource)}.{nameof(CancellationTokenSource.Cancel)} is called");
            SpecificClientClose();
            RaiseDisconnected();
            // only here and in the ctor.
            // ctor: initial
            // further flow: after everything is closed
            CancellationTokenSource = new CancellationTokenSource();
            Task.Delay(TimeSpan.FromMilliseconds(Options.DisconnectWait)).GetAwaiter().GetResult();
        }
        #endregion methods private


        #region methods protected
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
        protected abstract void SpecificClientSend(string message);
        /// <summary>
        ///     to instantiate the underlying
        ///     <list>
        ///         <item>
        ///             <see cref="System.Net.Sockets.TcpClient"/>
        ///         </item>
        ///         <item>or</item>
        ///         <item>
        ///             <see cref="System.Net.WebSockets.ClientWebSocket"/>
        ///         </item>
        ///     </list>
        /// </summary>
        protected abstract T NewClient();
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
        protected abstract void SpecificClientClose();

        /// <summary>
        ///     the specific connect for one of the following Client-Types
        ///     <list>
        ///         <item>
        ///             <see cref="System.Net.Sockets.TcpClient"/>
        ///         </item>
        ///         <item>
        ///             <see cref="System.Net.WebSockets.ClientWebSocket"/>
        ///         </item>
        ///     </list>
        /// </summary>
        protected abstract void SpecificClientConnect();
        /// <summary>
        ///     this method is needed,
        ///     cause closing/aborting the clients
        ///     <list>
        ///         <item>
        ///             <see cref="System.Net.Sockets.TcpClient"/>
        ///         </item>
        ///         <item>
        ///             <see cref="System.Net.WebSockets.ClientWebSocket"/>
        ///         </item>
        ///     </list>
        ///     <b>also disposes them</b>
        ///     <br></br>
        ///     <br></br>
        ///     its the only method, that sets one of the clients mentioned above
        ///     <br></br>
        ///     <br></br>
        ///     <b>it musnt be called anywhere else than within <see cref="Open()"/>!!!</b>
        /// </summary>
        protected void SetSpecificClient()
        {
            LOGGER?.TraceMethodCall(GetType());
            // this should be the only place where the Client is set!
            // dont do it anywhere else
            Client = NewClient();
        }
        #endregion methods protected


        #region methods internal
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
            LOGGER?.TraceMethodCall(GetType());
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
        #endregion methods internal
    }
}