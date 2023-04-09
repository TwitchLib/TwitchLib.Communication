using System;

namespace TwitchLib.Communication.Events
{
    public class StateChangedEventArgs : EventArgs
    {
        public bool IsConnected;
        public bool WasConnected;
    }
}
