namespace TwitchLib.Communication.Events;

public class OnErrorEventArgs : EventArgs
{
    public Exception Exception { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OnErrorEventArgs"/>.
    /// </summary>
    public OnErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }
}
