namespace TwitchLib.Communication.Events;

public class OnSendFailedEventArgs : EventArgs
{
    public string Data { get; }

    public Exception Exception { get; }

    public OnSendFailedEventArgs(Exception exception, string data)
    {
        Exception = exception;
        Data = data;
    }
}
