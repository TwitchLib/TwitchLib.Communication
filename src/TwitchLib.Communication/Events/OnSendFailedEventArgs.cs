using System;

namespace TwitchLib.Communication.Events
{
    public class SendFailedEventArgs : EventArgs
    {
        public string Data { get; }
        public Exception Exception { get; }
        public SendFailedEventArgs(string data, Exception exception)
        {
            Data = data;
            Exception = exception;
        }
    }
}
