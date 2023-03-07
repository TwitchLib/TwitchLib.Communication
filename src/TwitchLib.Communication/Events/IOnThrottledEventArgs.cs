using System;

namespace TwitchLib.Communication.Events
{

    public interface IOnThrottledEventArgs
    {
        string Reason { get; }
        /// <summary>
        ///     the Message that has been throttled
        /// </summary>
        string ItemNotSent { get; }
        long SentCount { get; }
        TimeSpan Period { get; }
        uint AllowedInPeriod { get; }
    }
}
