using System;
using System.Threading;
using TwitchLib.Communication.Events;

namespace TwitchLib.Communication.Interfaces
{
    public interface IClient
    {
        /// <summary>
        /// Fires when the Client has connected
        /// </summary>
        event EventHandler<OnConnectedEventArgs> OnConnected;

        /// <summary>
        /// Fires when the client reconnects automatically
        /// </summary>
        event EventHandler<OnReconnectedEventArgs> OnReconnected;

        /// <summary>
        /// Fires when the Client disconnects
        /// </summary>
        event EventHandler<OnDisconnectedEventArgs> OnDisconnected;

        /// <summary>
        /// Fires when a Message/ group of messages is received.
        /// </summary>
        event EventHandler<OnMessageEventArgs> OnMessage;

        /// <summary>
        /// Fires when a Message has been throttled.
        /// </summary>
        event EventHandler<OnMessageThrottledEventArgs> OnMessageThrottled;

        /// <summary>
        /// Fires when a Whisper has been throttled.
        /// </summary>
        event EventHandler<OnWhisperThrottledEventArgs> OnWhisperThrottled;

        /// <summary>
        /// Fires when the connection state changes
        /// </summary>
        event EventHandler<OnStateChangedEventArgs> OnStateChanged;

        /// <summary>
        /// Fires when a message Send event failed.
        /// </summary>
        event EventHandler<OnSendFailedEventArgs> OnSendFailed;

        /// <summary>
        /// Fires when An Exception Occurs in the client
        /// </summary>
        event EventHandler<OnErrorEventArgs> OnError;

        /// <summary>
        /// Fires when a Fatal Error Occurs.
        /// </summary>
        event EventHandler<OnFatalErrorEventArgs> OnFatality;

        /// <summary>
        /// Client CancellationTokenSource
        /// </summary>
        CancellationTokenSource TokenSource { get; set; }

        /// <summary>
        /// Client Configuration Options
        /// </summary>
        IClientOptions Options { get; }

        /// <summary>
        /// The current state of the connection.
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Connect the Client to the requested Url. (Alternative to Connect())
        /// </summary>
        /// <returns>Returns True if Connected, False if Failed to Connect.</returns>
        bool Open();

        /// <summary>
        /// Connect the Client to the requested Url.
        /// </summary>
        /// <returns>Returns True if Connected, False if Failed to Connect.</returns>
        bool Connect();

        /// <summary>
        /// Manually reconnects the client.
        /// </summary>
        void Reconnect();

        /// <summary>
        /// Disconnect the Client from the Server (Alternative to Disconnect())
        /// </summary>
        void Close();

        /// <summary>
        /// Disconnect the Client from the Server
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Queue a Message to Send to the server as a String. (Alternative to SendMessage())
        /// </summary>
        /// <param name="message">The Message To Queue</param>
        /// <returns>Returns True if was successfully queued. False if it fails.</returns>
        bool Send(string message);

        /// <summary>
        /// Queue a Message to Send to the server as a String.
        /// </summary>
        /// <param name="message">The Message To Queue</param>
        /// <returns>Returns True if was successfully queued. False if it fails.</returns>
        bool SendMessage(string message);

        /// <summary>
        /// Queue a Whisper to Send to the server as a String.
        /// </summary>
        /// <param name="message">The Whisper To Queue</param>
        /// <returns>Returns True if was successfully queued. False if it fails.</returns>
        bool SendWhisper(string message);

        void MessageThrottled(OnMessageThrottledEventArgs eventArgs);
        void WhisperThrottled(OnWhisperThrottledEventArgs eventArgs);
        void SendFailed(OnSendFailedEventArgs eventArgs);
        void Error(OnErrorEventArgs eventArgs);

        /// <summary>
        /// Dispose the Client. Forces the Send Queue to be destroyed, resulting in Message Loss.
        /// </summary>
        void Dispose();
    }
}