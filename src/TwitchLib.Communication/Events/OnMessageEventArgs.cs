using System;

namespace TwitchLib.Communication.Events
{
    public class MessageEventArgs : EventArgs
    {
        public string Message { get; }
        public MessageEventArgs(string message)
        {
            Message = message;
        }
    }
}
