using System;

namespace TwitchLib.Communication.Events
{
    /// <summary>
    ///     used for <see cref="OnMessageThrottledEventArgs"/> and <see cref="OnWhisperThrottledEventArgs"/>
    /// </summary>
    internal class OnThrottledEventArgs : IOnThrottledEventArgs
    {
        public string Reason { get; set; }
        public string ItemNotSent { get; set; }
        public long SentCount { get; set; }
        public TimeSpan Period { get; set; }
        public uint AllowedInPeriod { get; set; }
    }
}
