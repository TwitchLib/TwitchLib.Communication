
using System;

namespace TwitchLib.Communication.Models
{
    /// <summary>
    ///     Connection/Reconnection-Policy
    ///     <br></br>
    ///     <br></br>
    ///     controls the attempts to make to connect and to reconnect to twitch
    ///     <br></br>
    ///     <br></br>
    ///     to omit reconnects and to only make one attempt to connect to twitch, please use the ctor. <see cref="ReconnectionPolicy(Boolean)"/>
    /// </summary>
    public class ReconnectionPolicy
    {
        private readonly int reconnectStepInterval;
        private readonly int? initMaxAttempts;
        private int currentReconnectInterval;
        private readonly int maxReconnectInterval;
        private int? maxAttempts;
        private int attemptsMade;

        public bool OmitReconnect { get; }

        /// <summary>
        ///     this Constructor can/should be used to omit reconnect-attempts
        /// </summary>
        /// <param name="omitReconnect">
        ///     <see langword="true"/> if the <see cref="Interfaces.IClient"/> should not reconnect
        ///     <br></br>
        ///     <br></br>
        ///     passing <see langword="false"/> to this ctor. throws an <see cref="ArgumentOutOfRangeException"/>
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     if <paramref name="omitReconnect"/> is <see langword="false"/>
        /// </exception>
        public ReconnectionPolicy(bool omitReconnect)
        {
            if (!omitReconnect) throw new ArgumentOutOfRangeException(nameof(omitReconnect), "To use this Constructor, the parameters value has to be true!");
            OmitReconnect = true;
            initMaxAttempts = 1;
            maxAttempts = 1;
        }

        /// <summary>
        ///     the <see cref="TwitchLib.Communication.Clients.TcpClient"/> or <see cref="TwitchLib.Communication.Clients.WebSocketClient"/>
        ///     <b>infinitely</b>
        ///     attempts to reconnect
        ///     <br></br>
        ///     <br></br>
        ///     with each attempt, the reconnect interval increases by 3_000 milliseconds
        ///     until it reaches 30_000 milliseconds
        ///     <br></br>
        ///     
        ///     <br></br>
        ///     <br></br>
        ///     Example:
        ///     <br></br>
        ///     try to connect -> couldnt connect -> wait 3_000 milliseconds -> try to connect -> couldnt connect -> wait 6_000 milliseconds -> and so on
        /// </summary>
        public ReconnectionPolicy()
        {
            reconnectStepInterval = 3_000;
            currentReconnectInterval = reconnectStepInterval;
            maxReconnectInterval = 30_000;
            maxAttempts = null;
            initMaxAttempts = null;
            attemptsMade = 0;
        }

        /// <summary>
        ///     the <see cref="TwitchLib.Communication.Clients.TcpClient"/> or <see cref="TwitchLib.Communication.Clients.WebSocketClient"/>
        ///     attempts to reconnect for <paramref name="maxAttempts"/> times
        ///     <br></br>
        ///     <br></br>
        ///     with each attempt, the reconnect interval increases by the amount of <paramref name="minReconnectInterval"/>
        ///     until it reaches <paramref name="maxReconnectInterval"/>
        ///     <br></br>
        ///     <br></br>
        ///     Example:
        ///     <br></br>
        ///     <paramref name="minReconnectInterval"/> = 3_000
        ///     <br></br>
        ///     <paramref name="maxReconnectInterval"/> = 30_000
        ///     <br></br>
        ///     try to connect -> couldnt connect -> wait 3_000 milliseconds -> try to connect -> couldnt connect -> wait 6_000 milliseconds -> and so on
        /// </summary>
        /// <param name="minReconnectInterval">
        ///     minimum interval in milliseconds
        /// </param>
        /// <param name="maxReconnectInterval">
        ///     maximum interval in milliseconds
        /// </param>
        /// <param name="maxAttempts">
        ///     <see langword="null"/> means <b>infinite</b>; it never stops to try to reconnect
        /// </param>
        public ReconnectionPolicy(int minReconnectInterval,
                                  int maxReconnectInterval,
                                  int maxAttempts)
        {
            reconnectStepInterval = minReconnectInterval;
            currentReconnectInterval = minReconnectInterval > maxReconnectInterval
                ? maxReconnectInterval
                : minReconnectInterval;
            this.maxReconnectInterval = maxReconnectInterval;
            this.maxAttempts = maxAttempts;
            initMaxAttempts = maxAttempts;
            attemptsMade = 0;
        }

