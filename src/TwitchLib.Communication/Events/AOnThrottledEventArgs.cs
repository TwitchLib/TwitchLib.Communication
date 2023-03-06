using System;

namespace TwitchLib.Communication.Events
{

    public abstract class AOnThrottledEventArgs : EventArgs
    {
        public string Reason { get; set; }
        /// <summary>
        ///     the Message or the Whisper that has been throttled
        /// </summary>
        public string ItemNotSent { get; set; }
        public long SentCount { get; set; }
        public TimeSpan Period { get; set; }
        public uint AllowedInPeriod { get; set; }
    }
}
