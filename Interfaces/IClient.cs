using System;
using TwitchLib.Communication.Events;

namespace TwitchLib.Communication
{
    public interface IClient
    {
        /// <inheritdoc />
        TimeSpan DefaultKeepAliveInterval { get; set; }
        /// <inheritdoc />
        int SendQueueLength { get; }
        /// <inheritdoc />
        int WhisperQueueLength { get; }
        /// <inheritdoc />
        bool IsConnected { get; }
        /// <inheritdoc />
        event EventHandler<OnConnectedEventArgs> OnConnected;
        /// <inheritdoc />
        event EventHandler<OnDataEventArgs> OnData;
        /// <inheritdoc />
        event EventHandler<OnDisconnectedEventArgs> OnDisconnected;
        /// <inheritdoc />
        event EventHandler<OnErrorEventArgs> OnError;
        /// <inheritdoc />
        event EventHandler<OnFatalErrorEventArgs> OnFatality;
        /// <inheritdoc />
        event EventHandler<OnMessageEventArgs> OnMessage;
        /// <inheritdoc />
        event EventHandler<OnMessageThrottledEventArgs> OnMessageThrottled;
        /// <inheritdoc />
        event EventHandler<OnWhisperThrottledEventArgs> OnWhisperThrottled;
        /// <inheritdoc />
        event EventHandler<OnSendFailedEventArgs> OnSendFailed;
        /// <inheritdoc />
        event EventHandler<OnStateChangedEventArgs> OnStateChanged;
          /// <inheritdoc />
        void Close();
        /// <inheritdoc />
        void Dispose();
        /// <inheritdoc />
        void Dispose(bool waitForSendsToComplete);
        /// <inheritdoc />
        bool Open();
        /// <inheritdoc />
        bool Send(string data);
        /// <inheritdoc />
        bool SendWhisper(string data);
    }
}