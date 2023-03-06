using System;

using TwitchLib.Communication.Enums;
using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Models
{

    public class ClientOptions : IClientOptions
    {
        public ReconnectionPolicy ReconnectionPolicy { get; }
        public bool UseSsl { get; }
        public uint DisconnectWait { get; }
        public ClientType ClientType { get; }
        public ushort SendDelay { get; }
        public TimeSpan ThrottlingPeriod { get; } = TimeSpan.FromSeconds(30);
        public ISendOptions MessageSendOptions { get; }
        public ISendOptions WhisperSendOptions { get; }
        public ClientOptions(ReconnectionPolicy reconnectionPolicy = null,
                             bool useSsl = true,
                             uint disconnectWait = 1_500,
                             ClientType clientType = ClientType.Chat,
                             ushort sendDelay = 50,
                             ISendOptions messageSendOptions = null,
                             ISendOptions whisperSendOptions = null)
        {

            ReconnectionPolicy = reconnectionPolicy ?? new ReconnectionPolicy(3_000, maxAttempts: 10);
            UseSsl = useSsl;
            DisconnectWait = disconnectWait;
            ClientType = clientType;
            SendDelay = sendDelay;
            MessageSendOptions = messageSendOptions ?? new SendOptions((uint) MessageRateLimit.Limit_20_in_30_Seconds);
            WhisperSendOptions = whisperSendOptions ?? new SendOptions((uint) WhisperRateLimit.Limit_100_in_60_Seconds);
        }
    }
}
