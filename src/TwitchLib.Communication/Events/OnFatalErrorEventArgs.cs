using System;

namespace TwitchLib.Communication.Events
{
    public class OnFatalErrorEventArgs : EventArgs
    {
        public string Reason { get; }

        public OnFatalErrorEventArgs(string reason)
        {
            Reason = reason;
        }

        public OnFatalErrorEventArgs(Exception e)
        {
            Reason = e.ToString();
        }
    }
}