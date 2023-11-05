namespace TwitchLib.Communication.Events;

public class OnMessageEventArgs : EventArgs
{
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OnMessageEventArgs"/>.
    /// </summary>
    public OnMessageEventArgs(string message)
    {
        Message = message;
    }
}
