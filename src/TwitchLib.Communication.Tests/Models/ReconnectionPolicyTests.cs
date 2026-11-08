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

    /// <summary>
    ///     Checks that <see cref="ReconnectionPolicy.ConnectionEstablished"/>
    ///     gives every connection loss all of its attempts again
    /// </summary>
    [Fact]
    public void ReconnectionPolicy_Attempts_Start_Over_After_A_Connection_Is_Established()
    {
        var reconnectionPolicy = new ReconnectionPolicy(100, maxAttempts: 2);
        reconnectionPolicy.Reset(false);
        reconnectionPolicy.ProcessValues();
        reconnectionPolicy.ConnectionEstablished();

        for (var loss = 0; loss < 5; loss++)
        {
            reconnectionPolicy.Reset(true);
            Assert.False(reconnectionPolicy.AreAttemptsComplete());
            reconnectionPolicy.ProcessValues();
            reconnectionPolicy.ConnectionEstablished();
        }
    }

    /// <summary>
    ///     Checks that <see cref="NoReconnectionPolicy"/> still never reconnects
    ///     after <see cref="ReconnectionPolicy.ConnectionEstablished"/>
    /// </summary>
    [Fact]
    public void NoReconnectionPolicy_Does_Not_Reconnect_After_A_Connection_Is_Established()
    {
        ReconnectionPolicy reconnectionPolicy = new NoReconnectionPolicy();
        reconnectionPolicy.Reset(false);
        reconnectionPolicy.ProcessValues();
        reconnectionPolicy.ConnectionEstablished();

        reconnectionPolicy.Reset(true);
        Assert.True(reconnectionPolicy.AreAttemptsComplete());
    }
}
