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
        /// <summary>
        /// </summary>
        /// <param name="reconnectionPolicy">
        ///     your own <see cref="ReconnectionPolicy"/>
        ///     <br></br>
        ///     by leaving it <see langword="null"/>, a <see langword="default"/> <see cref="ReconnectionPolicy"/>, that makes every 3_000ms one attempt to connect for ten times, is going to be applied
        /// </param>
        /// <param name="useSsl">
        ///     <inheritdoc cref="UseSsl"/>
        /// </param>
        /// <param name="disconnectWait">
        ///     <inheritdoc cref="DisconnectWait"/>
        /// </param>
        /// <param name="clientType">
        ///     <inheritdoc cref="ClientType"/>
        /// </param>
        /// <param name="sendDelay">
        ///     <inheritdoc cref="SendDelay"/>
        /// </param>
        /// <param name="messageSendOptions">
        ///     by leaving it <see langword="null"/>, <see langword="default"/> <see cref="SendOptions"/> with the minimum <see cref="MessageRateLimit.Limit_20_in_30_Seconds"/> is going to be applied
        /// </param>
        public ClientOptions(ReconnectionPolicy reconnectionPolicy = null,
                             bool useSsl = true,
                             uint disconnectWait = 1_500,
                             ClientType clientType = ClientType.Chat,
                             ushort sendDelay = 50,
                             ISendOptions messageSendOptions = null)
        {

            ReconnectionPolicy = reconnectionPolicy ?? new ReconnectionPolicy(3_000, maxAttempts: 10);
            UseSsl = useSsl;
            DisconnectWait = disconnectWait;
            ClientType = clientType;
            SendDelay = sendDelay;
            MessageSendOptions = messageSendOptions ?? new SendOptions((uint) MessageRateLimit.Limit_20_in_30_Seconds);
        }
    }
}
