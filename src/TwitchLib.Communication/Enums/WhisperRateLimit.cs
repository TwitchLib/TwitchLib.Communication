namespace TwitchLib.Communication.Enums
{
    /// <summary>
    ///     <see href="https://discuss.dev.twitch.tv/t/whisper-rate-limiting/2836"/>
    ///     <br></br>
    ///     see also <see cref="IClientOptions.ThrottlingPeriod"/>:
    ///     <br></br>
    ///     <inheritdoc cref="IClientOptions.ThrottlingPeriod"/>
    /// </summary>
    public enum WhisperRateLimit : uint
    {
        // to have only one Reset-Timer running that fires each 30 seconds,
        // this value is set to 50
        // please take a look at this enums summary
        Limit_100_in_60_Seconds = 50
    }
}
