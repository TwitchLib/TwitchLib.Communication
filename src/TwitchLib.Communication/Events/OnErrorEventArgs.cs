using System;

namespace TwitchLib.Communication.Events
{
    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public ErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}
