using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace TwitchLib.Communication.Extensions
{
    /// <summary>
    ///     expensive Extensions of the <see cref="ILogger"/>
    /// </summary>
    internal static partial class LogExtensions
    {
        public static void TraceMethodCall(this ILogger logger, Type type, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            // because of the code-formatting, 2 line is subtracted from the callerLineNumber
            // cant be done inline!
            callerLineNumber -= 2;
            TraceMethodCallCore(logger, type, callerMemberName, callerLineNumber);
        }

        [LoggerMessage(LogLevel.Trace, "{type}.{callerMemberName} at line {callerLineNumber} is called")]
        static partial void TraceMethodCallCore(this ILogger logger, Type type, string callerMemberName, int callerLineNumber);

        [LoggerMessage(LogLevel.Error, "Exception in {type}.{callerMemberName} at line {callerLineNumber}")]
        public static partial void LogExceptionAsError(this ILogger logger, Type type, Exception exception, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0);

        [LoggerMessage(LogLevel.Information, "Exception in {type}.{callerMemberName} at line {callerLineNumber}")]
        public static partial void LogExceptionAsInformation(this ILogger logger, Type type, Exception exception, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0);

        [LoggerMessage(LogLevel.Trace, "{type}.{callerMemberName} at line {callerLineNumber}: {action}")]
        public static partial void TraceAction(this ILogger logger, Type type, string action, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0);
    }
}
