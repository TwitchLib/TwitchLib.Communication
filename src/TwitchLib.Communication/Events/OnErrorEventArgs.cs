using System;

namespace TwitchLib.Communication.Events
{
    public class OnErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }

        public OnErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}
