﻿using System;

using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Models
{

    public class SendOptions : ISendOptions
    {
        /// <summary>
        ///     <inheritdoc cref="ISendOptions.SendsAllowedInPeriod"/>
        /// </summary>
        public uint SendsAllowedInPeriod { get; }
        /// <summary>
        ///     <inheritdoc cref="ISendOptions.QueueCapacity"/>
        /// </summary>
        public uint QueueCapacity { get; }
        /// <summary>
        ///     <inheritdoc cref="ISendOptions.CacheItemTimeout"/>
        /// </summary>
        public TimeSpan CacheItemTimeout { get; }
        /// <summary>
        /// </summary>
        /// <param name="sendsAllowedInPeriod">
        ///     <inheritdoc cref="SendsAllowedInPeriod"/>
        /// </param>
        /// <param name="queueCapacity">
        ///     <inheritdoc cref="QueueCapacity"/>
        /// </param>
        /// <param name="cacheItemTimeoutInMinutes">
        ///     <inheritdoc cref="CacheItemTimeout"/>
        /// </param>
        public SendOptions(uint sendsAllowedInPeriod,
                           uint queueCapacity = 10_000,
                           uint cacheItemTimeoutInMinutes = 30)
        {
            SendsAllowedInPeriod = sendsAllowedInPeriod;
            QueueCapacity = queueCapacity;
            CacheItemTimeout = TimeSpan.FromMinutes(cacheItemTimeoutInMinutes);
        }
    }
}