using System;

namespace TwitchLib.Communication.Events
{

    public class OnMessageThrottledEventArgs : EventArgs, IOnThrottledEventArgs
    {
        private IOnThrottledEventArgs OnThrottledEventArgs { get; }

        public string Reason => OnThrottledEventArgs.Reason;

        public string ItemNotSent => OnThrottledEventArgs.ItemNotSent;

        public long SentCount => OnThrottledEventArgs.SentCount;

        public TimeSpan Period => OnThrottledEventArgs.Period;

        public uint AllowedInPeriod => OnThrottledEventArgs.AllowedInPeriod;

        internal OnMessageThrottledEventArgs(IOnThrottledEventArgs onThrottledEventArgs)
        {
            OnThrottledEventArgs = onThrottledEventArgs;
        }
        public override string ToString()
        {
            return OnThrottledEventArgs.ToString();
        }
    }
}
