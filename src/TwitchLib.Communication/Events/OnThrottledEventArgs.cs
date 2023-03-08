using System;
using System.Text;

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
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"{nameof(Reason)}: {Reason}");
            builder.AppendLine($"{nameof(ItemNotSent)}: {ItemNotSent}");
            builder.AppendLine($"{nameof(SentCount)}: {SentCount}");
            builder.AppendLine($"{nameof(Period)}: {Period}");
            builder.AppendLine($"{nameof(AllowedInPeriod)}: {AllowedInPeriod}");
            return builder.ToString();
        }
    }
}
