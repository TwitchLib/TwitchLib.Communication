using System;

namespace TwitchLib.WebSocket.Events
{
    public class OnWhisperThrottledEventArgs : EventArgs
    {
        public string Message { get; set; }
        public int SentWhisperCount { get; set; }
        public TimeSpan Period { get; set; }
        public int AllowedInPeriod { get; set; }
    }
}
