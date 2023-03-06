using System;

namespace TwitchLib.Communication.Events
{

    public class OnWhisperThrottledEventArgs : EventArgs, IOnThrottledEventArgs
    {
        private IOnThrottledEventArgs OnThrottledEventArgs { get; }

        public string Reason => OnThrottledEventArgs.Reason;

        public string ItemNotSent => OnThrottledEventArgs.ItemNotSent;

        public long SentCount => OnThrottledEventArgs.SentCount;

        public TimeSpan Period => OnThrottledEventArgs.Period;

        public uint AllowedInPeriod => OnThrottledEventArgs.AllowedInPeriod;

        internal OnWhisperThrottledEventArgs(IOnThrottledEventArgs onThrottledEventArgs)
        {
            OnThrottledEventArgs = onThrottledEventArgs;
        }
    }
}
