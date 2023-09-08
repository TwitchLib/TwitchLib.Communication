namespace TwitchLib.Communication.Models;

/// <summary>
///     Connection/Reconnection-Policy
///     <br></br>
///     <br></br>
///     controls the attempts to make to connect and to reconnect to twitch
///     <br></br>
///     <br></br>
///     to omit reconnects and to only make one attempt to connect to twitch, please use <see cref="NoReconnectionPolicy"/>
/// </summary>
public class ReconnectionPolicy
{
    private readonly int _reconnectStepInterval;
    private readonly int? _initMaxAttempts;
    private int _currentReconnectInterval;
    private readonly int _maxReconnectInterval;
    private int? _maxAttempts;
    private int _attemptsMade;

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
    ///     try to connect -> couldn't connect -> wait 3_000 milliseconds -> try to connect -> couldn't connect -> wait 6_000 milliseconds -> and so on
    /// </summary>
    public ReconnectionPolicy()
    {
        _reconnectStepInterval = 3_000;
        _currentReconnectInterval = _reconnectStepInterval;
        _maxReconnectInterval = 30_000;
        _maxAttempts = null;
        _initMaxAttempts = null;
        _attemptsMade = 0;
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
    public ReconnectionPolicy(
        int minReconnectInterval,
        int maxReconnectInterval,
        int maxAttempts)
    {
        _reconnectStepInterval = minReconnectInterval;
        _currentReconnectInterval = minReconnectInterval > maxReconnectInterval
            ? maxReconnectInterval
            : minReconnectInterval;
        _maxReconnectInterval = maxReconnectInterval;
        _maxAttempts = maxAttempts;
        _initMaxAttempts = maxAttempts;
        _attemptsMade = 0;
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
    ///     try to connect -> couldn't connect -> wait 3_000 milliseconds -> try to connect -> couldn't connect -> wait 6_000 milliseconds -> and so on
    /// </summary>
    /// <param name="minReconnectInterval">
    ///     minimum interval in milliseconds
    /// </param>
    /// <param name="maxReconnectInterval">
    ///     maximum interval in milliseconds
    /// </param>
    public ReconnectionPolicy(
        int minReconnectInterval,
        int maxReconnectInterval)
    {
        _reconnectStepInterval = minReconnectInterval;
        _currentReconnectInterval = minReconnectInterval > maxReconnectInterval
            ? maxReconnectInterval
            : minReconnectInterval;
        _maxReconnectInterval = maxReconnectInterval;
        _maxAttempts = null;
        _initMaxAttempts = null;
        _attemptsMade = 0;
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
        _reconnectStepInterval = reconnectInterval;
        _currentReconnectInterval = reconnectInterval;
        _maxReconnectInterval = reconnectInterval;
        _maxAttempts = null;
        _initMaxAttempts = null;
        _attemptsMade = 0;
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
    public ReconnectionPolicy(
        int reconnectInterval,
        int? maxAttempts)
    {
        _reconnectStepInterval = reconnectInterval;
        _currentReconnectInterval = reconnectInterval;
        _maxReconnectInterval = reconnectInterval;
        _maxAttempts = maxAttempts;
        _initMaxAttempts = maxAttempts;
        _attemptsMade = 0;
    }

    internal void Reset(bool isReconnect)
    {
        if (isReconnect) return;
        _attemptsMade = 0;
        _currentReconnectInterval = _reconnectStepInterval;
        _maxAttempts = _initMaxAttempts;
    }

    internal void ProcessValues()
    {
        _attemptsMade++;
        if (_currentReconnectInterval < _maxReconnectInterval)
        {
            _currentReconnectInterval += _reconnectStepInterval;
        }

        if (_currentReconnectInterval > _maxReconnectInterval)
        {
            _currentReconnectInterval = _maxReconnectInterval;
        }
    }

    public int GetReconnectInterval()
    {
        return _currentReconnectInterval;
    }

    public bool AreAttemptsComplete()
    {
        return _attemptsMade == _maxAttempts;
    }
}
