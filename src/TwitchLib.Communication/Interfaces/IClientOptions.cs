using System;

using TwitchLib.Communication.Enums;
using TwitchLib.Communication.Models;

namespace TwitchLib.Communication.Interfaces
{

    public interface IClientOptions
    {
        /// <summary>
        /// Type of the Client to Create. Possible Types Chat or PubSub.
        /// </summary>
        ClientType ClientType { get; }

        /// <summary>
        /// How long to wait on a clean disconnect [in ms] (default 1_500ms).
        /// </summary>
        uint DisconnectWait { get; }

        /// <summary>
        /// Reconnection Policy Settings. Reconnect without Losing data etc.
        /// The Default Policy applied is 10 reconnection attempts with 3 seconds between each attempt.
        /// </summary>
        ReconnectionPolicy ReconnectionPolicy { get; }

        /// <summary>
        /// Use Secure Connection [SSL] (default: true)
        /// </summary>
        bool UseSsl { get; }
        /// <summary>
        /// Minimum time between sending items from the queue [in ms] (default 50ms).
        /// </summary>
        ushort SendDelay { get; }
        /// <summary>
        ///     Period Between each reset of the throttling instance window.
        ///     <br></br>
        ///     is always set to 30 seconds and you cannot change it
        ///     <br></br>
        ///     also in case of whispers!
        ///     <br></br>
        ///     <br></br>
        ///     <list type="bullet">
        ///         <item>
        ///             <see href="https://dev.twitch.tv/docs/irc/#rate-limits"/>
        ///         </item>
        ///         <item>
        ///             <see href="https://discuss.dev.twitch.tv/t/whisper-rate-limiting/2836"/>
        ///         </item>
        ///     </list>
        /// </summary>
        TimeSpan ThrottlingPeriod { get; }
        ISendOptions MessageSendOptions { get; }
        ISendOptions WhisperSendOptions { get; }
    }
}