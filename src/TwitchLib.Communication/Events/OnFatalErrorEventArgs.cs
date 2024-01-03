namespace TwitchLib.Communication.Events;

public class OnFatalErrorEventArgs : EventArgs
{
    public string Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OnFatalErrorEventArgs"/>.
    /// </summary>
    public OnFatalErrorEventArgs(string reason)
    {
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OnFatalErrorEventArgs"/>.
    /// </summary>
    public OnFatalErrorEventArgs(Exception e)
    {
        Reason = e.ToString();
    }
}