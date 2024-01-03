namespace TwitchLib.Communication.Events;

public class OnSendFailedEventArgs : EventArgs
{
    public string Message { get; }

    public Exception Exception { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OnSendFailedEventArgs"/>.
    /// </summary>
    public OnSendFailedEventArgs(Exception exception, string message)
    {
        Exception = exception;
        Message = message;
    }
}
