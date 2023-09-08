using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace TwitchLib.Communication.Tests.Clients;

public class TcpClientTests : ClientTestsBase<TcpClient>
{
    public TcpClientTests() : base(new ClientOptions(useSsl: false)) { }
}