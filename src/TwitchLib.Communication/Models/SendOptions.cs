using System;

using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Models
{

    public class SendOptions : ISendOptions
    {
        public uint MessagesAllowedInPeriod { get; }
        public uint SendQueueCapacity { get; }
        public TimeSpan SendCacheItemTimeout { get; }
        public SendOptions(uint messagesAllowedInPeriod,
                           uint sendQueueCapacity = 10_000,
                           uint sendCacheItemTimeoutInMinutes = 30)
        {
            MessagesAllowedInPeriod = messagesAllowedInPeriod;
            SendQueueCapacity = sendQueueCapacity;
            SendCacheItemTimeout = TimeSpan.FromMinutes(sendCacheItemTimeoutInMinutes);
        }
    }
}