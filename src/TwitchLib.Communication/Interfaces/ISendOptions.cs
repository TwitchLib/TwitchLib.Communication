using System;

namespace TwitchLib.Communication.Interfaces
{

    public interface ISendOptions
    {

        /// <summary>
        ///     Number of Messages Allowed Per Instance of the <see cref="IClientOptions.ThrottlingPeriod"/>.
        ///     <br></br>
        ///     <br></br>
        ///     see also <see cref="IClientOptions.ThrottlingPeriod"/>:
        ///     <br></br>
        ///     <inheritdoc cref="IClientOptions.ThrottlingPeriod"/>
        ///     <br></br>
        ///     <br></br>
        ///     see also <see cref="Enums.MessageRateLimit"/>:
        ///     <br></br>
        ///     <inheritdoc cref="Enums.MessageRateLimit"/>
        /// </summary>
        uint SendsAllowedInPeriod { get; }

        /// <summary>
        /// The amount of time an object can wait to be sent before it is considered dead, and should be skipped (default 30 minutes).
        /// A dead item will be ignored and removed from the send queue when it is hit.
        /// </summary>
        TimeSpan CacheItemTimeout { get; }

        /// <summary>
        /// Maximum number of Queued outgoing messages (default 10_000).
        /// </summary>
        uint QueueCapacity { get; }
    }
}