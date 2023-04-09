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
    public abstract class ClientBase<T> : IClient where T : IDisposable
    {
        private readonly object _lock = new object();

        #region properties protected
        protected ILogger Logger { get; }
        protected abstract string Url { get; }
        #endregion properties protected


        #region properties internal
        /// <summary>
        ///     <inheritdoc cref="CancellationTokenSource"/>
        /// </summary>
        internal CancellationToken Token => CancellationTokenSource.Token;

        internal static TimeSpan TimeOutEstablishConnection => TimeSpan.FromSeconds(15);
        #endregion properties internal


        #region events public
        public event EventHandler<ConnectedEventArgs> Connected;
        public event EventHandler<DisconnectedEventArgs> Disconnected;
        public event EventHandler<ErrorEventArgs> Error;
        public event EventHandler<FatalErrorEventArgs> Fatality;
        public event EventHandler<MessageEventArgs> Message;
        public event EventHandler<SendFailedEventArgs> SendFailed;
        public event EventHandler<ReconnectedEventArgs> Reconnected;
        #endregion events public


        #region properties public
        public T Client { get; private set; }
        public abstract bool IsConnected { get; }
        public IClientOptions Options { get; }

        #endregion properties public


        #region properties private
        private CancellationTokenSource CancellationTokenSource { get; set; }
        private NetworkServices<T> NetworkServices { get; }
        #endregion properties private


        #region ctor(s)
        internal ClientBase(IClientOptions options = null,
                             ILogger logger = null)
        {
            Logger = logger;
            CancellationTokenSource = new CancellationTokenSource();
            Options = options ?? new ClientOptions();
            NetworkServices = new NetworkServices<T>(this, logger);
        }
        #endregion ctor(s)

        #region invoker/raiser internal
        /// <summary>
        ///     wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseSendFailed(SendFailedEventArgs eventArgs)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            SendFailed?.Invoke(this, eventArgs);
        }

        /// <summary>
        ///  wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseError(ErrorEventArgs eventArgs)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            Error?.Invoke(this, eventArgs);
        }
        /// <summary>
        ///     wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseReconnected()
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            Reconnected?.Invoke(this, new ReconnectedEventArgs());
        }
        /// <summary>
        ///     wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseMessage(MessageEventArgs eventArgs)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            Message?.Invoke(this, eventArgs);
        }
        /// <summary>
        ///     wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
        /// </summary>
        internal void RaiseFatal(Exception e = null)
        {
            Logger?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            FatalErrorEventArgs onFatalErrorEventArgs = new FatalErrorEventArgs("Fatal network error.");
            if (e != null)
            {
                onFatalErrorEventArgs = new FatalErrorEventArgs(e);
            }
            Fatality?.Invoke(this, onFatalErrorEventArgs);
        }
        internal void RaiseDisconnected()
        {
            Logger?.TraceMethodCall(GetType());
            Disconnected?.Invoke(this, new DisconnectedEventArgs());
        }
        internal void RaiseConnected()
        {
            Logger?.TraceMethodCall(GetType());
            Connected?.Invoke(this, new ConnectedEventArgs());
        }
        #endregion invoker/raiser internal


        #region methods public
        public bool Send(string message)
        {
            Logger?.TraceMethodCall(GetType());
            try
            {
                lock (_lock)
                {
                    SpecificClientSend(message);
                    return true;
                }
            }
            catch (Exception e)
            {
                RaiseSendFailed(new SendFailedEventArgs(message, e));
                return false;
            }
        }

        public bool Open()
        {
            Logger?.TraceMethodCall(GetType());
            return OpenInternal(false);
        }
        public void Close()
        {
            Logger?.TraceMethodCall(GetType());
            // ConnectionWatchDog has to be stopped first
            // so that it wont reconnect
            NetworkServices.Stop();
            // ClosePrivate() also handles IClientOptions.DisconnectWait
            CloseInternal();
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
        private bool OpenInternal(bool isReconnect)
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
                    Logger?.TraceAction(GetType(), "try to connect");
                    if (!first)
                    {
                        Task.Delay(Options.ReconnectionPolicy.GetReconnectInterval(), Token).GetAwaiter().GetResult();
                    }

                    SpecificClientConnect();
                    Options.ReconnectionPolicy.ProcessValues();
                    first = false;
                }
                if (!IsConnected)
                {
                    Logger?.TraceAction(GetType(), "Client couldn't establish a connection");
                    RaiseFatal();
                    return false;
                }
                Logger?.TraceAction(GetType(), "Client established a connection");
                NetworkServices.Start();
                if (!isReconnect)
                {
                    RaiseConnected();
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger?.LogExceptionAsError(GetType(), ex);
                RaiseError(new ErrorEventArgs(ex));
                RaiseFatal();
                return false;
            }
        }
        /// <summary>
        ///     stops <see cref="ListenTask"/>
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
        private void CloseInternal()
        {
            Logger?.TraceMethodCall(GetType());
            // this call to Cancel stops
            // NetworkServices.ListenTask
            CancellationTokenSource.Cancel();
            Logger?.TraceAction(GetType(), $"{nameof(CancellationTokenSource)}.{nameof(CancellationTokenSource.Cancel)} is called");
            SpecificClientClose();
            RaiseDisconnected();
            // only here and in the ctor.
            // ctor: initial
            // further flow: after everything is closed
            CancellationTokenSource = new CancellationTokenSource();
            Task.Delay(TimeSpan.FromMilliseconds(Options.DisconnectWait), Token).GetAwaiter().GetResult();
        }
        #endregion methods private


        #region methods protected
        /// <summary>
        ///     specific client send method
        /// </summary>
        /// <param name="message">
        ///     IRC-Message
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
            Logger?.TraceMethodCall(GetType());
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
        ///     it stops all <see cref="NetworkServices"/> but <see cref="ConnectionWatchDog"/>!
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
            CloseInternal();
            bool reconnected = OpenInternal(true);
            if (reconnected)
            {
                RaiseReconnected();
            }
            return reconnected;
        }
        /// <summary>
        ///     just the Action that listens for new Messages
        ///     the corresponding <see cref="Task"/> is held by <see cref="NetworkServices"/>
        /// </summary>
        internal abstract void ListenTaskAction();
        #endregion methods internal
    }
}