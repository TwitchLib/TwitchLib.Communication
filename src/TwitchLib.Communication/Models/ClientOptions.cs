using System;
using TwitchLib.Communication.Enums;
using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Models
{
    public class ClientOptions : IClientOptions
    {
        public ClientType ClientType { get; set; } = ClientType.Chat;
        public TimeSpan MessageThrottlingPeriod { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan WhisperThrottlingPeriod { get; set; } = TimeSpan.FromMinutes(60);
        public TimeSpan SendCacheItemTimeout { get; set; } = TimeSpan.FromMinutes(30);
        public ReconnectionPolicy ReconnectionPolicy { get; set; } = new ReconnectionPolicy();
        public ushort SendDelay { get; set; } = 50;
        public int MessagesAllowedInPeriod { get; set; } = 100;
        public int WhispersAllowedInPeriod { get; set; } = 100;
        public bool UseSsl { get; set; } = true;
        public int MessageQueueCapacity { get; set; } = 10000;
        public int WhisperQueueCapacity { get; set; } = 10000;
    }
}