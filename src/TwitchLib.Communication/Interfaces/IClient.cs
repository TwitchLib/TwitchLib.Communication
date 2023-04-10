using System;

using TwitchLib.Communication.Events;

namespace TwitchLib.Communication.Interfaces
{

    public interface IClient : IDisposable
    {

        #region properties

        /// <summary>
        ///     The current state of the connection.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        ///     Client Configuration Options
        /// </summary>
        IClientOptions Options { get; }
        #endregion properties


        #region events
        /// <summary>
        ///     Fires when the Client has connected
        /// </summary>
        event EventHandler<OnConnectedEventArgs> Connected;

        /// <summary>
        ///     Fires when the Client disconnects
        /// </summary>
        event EventHandler<OnDisconnectedEventArgs> Disconnected;

        /// <summary>
        ///     Fires when An Exception Occurs in the client
        /// </summary>
        event EventHandler<OnErrorEventArgs> Error;

        /// <summary>
        ///     Fires when a Fatal Error Occurs.
        /// </summary>
        event EventHandler<OnFatalErrorEventArgs> Fatality;

        /// <summary>
        ///     Fires when a Message/ group of messages is received.
        /// </summary>
        event EventHandler<OnMessageEventArgs> Message;

        /// <summary>
        ///     Fires when a message Send event failed.
        /// </summary>
        event EventHandler<OnSendFailedEventArgs> SendFailed;

        /// <summary>
        ///     Fires when the client reconnects automatically
        /// </summary>
        event EventHandler<OnReconnectedEventArgs> Reconnected;
        #endregion events


        #region methods

        /// <summary>
        ///     tries to connect to twitch according to <see cref="IClientOptions.ReconnectionPolicy"/>!
        /// </summary>
        /// <returns>
        ///     <see langword="true"/> if a connection could be established, <see langword="false"/> otherwise
        /// </returns>
        bool Open();
        /// <summary>
        ///     if the underlying Client is connected,
        ///     <br></br>
        ///     <see cref="Close()"/> is invoked
        ///     <br></br>
        ///     before it makes a call to <see cref="Open()"/> and <see cref="RaiseConnected()"/>
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
        bool Reconnect();
        /// <summary>
        ///     stops everything
        ///     and waits for the via <see cref="IClientOptions.DisconnectWait"/> given amount of milliseconds
        /// </summary>
        void Close();
        /// <summary>
        ///     sends the given irc-<paramref name="message"/>
        /// </summary>
        /// <param name="message">
        ///     irc-message to send
        /// </param>
        /// <returns>
        ///     <see langword="true"/>, if the message should be sent
        ///     <br></br>
        ///     <see langword="false"/> otherwise
        /// </returns>
        bool Send(string message);
        #endregion methods
    }
}