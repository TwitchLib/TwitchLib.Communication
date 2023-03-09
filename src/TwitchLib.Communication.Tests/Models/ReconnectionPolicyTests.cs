using System;
using System.Diagnostics.CodeAnalysis;

using TwitchLib.Communication.Models;

using Xunit;

namespace TwitchLib.Communication.Tests.Models
{
    public class ReconnectionPolicyTests
    {
        /// <summary>
        ///     checks <see cref="ClientOptions.ReconnectionPolicy"/>
        ///     <br></br>
        ///     <see cref="ReconnectionPolicy.ReconnectionPolicy(Boolean)"/> = <see langword="false"/>
        /// </summary>
        [Fact]
        [SuppressMessage("Style", "CA1806")]
        [SuppressMessage("Style", "IDE0058")]
        public void ReconnectionPolicy_Throws_ArgumentOutOfRangeException_YES()
        {
            try
            {
                // Suppress CA1806 and IDE0058
                new ReconnectionPolicy(false);
                Assert.Fail($"{nameof(ArgumentOutOfRangeException)} expected.");
            }
            catch (Exception e)
            {
                Assert.NotNull(e);
                Assert.IsType<ArgumentOutOfRangeException>(e);
            }
        }
        /// <summary>
        ///     checks <see cref="ClientOptions.ReconnectionPolicy"/>
        ///     <br></br>
        ///     <see cref="ReconnectionPolicy.ReconnectionPolicy(Boolean)"/> = <see langword="true"/>
        /// </summary>
        [Fact]
        public void ReconnectionPolicy_Throws_ArgumentOutOfRangeException_NO()
        {
            try
            {
                ReconnectionPolicy reconnectionPolicy = new ReconnectionPolicy(true);
                Assert.True(reconnectionPolicy.OmitReconnect);
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }
        }
        /// <summary>
        ///     checks <see cref="ClientOptions.ReconnectionPolicy"/>
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
                ReconnectionPolicy reconnectionPolicy = new ReconnectionPolicy(true);
                Assert.True(reconnectionPolicy.OmitReconnect);
                Assert.False(reconnectionPolicy.AreAttemptsComplete());
                reconnectionPolicy.ProcessValues();
                Assert.True(reconnectionPolicy.AreAttemptsComplete());
                reconnectionPolicy.Reset(false);
                Assert.False(reconnectionPolicy.AreAttemptsComplete());
                reconnectionPolicy.ProcessValues();
                Assert.True(reconnectionPolicy.AreAttemptsComplete());
                reconnectionPolicy.Reset(true);
                Assert.True(reconnectionPolicy.AreAttemptsComplete());
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }
        }
    }
}
