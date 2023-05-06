using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Tests.Helpers;
using Xunit;
using static TwitchLib.Communication.Events.CoreEvents;
using Xunit.Sdk;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace TwitchLib.Communication.Tests.Clients
{
    /// <summary>
    ///     bundles <see cref="IClient"/>-Tests in one container
    /// </summary>
    /// <typeparam name="T">
    ///     <list type="bullet">
    ///         <see cref="Clients.TcpClient"/>
    ///     </list>
    ///     <list type="bullet">
    ///         <see cref="Clients.WebSocketClient"/>
    ///     </list>
    /// </typeparam>
    public abstract class ClientTestsBase<T> where T : IClient
    {
        private static TimeSpan WaitOneDuration => TimeSpan.FromSeconds(5);
        private readonly IClientOptions? _options;

        protected ClientTestsBase(IClientOptions? options = null)
        {
            _options = options;
        }
        
        [Fact]
        public async Task Client_Raises_OnConnected_EventArgs()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            var logger = TestLogHelper.GetLogger<T>();
            var client = GetClient(logger, _options);
            Assert.NotNull(client);
            try
            {
                var pauseConnected = new ManualResetEvent(false);

                await Assert.RaisesAsync<OnConnectedEventArgs>(
                    h => client.OnConnected += h,
                    h => client.OnConnected -= h,
                    async () =>
                    {
                        client.OnConnected += async(sender, e) =>
                        {
                            pauseConnected.Set();
                        };

                        await client.OpenAsync();
                        Assert.True(pauseConnected.WaitOne(WaitOneDuration));
                    });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public async Task Client_Raises_OnDisconnected_EventArgs()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            var logger = TestLogHelper.GetLogger<T>();
            var client = GetClient(logger, _options);
            Assert.NotNull(client);
            try
            {
                var pauseDisconnected = new ManualResetEvent(false);

                await Assert.RaisesAsync<OnDisconnectedEventArgs>(
                    h => client.OnDisconnected += h,
                    h => client.OnDisconnected -= h,
                    async () =>
                    {
                        client.OnConnected += async (sender, e) =>
                        {
                            await client.CloseAsync();
                        };

                        client.OnDisconnected += async (sender, e) =>
                        {
                            pauseDisconnected.Set();
                        };
                        await client.OpenAsync();

                        Assert.True(pauseDisconnected.WaitOne(WaitOneDuration));
                    });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public async Task Client_Raises_OnReconnected_EventArgs()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            var logger = TestLogHelper.GetLogger<T>();
            var client = GetClient(logger, _options);
            Assert.NotNull(client);
            try
            {
                var pauseReconnected = new ManualResetEvent(false);

                await Assert.RaisesAsync<OnConnectedEventArgs>(
                    h => client.OnReconnected += h,
                    h => client.OnReconnected -= h,
                    async () =>
                    {
                        client.OnConnected += async (s, e) => await client.ReconnectAsync();

                        client.OnReconnected += async (s, e) =>
                        {
                            pauseReconnected.Set();
                        };
                        await client.OpenAsync();
                        Assert.True(pauseReconnected.WaitOne(WaitOneDuration));
                    });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public void Dispose_Client_Before_Connecting_IsOK()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            var logger = TestLogHelper.GetLogger<T>();
            IClient? client = null;
            try
            {
                client = GetClient(logger, _options);
                Assert.NotNull(client);
                client.Dispose();
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                client?.Dispose();
            }
        }

        private static TClient? GetClient<TClient>(ILogger<TClient> logger, IClientOptions? options = null)
        {
            var constructorParameterTypes = new Type[]
            {
                typeof(IClientOptions),
                typeof(ILogger<TClient>)
            };
            
            var constructor = typeof(TClient).GetConstructor(constructorParameterTypes);
            var constructorParameters = new object[]
            {
                options ?? new ClientOptions(),
                logger
            };
            
            return (TClient?)constructor?.Invoke(constructorParameters);
        }
    }


    #region Modified Assert
    //TL;DR: Extracted version of XUNIT with
    //modification to accept new event Handler

    public partial class Assert
    {

        /// <summary>
        /// Verifies that a event with the exact event args (and not a derived type) is raised.
        /// </summary>
        /// <typeparam name="T">The type of the event arguments to expect</typeparam>
        /// <param name="attach">Code to attach the event handler</param>
        /// <param name="detach">Code to detach the event handler</param>
        /// <param name="testCode">A delegate to the code to be tested</param>
        /// <returns>The event sender and arguments wrapped in an object</returns>
        /// <exception cref="RaisesException">Thrown when the expected event was not raised.</exception>
        public static async Task<RaisedEvent<T>> RaisesAsync<T>(Action<AsyncEventHandler<T>> attach, Action<AsyncEventHandler<T>> detach, Func<Task> testCode)
        {
            var raisedEvent = await RaisesAsyncInternal(attach, detach, testCode);

            if (raisedEvent == null)
                throw new RaisesException(typeof(T));

            if (raisedEvent.Arguments != null && !raisedEvent.Arguments.GetType().Equals(typeof(T)))
                throw new RaisesException(typeof(T), raisedEvent.Arguments.GetType());

            return raisedEvent;
        }

        /// <summary>
        /// Verifies that an event with the exact or a derived event args is raised.
        /// </summary>
        /// <typeparam name="T">The type of the event arguments to expect</typeparam>
        /// <param name="attach">Code to attach the event handler</param>
        /// <param name="detach">Code to detach the event handler</param>
        /// <param name="testCode">A delegate to the code to be tested</param>
        /// <returns>The event sender and arguments wrapped in an object</returns>
        /// <exception cref="RaisesException">Thrown when the expected event was not raised.</exception>
        public static async Task<RaisedEvent<T>> RaisesAnyAsync<T>(Action<AsyncEventHandler<T>> attach, Action<AsyncEventHandler<T>> detach, Func<Task> testCode)
        {
            var raisedEvent = await RaisesAsyncInternal(attach, detach, testCode);

            if (raisedEvent == null)
                throw new RaisesException(typeof(T));

            return raisedEvent;
        }

#if XUNIT_NULLABLE
		static async Task<RaisedEvent<T>?> RaisesAsyncInternal<T>(Action<EventHandler<T>> attach, Action<EventHandler<T>> detach, Func<Task> testCode)
#else
        static async Task<RaisedEvent<T>> RaisesAsyncInternal<T>(Action<AsyncEventHandler<T>> attach, Action<AsyncEventHandler<T>> detach, Func<Task> testCode)
#endif
        {
            NotNull(attach);
            NotNull(detach);
            NotNull(testCode);

#if XUNIT_NULLABLE
			RaisedEvent<T>? raisedEvent = null;
			void handler(object? s, T args) => raisedEvent = new RaisedEvent<T>(s, args);
#else
            RaisedEvent<T> raisedEvent = null;
            AsyncEventHandler<T> value = (object s, T args) =>
            {
                raisedEvent = new RaisedEvent<T>(s, args);
                return Task.CompletedTask;
            };
            AsyncEventHandler<T> handler = value;
#endif
            attach(handler);
            await testCode();
            detach(handler);
            return raisedEvent;
        }

        /// <summary>
        /// Represents a raised event after the fact.
        /// </summary>
        /// <typeparam name="T">The type of the event arguments.</typeparam>
        public class RaisedEvent<T>
        {
            /// <summary>
            /// The sender of the event.
            /// </summary>
#if XUNIT_NULLABLE
			public object? Sender { get; }
#else
            public object Sender { get; }
#endif

            /// <summary>
            /// The event arguments.
            /// </summary>
            public T Arguments { get; }

            /// <summary>
            /// Creates a new instance of the <see cref="RaisedEvent{T}" /> class.
            /// </summary>
            /// <param name="sender">The sender of the event.</param>
            /// <param name="args">The event arguments</param>
#if XUNIT_NULLABLE
			public RaisedEvent(object? sender, T args)
#else
            public RaisedEvent(object sender, T args)
#endif
            {
                Sender = sender;
                Arguments = args;
            }
        }


#if XUNIT_NULLABLE
		public static void False([DoesNotReturnIf(parameterValue: true)] bool condition)
#else
        public static void False(bool condition)
#endif
        {
            False((bool?)condition, null);
        }

        /// <summary>
        /// Verifies that the condition is false.
        /// </summary>
        /// <param name="condition">The condition to be tested</param>
        /// <exception cref="FalseException">Thrown if the condition is not false</exception>
#if XUNIT_NULLABLE
		public static void False([DoesNotReturnIf(parameterValue: true)] bool? condition)
#else
        public static void False(bool? condition)
#endif
        {
            False(condition, null);
        }

        /// <summary>
        /// Verifies that the condition is false.
        /// </summary>
        /// <param name="condition">The condition to be tested</param>
        /// <param name="userMessage">The message to show when the condition is not false</param>
        /// <exception cref="FalseException">Thrown if the condition is not false</exception>
#if XUNIT_NULLABLE
		public static void False([DoesNotReturnIf(parameterValue: true)] bool condition, string? userMessage)
#else
        public static void False(bool condition, string userMessage)
#endif
        {
            False((bool?)condition, userMessage);
        }

        /// <summary>
        /// Verifies that the condition is false.
        /// </summary>
        /// <param name="condition">The condition to be tested</param>
        /// <param name="userMessage">The message to show when the condition is not false</param>
        /// <exception cref="FalseException">Thrown if the condition is not false</exception>
#if XUNIT_NULLABLE
		public static void False([DoesNotReturnIf(parameterValue: true)] bool? condition, string? userMessage)
#else
        public static void False(bool? condition, string userMessage)
#endif
        {
            if (!condition.HasValue || condition.GetValueOrDefault())
                throw new FalseException(userMessage, condition);
        }

        /// <summary>
        /// Verifies that an expression is true.
        /// </summary>
        /// <param name="condition">The condition to be inspected</param>
        /// <exception cref="TrueException">Thrown when the condition is false</exception>
#if XUNIT_NULLABLE
		public static void True([DoesNotReturnIf(parameterValue: false)] bool condition)
#else
        public static void True(bool condition)
#endif
        {
            True((bool?)condition, null);
        }

        /// <summary>
        /// Verifies that an expression is true.
        /// </summary>
        /// <param name="condition">The condition to be inspected</param>
        /// <exception cref="TrueException">Thrown when the condition is false</exception>
#if XUNIT_NULLABLE
		public static void True([DoesNotReturnIf(parameterValue: false)] bool? condition)
#else
        public static void True(bool? condition)
#endif
        {
            True(condition, null);
        }

        /// <summary>
        /// Verifies that an expression is true.
        /// </summary>
        /// <param name="condition">The condition to be inspected</param>
        /// <param name="userMessage">The message to be shown when the condition is false</param>
        /// <exception cref="TrueException">Thrown when the condition is false</exception>
#if XUNIT_NULLABLE
		public static void True([DoesNotReturnIf(parameterValue: false)] bool condition, string? userMessage)
#else
        public static void True(bool condition, string userMessage)
#endif
        {
            True((bool?)condition, userMessage);
        }

        /// <summary>
        /// Verifies that an expression is true.
        /// </summary>
        /// <param name="condition">The condition to be inspected</param>
        /// <param name="userMessage">The message to be shown when the condition is false</param>
        /// <exception cref="TrueException">Thrown when the condition is false</exception>
#if XUNIT_NULLABLE
		public static void True([DoesNotReturnIf(parameterValue: false)] bool? condition, string? userMessage)
#else
        public static void True(bool? condition, string userMessage)
#endif
        {
            if (!condition.HasValue || !condition.GetValueOrDefault())
                throw new TrueException(userMessage, condition);
        }

        /// <summary>
        /// Verifies that a string contains a given sub-string, using the current culture.
        /// </summary>
        /// <param name="expectedSubstring">The sub-string expected to be in the string</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <exception cref="ContainsException">Thrown when the sub-string is not present inside the string</exception>
#if XUNIT_NULLABLE
		public static void Contains(string expectedSubstring, string? actualString)
#else
        public static void Contains(string expectedSubstring, string actualString)
#endif
        {
            Contains(expectedSubstring, actualString, StringComparison.CurrentCulture);
        }

        /// <summary>
        /// Verifies that a string contains a given sub-string, using the given comparison type.
        /// </summary>
        /// <param name="expectedSubstring">The sub-string expected to be in the string</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <param name="comparisonType">The type of string comparison to perform</param>
        /// <exception cref="ContainsException">Thrown when the sub-string is not present inside the string</exception>
#if XUNIT_NULLABLE
		public static void Contains(string expectedSubstring, string? actualString, StringComparison comparisonType)
#else
        public static void Contains(string expectedSubstring, string actualString, StringComparison comparisonType)
#endif
        {
            NotNull(expectedSubstring);

            if (actualString == null || actualString.IndexOf(expectedSubstring, comparisonType) < 0)
                throw new ContainsException(expectedSubstring, actualString);
        }

        /// <summary>
        /// Verifies that a string does not contain a given sub-string, using the current culture.
        /// </summary>
        /// <param name="expectedSubstring">The sub-string which is expected not to be in the string</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <exception cref="DoesNotContainException">Thrown when the sub-string is present inside the string</exception>
#if XUNIT_NULLABLE
		public static void DoesNotContain(string expectedSubstring, string? actualString)
#else
        public static void DoesNotContain(string expectedSubstring, string actualString)
#endif
        {
            DoesNotContain(expectedSubstring, actualString, StringComparison.CurrentCulture);
        }

        /// <summary>
        /// Verifies that a string does not contain a given sub-string, using the current culture.
        /// </summary>
        /// <param name="expectedSubstring">The sub-string which is expected not to be in the string</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <param name="comparisonType">The type of string comparison to perform</param>
        /// <exception cref="DoesNotContainException">Thrown when the sub-string is present inside the given string</exception>
#if XUNIT_NULLABLE
		public static void DoesNotContain(string expectedSubstring, string? actualString, StringComparison comparisonType)
#else
        public static void DoesNotContain(string expectedSubstring, string actualString, StringComparison comparisonType)
#endif
        {
            NotNull(expectedSubstring);

            if (actualString != null && actualString.IndexOf(expectedSubstring, comparisonType) >= 0)
                throw new DoesNotContainException(expectedSubstring, actualString);
        }

        /// <summary>
        /// Verifies that a string starts with a given string, using the current culture.
        /// </summary>
        /// <param name="expectedStartString">The string expected to be at the start of the string</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <exception cref="ContainsException">Thrown when the string does not start with the expected string</exception>
#if XUNIT_NULLABLE
		public static void StartsWith(string? expectedStartString, string? actualString)
#else
        public static void StartsWith(string expectedStartString, string actualString)
#endif
        {
            StartsWith(expectedStartString, actualString, StringComparison.CurrentCulture);
        }

        /// <summary>
        /// Verifies that a string starts with a given string, using the given comparison type.
        /// </summary>
        /// <param name="expectedStartString">The string expected to be at the start of the string</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <param name="comparisonType">The type of string comparison to perform</param>
        /// <exception cref="ContainsException">Thrown when the string does not start with the expected string</exception>
#if XUNIT_NULLABLE
		public static void StartsWith(string? expectedStartString, string? actualString, StringComparison comparisonType)
#else
        public static void StartsWith(string expectedStartString, string actualString, StringComparison comparisonType)
#endif
        {
            if (expectedStartString == null || actualString == null || !actualString.StartsWith(expectedStartString, comparisonType))
                throw new StartsWithException(expectedStartString, actualString);
        }

        /// <summary>
        /// Verifies that a string ends with a given string, using the current culture.
        /// </summary>
        /// <param name="expectedEndString">The string expected to be at the end of the string</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <exception cref="ContainsException">Thrown when the string does not end with the expected string</exception>
#if XUNIT_NULLABLE
		public static void EndsWith(string? expectedEndString, string? actualString)
#else
        public static void EndsWith(string expectedEndString, string actualString)
#endif
        {
            EndsWith(expectedEndString, actualString, StringComparison.CurrentCulture);
        }

        /// <summary>
        /// Verifies that a string ends with a given string, using the given comparison type.
        /// </summary>
        /// <param name="expectedEndString">The string expected to be at the end of the string</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <param name="comparisonType">The type of string comparison to perform</param>
        /// <exception cref="ContainsException">Thrown when the string does not end with the expected string</exception>
#if XUNIT_NULLABLE
		public static void EndsWith(string? expectedEndString, string? actualString, StringComparison comparisonType)
#else
        public static void EndsWith(string expectedEndString, string actualString, StringComparison comparisonType)
#endif
        {
            if (expectedEndString == null || actualString == null || !actualString.EndsWith(expectedEndString, comparisonType))
                throw new EndsWithException(expectedEndString, actualString);
        }

        /// <summary>
        /// Verifies that a string matches a regular expression.
        /// </summary>
        /// <param name="expectedRegexPattern">The regex pattern expected to match</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <exception cref="MatchesException">Thrown when the string does not match the regex pattern</exception>
#if XUNIT_NULLABLE
		public static void Matches(string expectedRegexPattern, string? actualString)
#else
        public static void Matches(string expectedRegexPattern, string actualString)
#endif
        {
            NotNull(expectedRegexPattern);

            if (actualString == null || !Regex.IsMatch(actualString, expectedRegexPattern))
                throw new MatchesException(expectedRegexPattern, actualString);
        }

        /// <summary>
        /// Verifies that a string matches a regular expression.
        /// </summary>
        /// <param name="expectedRegex">The regex expected to match</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <exception cref="MatchesException">Thrown when the string does not match the regex</exception>
#if XUNIT_NULLABLE
		public static void Matches(Regex expectedRegex, string? actualString)
#else
        public static void Matches(Regex expectedRegex, string actualString)
#endif
        {
            NotNull(expectedRegex);

            if (actualString == null || !expectedRegex.IsMatch(actualString))
                throw new MatchesException(expectedRegex.ToString(), actualString);
        }

        /// <summary>
        /// Verifies that a string does not match a regular expression.
        /// </summary>
        /// <param name="expectedRegexPattern">The regex pattern expected not to match</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <exception cref="DoesNotMatchException">Thrown when the string matches the regex pattern</exception>
#if XUNIT_NULLABLE
		public static void DoesNotMatch(string expectedRegexPattern, string? actualString)
#else
        public static void DoesNotMatch(string expectedRegexPattern, string actualString)
#endif
        {
            NotNull(expectedRegexPattern);

            if (actualString != null && Regex.IsMatch(actualString, expectedRegexPattern))
                throw new DoesNotMatchException(expectedRegexPattern, actualString);
        }

        /// <summary>
        /// Verifies that a string does not match a regular expression.
        /// </summary>
        /// <param name="expectedRegex">The regex expected not to match</param>
        /// <param name="actualString">The string to be inspected</param>
        /// <exception cref="DoesNotMatchException">Thrown when the string matches the regex</exception>
#if XUNIT_NULLABLE
		public static void DoesNotMatch(Regex expectedRegex, string? actualString)
#else
        public static void DoesNotMatch(Regex expectedRegex, string actualString)
#endif
        {
            NotNull(expectedRegex);

            if (actualString != null && expectedRegex.IsMatch(actualString))
                throw new DoesNotMatchException(expectedRegex.ToString(), actualString);
        }

        /// <summary>
        /// Verifies that two strings are equivalent.
        /// </summary>
        /// <param name="expected">The expected string value.</param>
        /// <param name="actual">The actual string value.</param>
        /// <exception cref="EqualException">Thrown when the strings are not equivalent.</exception>
#if XUNIT_NULLABLE
		public static void Equal(string? expected, string? actual)
#else
        public static void Equal(string expected, string actual)
#endif
        {
            Equal(expected, actual, false, false, false);
        }

        /// <summary>
        /// Verifies that two strings are equivalent.
        /// </summary>
        /// <param name="expected">The expected string value.</param>
        /// <param name="actual">The actual string value.</param>
        /// <param name="ignoreCase">If set to <c>true</c>, ignores cases differences. The invariant culture is used.</param>
        /// <param name="ignoreLineEndingDifferences">If set to <c>true</c>, treats \r\n, \r, and \n as equivalent.</param>
        /// <param name="ignoreWhiteSpaceDifferences">If set to <c>true</c>, treats spaces and tabs (in any non-zero quantity) as equivalent.</param>
        /// <exception cref="EqualException">Thrown when the strings are not equivalent.</exception>
#if XUNIT_NULLABLE
		public static void Equal(
			string? expected,
			string? actual,
			bool ignoreCase = false,
			bool ignoreLineEndingDifferences = false,
			bool ignoreWhiteSpaceDifferences = false)
#else
        public static void Equal(
            string expected,
            string actual,
            bool ignoreCase = false,
            bool ignoreLineEndingDifferences = false,
            bool ignoreWhiteSpaceDifferences = false)
#endif
        {
#if XUNIT_SPAN
			if (expected == null && actual == null)
				return;
			if (expected == null || actual == null)
				throw new EqualException(expected, actual, -1, -1);

			Equal(expected.AsSpan(), actual.AsSpan(), ignoreCase, ignoreLineEndingDifferences, ignoreWhiteSpaceDifferences);
#else
            // Start out assuming the one of the values is null
            int expectedIndex = -1;
            int actualIndex = -1;
            int expectedLength = 0;
            int actualLength = 0;

            if (expected == null)
            {
                if (actual == null)
                    return;
            }
            else if (actual != null)
            {
                // Walk the string, keeping separate indices since we can skip variable amounts of
                // data based on ignoreLineEndingDifferences and ignoreWhiteSpaceDifferences.
                expectedIndex = 0;
                actualIndex = 0;
                expectedLength = expected.Length;
                actualLength = actual.Length;

                while (expectedIndex < expectedLength && actualIndex < actualLength)
                {
                    char expectedChar = expected[expectedIndex];
                    char actualChar = actual[actualIndex];

                    if (ignoreLineEndingDifferences && IsLineEnding(expectedChar) && IsLineEnding(actualChar))
                    {
                        expectedIndex = SkipLineEnding(expected, expectedIndex);
                        actualIndex = SkipLineEnding(actual, actualIndex);
                    }
                    else if (ignoreWhiteSpaceDifferences && IsWhiteSpace(expectedChar) && IsWhiteSpace(actualChar))
                    {
                        expectedIndex = SkipWhitespace(expected, expectedIndex);
                        actualIndex = SkipWhitespace(actual, actualIndex);
                    }
                    else
                    {
                        if (ignoreCase)
                        {
                            expectedChar = Char.ToUpperInvariant(expectedChar);
                            actualChar = Char.ToUpperInvariant(actualChar);
                        }

                        if (expectedChar != actualChar)
                        {
                            break;
                        }

                        expectedIndex++;
                        actualIndex++;
                    }
                }
            }

            if (expectedIndex < expectedLength || actualIndex < actualLength)
            {
                throw new EqualException(expected, actual, expectedIndex, actualIndex);
            }
#endif
        }
        static bool IsLineEnding(char c)
        {
            return c == '\r' || c == '\n';
        }

        static bool IsWhiteSpace(char c)
        {
            return c == ' ' || c == '\t';
        }

        static int SkipLineEnding(string value, int index)
        {
            if (value[index] == '\r')
            {
                ++index;
            }
            if (index < value.Length && value[index] == '\n')
            {
                ++index;
            }

            return index;
        }

        static int SkipWhitespace(string value, int index)
        {
            while (index < value.Length)
            {
                switch (value[index])
                {
                    case ' ':
                    case '\t':
                        index++;
                        break;

                    default:
                        return index;
                }
            }

            return index;
        }

        /// <summary>
        /// Verifies that an object reference is not null.
        /// </summary>
        /// <param name="object">The object to be validated</param>
        /// <exception cref="NotNullException">Thrown when the object reference is null</exception>
#if XUNIT_NULLABLE
		public static void NotNull([NotNull] object? @object)
#else
        public static void NotNull(object @object)
#endif
        {
            if (@object == null)
                throw new NotNullException();
        }

        /// <summary>
        /// Verifies that an object reference is null.
        /// </summary>
        /// <param name="object">The object to be inspected</param>
        /// <exception cref="NullException">Thrown when the object reference is not null</exception>
#if XUNIT_NULLABLE
		public static void Null([MaybeNull] object? @object)
#else
        public static void Null(object @object)
#endif
        {
            if (@object != null)
                throw new NullException(@object);
        }

        /// <summary>
        /// Indicates that the test should immediately fail.
        /// </summary>
        /// <param name="message">The failure message</param>
#if XUNIT_NULLABLE
		[DoesNotReturn]
#endif
        public static void Fail(string message)
        {
            NotNull( message);

            throw new FailException(message);
        }




    }
    #endregion


}




