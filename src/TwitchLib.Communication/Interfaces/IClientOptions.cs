using TwitchLib.Communication.Enums;
using TwitchLib.Communication.Models;

namespace TwitchLib.Communication.Interfaces;

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
}
