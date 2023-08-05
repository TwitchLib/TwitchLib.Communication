using System;
using TwitchLib.Communication.Models;
using Xunit;

namespace TwitchLib.Communication.Tests.Models;

public class ReconnectionPolicyTests
{
    /// <summary>
    ///     Checks <see cref="ClientOptions.ReconnectionPolicy"/>
    ///     <br></br>
    ///     <see cref="ReconnectionPolicy.AreAttemptsComplete"/>
    ///     <br></br>
    ///     <see cref="ReconnectionPolicy.Reset(Boolean)"/>
    /// </summary>
    [Fact]
    public void ReconnectionPolicy_OmitReconnect()
    {
        try
        {
            ReconnectionPolicy reconnectionPolicy = new NoReconnectionPolicy();
            Assert.False(reconnectionPolicy.AreAttemptsComplete());
            reconnectionPolicy.ProcessValues();
            Assert.True(reconnectionPolicy.AreAttemptsComplete());
            // in case of a normal connect, we expect the ReconnectionPolicy to be reset
            reconnectionPolicy.Reset(false);
            Assert.False(reconnectionPolicy.AreAttemptsComplete());
            reconnectionPolicy.ProcessValues();
            Assert.True(reconnectionPolicy.AreAttemptsComplete());
            // in case of a reconnect, we expect the ReconnectionPolicy not to be reset
            reconnectionPolicy.Reset(true);
            Assert.True(reconnectionPolicy.AreAttemptsComplete());
        }
        catch (Exception e)
        {
            Assert.Fail(e.ToString());
        }
    }
}
