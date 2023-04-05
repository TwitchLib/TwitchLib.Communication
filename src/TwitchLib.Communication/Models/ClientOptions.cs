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
        public ClientOptions(ReconnectionPolicy? reconnectionPolicy = null,
                             bool useSsl = true,
                             uint disconnectWait = 1_500,
                             ClientType clientType = ClientType.Chat)
        {

            ReconnectionPolicy = reconnectionPolicy ?? new ReconnectionPolicy(3_000, maxAttempts: 10);
            UseSsl = useSsl;
            DisconnectWait = disconnectWait;
            ClientType = clientType;
        }
    }
}
