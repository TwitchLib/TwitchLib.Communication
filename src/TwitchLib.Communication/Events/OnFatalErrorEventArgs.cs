using System;

namespace TwitchLib.Communication.Events
{

    public class FatalErrorEventArgs : EventArgs
    {
        public string Reason { get; }
        public Exception Exception { get; }
        public FatalErrorEventArgs(string reason)
        {
            Reason = reason;
        }

        public FatalErrorEventArgs(Exception e)
        {
            Reason = e.ToString();
            Exception = e;
        }
    }
}
