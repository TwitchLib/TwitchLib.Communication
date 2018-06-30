using System;
using System.Collections.Generic;
using TwitchLib.Communication.Enums;

namespace TwitchLib.Communication
{
    public interface IClientOptions
    {
        /// <inheritdoc />
        ClientType ClientType { get; set; }
        /// <inheritdoc />
        int DisconnectWait { get; set; }
        /// <inheritdoc />
        IEnumerable<Tuple<string, string>> Headers { get; set; }
        /// <inheritdoc />
        int MessagesAllowedInPeriod { get; set; }
        /// <inheritdoc />
        ReconnectionPolicy ReconnectionPolicy { get; set; }
        /// <inheritdoc />
        TimeSpan SendCacheItemTimeout { get; set; }
        /// <inheritdoc />
        ushort SendDelay { get; set; }
        /// <inheritdoc />
        int SendQueueCapacity { get; set; }
        /// <inheritdoc />
        TimeSpan ThrottlingPeriod { get; set; }
        /// <inheritdoc />
        bool UseSSL { get; set; }
        /// <inheritdoc />
        TimeSpan WhisperThrottlingPeriod { get; set; }
        /// <inheritdoc />
        int WhispersAllowedInPeriod { get; set; }
        /// <inheritdoc />
        int WhisperQueueCapacity { get; set; }
    }
}