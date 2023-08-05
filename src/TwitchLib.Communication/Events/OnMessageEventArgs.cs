namespace TwitchLib.Communication.Events;

public class OnMessageEventArgs : EventArgs
{
    public string Message { get; }

    public OnMessageEventArgs(string message)
    {
        Message = message;
    }
}
