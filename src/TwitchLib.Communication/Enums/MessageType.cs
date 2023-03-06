namespace TwitchLib.Communication.Enums
{
    internal enum MessageType
    {
        /// <summary>
        ///     should only be used for "PING"-/"PONG"-'Messages'
        /// </summary>
        ByPass,
        /// <summary>
        ///     indicates a normal Message
        /// </summary>
        Message,
        /// <summary>
        ///     indicates a Whisper
        /// </summary>
        Whisper
    }
}