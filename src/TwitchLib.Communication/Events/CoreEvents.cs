using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLib.Communication.Events
{
    public class CoreEvents
    {
        /*
    * Custom implementation of asynchronous event handler
    * This is useful to properly and safely handle async Tasks 
    * Reference: https://medium.com/@a.lyskawa/the-hitchhiker-guide-to-asynchronous-events-in-c-e9840109fb53
    */
    public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);
    }
}
