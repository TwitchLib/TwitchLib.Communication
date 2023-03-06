

using System;

using Microsoft.Extensions.Logging;

using TwitchLib.Communication.Extensions;
using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Clients
{

    /// <summary>
    ///     this is a generic "Overlay",
    ///     to get rid of the <see cref="System.Type"/>-Parameter in <see cref="AClientBase"/>
    /// </summary>
    /// <typeparam name="T">
    ///     should be one of the following
    ///     <list>
    ///         <item>
    ///             <see cref="System.Net.Sockets.TcpClient"/>
    ///         </item>
    ///         <item>
    ///             <see cref="System.Net.WebSockets.ClientWebSocket"/>
    ///         </item>
    ///     </list>
    /// </typeparam>
    public abstract class AClientBaseTyped<T> : AClientBase where T : IDisposable
    {


        #region properties protected
        protected abstract string URL { get; }
        #endregion properties protected


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
        #endregion properties public


        #region ctors
        internal AClientBaseTyped(IClientOptions options = null,
                         ILogger logger = null) : base(options, logger)
        {
            // INFO: Feedback by Bukk94: not to restrict the Client to those two known types
            //if (typeof(T) != typeof(System.Net.WebSockets.ClientWebSocket)
            //    && typeof(T) != typeof(System.Net.Sockets.TcpClient))
            //{
            //    throw new ArgumentOutOfRangeException(nameof(T),
            //                                          typeof(T),
            //                                          "Type-Parameter hast to be 'System.Net.Sockets.TcpClient' or 'System.Net.WebSockets.ClientWebSocket'");
            //}
        }
        #endregion ctors


        #region methods protected
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
        protected override void SetSpecificClient()
        {
            LOGGER?.TraceMethodCall(GetType());
            // this should be the only place where the Client is set!
            // dont do it anywhere else
            Client = NewClient();
        }
        #endregion methods protected
    }
}