        /// <summary>
        ///     the <see cref="TwitchLib.Communication.Clients.TcpClient"/> or <see cref="TwitchLib.Communication.Clients.WebSocketClient"/>
        ///     <b>infinitely</b>
        ///     attempts to reconnect
        ///     <br></br>
        ///     <br></br>
        ///     with each attempt, the reconnect interval increases by the amount of <paramref name="minReconnectInterval"/>
        ///     until it reaches <paramref name="maxReconnectInterval"/>
        ///     <br></br>
        ///     <br></br>
        ///     Example:
        ///     <br></br>
        ///     <paramref name="minReconnectInterval"/> = 3_000
        ///     <br></br>
        ///     <paramref name="maxReconnectInterval"/> = 30_000
        ///     <br></br>
        ///     try to connect -> couldnt connect -> wait 3_000 milliseconds -> try to connect -> couldnt connect -> wait 6_000 milliseconds -> and so on
        /// </summary>
        /// <param name="minReconnectInterval">
        ///     minimum interval in milliseconds
        /// </param>
        /// <param name="maxReconnectInterval">
        ///     maximum interval in milliseconds
        /// </param>
        public ReconnectionPolicy(int minReconnectInterval,
                                  int maxReconnectInterval)
        {
            reconnectStepInterval = minReconnectInterval;
            currentReconnectInterval = minReconnectInterval > maxReconnectInterval
                ? maxReconnectInterval
                : minReconnectInterval;
            this.maxReconnectInterval = maxReconnectInterval;
            maxAttempts = null;
            initMaxAttempts = null;
            attemptsMade = 0;
        }
        /// <summary>
        ///     the <see cref="TwitchLib.Communication.Clients.TcpClient"/> or <see cref="TwitchLib.Communication.Clients.WebSocketClient"/>
        ///     <b>infinitely</b>
        ///      attempts to reconnect every <paramref name="reconnectInterval"/>-milliseconds
        /// </summary>
        /// <param name="reconnectInterval">
        ///     Interval in milliseconds between trying to reconnect
        /// </param>
        public ReconnectionPolicy(int reconnectInterval)
        {
            reconnectStepInterval = reconnectInterval;
            currentReconnectInterval = reconnectInterval;
            maxReconnectInterval = reconnectInterval;
            maxAttempts = null;
            initMaxAttempts = null;
            attemptsMade = 0;
        }

        /// <summary>
        ///     the <see cref="TwitchLib.Communication.Clients.TcpClient"/> or <see cref="TwitchLib.Communication.Clients.WebSocketClient"/>
        ///     attempts to reconnect every <paramref name="reconnectInterval"/>-milliseconds for <paramref name="maxAttempts"/> times
        /// </summary>
        /// <param name="reconnectInterval">
        ///     Interval in milliseconds between trying to reconnect
        /// </param>
        /// <param name="maxAttempts">
        ///     <see langword="null"/> means <b>infinite</b>; it never stops to try to reconnect
        /// </param>
        public ReconnectionPolicy(int reconnectInterval,
                                  int? maxAttempts)
        {
            reconnectStepInterval = reconnectInterval;
            currentReconnectInterval = reconnectInterval;
            maxReconnectInterval = reconnectInterval;
            this.maxAttempts = maxAttempts;
            initMaxAttempts = maxAttempts;
            attemptsMade = 0;
        }

        internal void Reset(bool isReconnect)
        {
            if (isReconnect) return;
            attemptsMade = 0;
            currentReconnectInterval = reconnectStepInterval;
            maxAttempts = initMaxAttempts;
        }

        internal void ProcessValues()
        {
            attemptsMade++;
            if (currentReconnectInterval < maxReconnectInterval)
            {
                currentReconnectInterval += reconnectStepInterval;
            }

            if (currentReconnectInterval > maxReconnectInterval)
            {
                currentReconnectInterval = maxReconnectInterval;
            }
        }

        public int GetReconnectInterval()
        {
            return currentReconnectInterval;
        }

        public bool AreAttemptsComplete()
        {
            if (maxAttempts == null)
            {
                return false;
            }

            return attemptsMade == maxAttempts;
        }
    }
}