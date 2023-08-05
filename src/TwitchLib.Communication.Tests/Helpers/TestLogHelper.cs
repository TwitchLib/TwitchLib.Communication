using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace TwitchLib.Communication.Tests.Helpers;

internal static class TestLogHelper
{
    private static readonly string OUTPUT_TEMPLATE =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}]  [{Level:u}]  {Message:lj}{NewLine}{Exception}{NewLine}";

    private static readonly string NEW_TEST_RUN_INDICATOR;

    static TestLogHelper()
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.Append('-', 80).AppendLine();
        builder.Append(' ', 34);
        builder.AppendLine("new Test-Run");
        builder.Append('-', 80).AppendLine();
        NEW_TEST_RUN_INDICATOR = builder.ToString();
    }

    internal static Microsoft.Extensions.Logging.ILogger<T> GetLogger<T>(
        LogEventLevel logEventLevel = LogEventLevel.Verbose,
        [CallerMemberName] string callerMemberName = "TestMethod")
    {
        Serilog.ILogger logger = GetSerilogLogger<T>(typeof(T).Name,
            callerMemberName,
            logEventLevel);
        Microsoft.Extensions.Logging.ILoggerFactory loggerFactory =
            new Serilog.Extensions.Logging.SerilogLoggerFactory(logger);
        return loggerFactory.CreateLogger<T>();
    }

    private static Serilog.ILogger GetSerilogLogger<T>(string typeName,
        string callerMemberName,
        LogEventLevel logEventLevel)
    {
        Serilog.LoggerConfiguration loggerConfiguration = GetConfiguration(typeName,
            callerMemberName,
            logEventLevel);
        Serilog.ILogger logger = loggerConfiguration.CreateLogger().ForContext<T>();
        logger.Information(NEW_TEST_RUN_INDICATOR);
        return logger;
    }

    private static Serilog.LoggerConfiguration GetConfiguration(string typeName,
        string callerMemberName,
        LogEventLevel logEventLevel)
    {
        var loggerConfiguration = new Serilog.LoggerConfiguration();
        loggerConfiguration.MinimumLevel.Verbose();
        string path = $"../../../Logs/{typeName}/{callerMemberName}.log";
        loggerConfiguration.WriteTo.File(
            path: path,
            restrictedToMinimumLevel: logEventLevel,
            outputTemplate: OUTPUT_TEMPLATE
        );
        loggerConfiguration.Enrich.WithExceptionDetails();
        loggerConfiguration.Enrich.FromLogContext();
        return loggerConfiguration;
    }
}
