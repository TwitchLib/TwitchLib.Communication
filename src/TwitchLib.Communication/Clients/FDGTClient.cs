using System;
using System.Collections.Generic;
using System.Text;
using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Clients
{
	public class FDGTClient : WebSocketClient, IClient
	{
		public FDGTClient(IClientOptions options = null) : base(options)
		{
			Url = "wss://irc.fdgt.dev:443";
		}
	}
}
