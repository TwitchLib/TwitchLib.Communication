using TwitchLib.Communication.Events;

namespace TwitchLib.Communication.Interfaces;

public interface IClient : IDisposable
{
    /// <summary>
    ///     The current state of the connection.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    ///     Client Configuration Options
    /// </summary>
    IClientOptions Options { get; }

    /// <summary>
    ///     Fires when the Client has connected
    /// </summary>
    event AsyncEventHandler<OnConnectedEventArgs>? OnConnected;

    /// <summary>
    ///     Fires when the Client disconnects
    /// </summary>
    event AsyncEventHandler<OnDisconnectedEventArgs>? OnDisconnected;

    /// <summary>
    ///     Fires when An Exception Occurs in the client
    /// </summary>
    event AsyncEventHandler<OnErrorEventArgs>? OnError;

    /// <summary>
    ///     Fires when a Fatal Error Occurs.
    /// </summary>
    event AsyncEventHandler<OnFatalErrorEventArgs>? OnFatality;

    /// <summary>
    ///     Fires when a Message/ group of messages is received.
    /// </summary>
    event AsyncEventHandler<OnMessageEventArgs>? OnMessage;

    /// <summary>
    ///     Fires when a message Send event failed.
    /// </summary>
    event AsyncEventHandler<OnSendFailedEventArgs>? OnSendFailed;

    /// <summary>
    ///     Fires when the client reconnects automatically
    /// </summary>
    event AsyncEventHandler<OnConnectedEventArgs>? OnReconnected;

    /// <summary>
    ///     tries to connect to twitch according to <see cref="IClientOptions.ReconnectionPolicy"/>!
    /// </summary>
    /// <returns>
    ///     <see langword="true"/> if a connection could be established, <see langword="false"/> otherwise
    /// </returns>
    Task<bool> OpenAsync();

    /// <summary>
    ///     if the underlying Client is connected,
    ///     <br></br>
    ///     <see cref="CloseAsync()"/> is invoked
    ///     <br></br>
    ///     before it makes a call to <see cref="OpenAsync()"/> and <see cref="RaiseConnected()"/>
    ///     <br></br>
    ///     <br></br>
    ///     this Method is also used by 'TwitchLib.Client.TwitchClient' 
    ///     <br></br>
    ///     whenever it receives a Reconnect-Message
    ///     <br></br>
    ///     <br></br>
    ///     so, if the twitch-servers want us to reconnect,
    ///     <br></br>
    ///     we have to close the connection and establish a new ones
    ///     <br></br>
    ///     <br></br>
    ///     can also be used for a manual reconnect
    /// </summary>
    /// <returns>
    ///     <see langword="true"/>, if the client reconnected; <see langword="false"/> otherwise
    /// </returns>
    Task<bool> ReconnectAsync();

    /// <summary>
    ///     stops everything
    ///     and waits for the via <see cref="IClientOptions.DisconnectWait"/> given amount of milliseconds
    /// </summary>
    Task CloseAsync();

    /// <summary>
    ///     Sends the given irc-<paramref name="message"/>
    /// </summary>
    /// <param name="message">
    ///     irc-message to send
    /// </param>
    /// <returns>
    ///     <see langword="true"/>, if the message was sent
    ///     <br></br>
    ///     <see langword="false"/> otherwise
    /// </returns>
    Task<bool> SendAsync(string message);
}
