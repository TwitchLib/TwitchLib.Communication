using System;

using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Models
{

    public class SendOptions : ISendOptions
    {
        /// <summary>
        ///     <inheritdoc cref="ISendOptions.MessagesAllowedInPeriod"/>
        /// </summary>
        public uint MessagesAllowedInPeriod { get; }
        /// <summary>
        ///     <inheritdoc cref="ISendOptions.SendQueueCapacity"/>
        /// </summary>
        public uint SendQueueCapacity { get; }
        /// <summary>
        ///     <inheritdoc cref="ISendOptions.SendCacheItemTimeout"/>
        /// </summary>
        public TimeSpan SendCacheItemTimeout { get; }
        /// <summary>
        /// </summary>
        /// <param name="messagesAllowedInPeriod">
        ///     <inheritdoc cref="MessagesAllowedInPeriod"/>
        /// </param>
        /// <param name="sendQueueCapacity">
        ///     <inheritdoc cref="SendQueueCapacity"/>
        /// </param>
        /// <param name="sendCacheItemTimeoutInMinutes">
        ///     <inheritdoc cref="SendCacheItemTimeout"/>
        /// </param>
        public SendOptions(uint messagesAllowedInPeriod,
                           uint sendQueueCapacity = 10_000,
                           uint sendCacheItemTimeoutInMinutes = 30)
        {
            MessagesAllowedInPeriod = messagesAllowedInPeriod;
            SendQueueCapacity = sendQueueCapacity;
            SendCacheItemTimeout = TimeSpan.FromMinutes(sendCacheItemTimeoutInMinutes);
        }
    }
}