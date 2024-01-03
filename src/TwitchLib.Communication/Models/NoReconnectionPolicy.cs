namespace TwitchLib.Communication.Models;

/// <summary>
///    This policy should be used to omit reconnect-attempts.
/// </summary>
public class NoReconnectionPolicy : ReconnectionPolicy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoReconnectionPolicy"/>.
    /// </summary>
    public NoReconnectionPolicy()
        : base(
            reconnectInterval: 0, 
            maxAttempts: 1)
    {
    }
}
