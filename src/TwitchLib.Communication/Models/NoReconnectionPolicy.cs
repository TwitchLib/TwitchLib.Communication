namespace TwitchLib.Communication.Models
{
    /// <summary>
    ///    This policy should be used to omit reconnect-attempts.
    /// </summary>
    public class NoReconnectionPolicy : ReconnectionPolicy
    {
        public NoReconnectionPolicy()
            : base(
                reconnectInterval: 0, 
                maxAttempts: 1)
        {
        }
    }
}