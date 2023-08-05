namespace TwitchLib.Communication.Events;

public class OnSendFailedEventArgs : EventArgs
{
    public string Message { get; }

    public Exception Exception { get; }

    public OnSendFailedEventArgs(Exception exception, string message)
    {
        Exception = exception;
        Message = message;
    }
}
