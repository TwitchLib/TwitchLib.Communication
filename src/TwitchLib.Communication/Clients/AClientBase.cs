using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using TwitchLib.Communication.Enums;
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
    public abstract class AClientBase : IClient
    {
        #region properties protected
        protected ILogger LOGGER { get; }
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
        public event EventHandler<OnDataEventArgs> OnData;
        public event EventHandler<OnDisconnectedEventArgs> OnDisconnected;
        public event EventHandler<OnErrorEventArgs> OnError;
        public event EventHandler<OnFatalErrorEventArgs> OnFatality;
        public event EventHandler<OnMessageEventArgs> OnMessage;
        public event EventHandler<OnMessageThrottledEventArgs> OnMessageThrottled;
        public event EventHandler<OnWhisperThrottledEventArgs> OnWhisperThrottled;
        public event EventHandler<OnSendFailedEventArgs> OnSendFailed;
        public event EventHandler<OnStateChangedEventArgs> OnStateChanged;
        public event EventHandler<OnReconnectedEventArgs> OnReconnected;
        #endregion events public


        #region properties public
        public abstract bool IsConnected { get; }
        public IClientOptions Options { get; }

        #endregion properties public


        #region properties private
        /// <summary>
        ///     this <see cref="CancellationTokenSource"/> is used for <see cref="NetworkServices.ListenTask"/>
        ///     whenever a call to <see cref="CancellationTokenSource.Cancel()"/> is made,
        ///     the <see cref="Task"/>s listed above are cancelled
        /// </summary>
        private CancellationTokenSource CancellationTokenSource { get; set; }
        private ThrottlerService Throttler { get; }
        private NetworkServices NetworkServices { get; }
        #endregion properties private


        #region ctor(s)
        internal AClientBase(IClientOptions options = null,
                             ILogger logger = null)
        {
            LOGGER = logger;
            CancellationTokenSource = new CancellationTokenSource();
            Options = options ?? new ClientOptions();
            Throttler = new ThrottlerService(this,
                                                  Options.MessageSendOptions,
                                                  Options.WhisperSendOptions,
                                                  logger);
            NetworkServices = new NetworkServices(this,
                                                       Throttler,
                                                       logger);
        }
        #endregion ctor(s)


        #region invoker/raiser internal
        internal void RaiseWhisperThrottled(OnWhisperThrottledEventArgs eventArgs)
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnWhisperThrottled?.Invoke(this, eventArgs);
        }

        internal void RaiseMessageThrottled(OnMessageThrottledEventArgs eventArgs)
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnMessageThrottled?.Invoke(this, eventArgs);
        }

        internal void RaiseSendFailed(OnSendFailedEventArgs eventArgs)
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnSendFailed?.Invoke(this, eventArgs);
        }

        internal void RaiseError(OnErrorEventArgs eventArgs)
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnError?.Invoke(this, eventArgs);
        }
        internal void RaiseReconnected()
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnReconnected?.Invoke(this, new OnReconnectedEventArgs());
        }
        internal void RaiseMessage(OnMessageEventArgs eventArgs)
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnMessage?.Invoke(this, eventArgs);
        }
        internal void RaiseFatal()
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnFatality?.Invoke(this, new OnFatalErrorEventArgs("Fatal network error."));
        }
        internal void RaiseFatal(Exception e)
        {
            LOGGER?.TraceMethodCall(GetType());
            if (Token.IsCancellationRequested)
            {
                return;
            }
            OnFatality?.Invoke(this, new OnFatalErrorEventArgs(e));
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
        internal void RaiseStateChanged(OnStateChangedEventArgs eventArgs)
        {
            LOGGER?.TraceMethodCall(GetType());
            OnStateChanged?.Invoke(this, eventArgs);
        }
        #endregion invoker/raiser internal


        #region methods public
        public bool SendPONG()
        {
            LOGGER?.TraceMethodCall(GetType());
            // TODO: check if thats correct, do we have to bypass throttling for PONG-messages? if not, remove this stuff
            return Throttler.Enqueue("PONG", MessageType.ByPass);
        }
        public bool Send(string message)
        {
            LOGGER?.TraceMethodCall(GetType());
            return Throttler.Enqueue(message, MessageType.Message);
        }

        public bool SendWhisper(string message)
        {
            LOGGER?.TraceMethodCall(GetType());
            return Throttler.Enqueue(message, MessageType.Whisper);
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
                Options.ReconnectionPolicy.Reset();
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
                RaiseError(new OnErrorEventArgs() { Exception = ex });
                RaiseFatal();
                return false;
            }
        }
        /// <summary>
        ///     stops <see cref="NetworkServices.SendWhisperTask"/>
        ///     by calling <see cref="CancellationTokenSource.Cancel()"/>
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
            CancellationTokenSource = new CancellationTokenSource();
            Task.Delay(TimeSpan.FromMilliseconds(Options.DisconnectWait)).GetAwaiter().GetResult();
        }
        #endregion methods private


        #region methods protected
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
        protected abstract void SetSpecificClient();
        #endregion methods protected


        #region methods internal
        /// <summary>
        ///     the 'real' send
        ///     <br></br>
        ///     <br></br>
        ///     should only be used by <see cref="Services.ThrottlerService"/>
        /// </summary>
        /// <param name="message">
        ///     IRC-Messsage
        /// </param>
        internal abstract void SendIRC(string message);
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
        internal bool SendPING()
        {
            LOGGER?.TraceMethodCall(GetType());
            // TODO: check if thats correct, do we have to bypass throttling for PING-messages? if not, change it to normal Send("PING")
            //this.Send("PING");
            return Throttler.Enqueue("PING", MessageType.ByPass);
        }
        #endregion methods internal
    }
}