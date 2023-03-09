using System;
using System.Diagnostics.CodeAnalysis;
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
        [SuppressMessage("Style", "IDE0058")]
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            // Suppress IDE0058 - we dont daisy-chain here
            builder.AppendLine($"{nameof(Reason)}: {Reason}");
            builder.AppendLine($"{nameof(ItemNotSent)}: {ItemNotSent}");
            builder.AppendLine($"{nameof(SentCount)}: {SentCount}");
            builder.AppendLine($"{nameof(Period)}: {Period}");
            builder.AppendLine($"{nameof(AllowedInPeriod)}: {AllowedInPeriod}");
            return builder.ToString();
        }
    }
}
