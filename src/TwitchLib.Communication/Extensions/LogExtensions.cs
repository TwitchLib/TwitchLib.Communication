using System;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

namespace TwitchLib.Communication.Extensions
{
    /// <summary>
    ///     expensive Extensions of the <see cref="ILogger"/>
    /// </summary>
    internal static class LogExtensions
    {
        public static void TraceMethodCall(this ILogger logger,
                                           Type type,
                                           [CallerMemberName] string callerMemberName = "",
                                           [CallerLineNumber] int callerLineNumber = 0)
        {
            // because of the code-formatting, 2 line is subtracted from the callerLineNumber
            // cant be done inline!
            callerLineNumber -= 2;
            logger?.LogTrace("{FullName}.{CallerMemberName} at line {CallerLineNumber} is called",
                             type.FullName, callerMemberName, callerLineNumber);
        }
        public static void LogExceptionAsError(this ILogger logger,
                                               Type type,
                                               Exception exception,
                                               [CallerMemberName] string callerMemberName = "",
                                               [CallerLineNumber] int callerLineNumber = 0)
        {
            logger?.LogError(exception,
                             "Exception in {FullName}.{CallerMemberName} at line {CallerLineNumber}:",
                             type.FullName, callerMemberName, callerLineNumber);
        }
        public static void LogExceptionAsInformation(this ILogger logger,
                                                     Type type,
                                                     Exception exception,
                                                     [CallerMemberName] string callerMemberName = "",
                                                     [CallerLineNumber] int callerLineNumber = 0)
        {
            logger?.LogInformation(exception,
                                   "Exception in {FullName}.{CallerMemberName} at line {CallerLineNumber}:",
                                   type.FullName, callerMemberName, callerLineNumber);
        }
        public static void TraceAction(this ILogger logger,
                                       Type type,
                                       string action,
                                       [CallerMemberName] string callerMemberName = "",
                                       [CallerLineNumber] int callerLineNumber = 0)
        {
            logger?.LogTrace("{FullName}.{CallerMemberName} at line {CallerLineNumber}: {Action}",
                             type.FullName, callerMemberName, callerLineNumber, action);
        }
    }
}
