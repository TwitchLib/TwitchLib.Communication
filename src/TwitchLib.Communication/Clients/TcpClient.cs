using System.Net.Security;
using Microsoft.Extensions.Logging;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Extensions;
using TwitchLib.Communication.Interfaces;

namespace TwitchLib.Communication.Clients;

public class TcpClient : ClientBase<System.Net.Sockets.TcpClient>
{
    private StreamReader? _reader;
    private StreamWriter? _writer;

    /// <inheritdoc/>
    protected override string Url => "irc.chat.twitch.tv";

    private int Port => Options.UseSsl ? 6697 : 6667;

    /// <inheritdoc/>
    public override bool IsConnected => Client?.Connected ?? false;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpClient"/>.
    /// </summary>
    public TcpClient(
        IClientOptions? options = null,
        ILogger<TcpClient>? logger = null)
        : base(options, logger)
    {
    }

    internal override async Task ListenTaskActionAsync()
    {
        Logger?.TraceMethodCall(GetType());
        if (_reader == null)
        {
            var ex = new InvalidOperationException($"{nameof(_reader)} was null!");
            Logger?.LogExceptionAsError(GetType(), ex);
            await RaiseFatal(ex);
            throw ex;
        }

        while (IsConnected)
        {
            try
            {
                var input = await _reader.ReadLineAsync();
                if (input is null)
                {
                    continue;
                }

                await RaiseMessage(new OnMessageEventArgs(input));
            }
            catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
            {
                // occurs if the Tasks are canceled by the CancellationTokenSource.Token
                Logger?.LogExceptionAsInformation(GetType(), ex);
            }
            catch (Exception ex)
            {
                Logger?.LogExceptionAsError(GetType(), ex);
                await RaiseError(new OnErrorEventArgs(ex));
                break;
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task ClientSendAsync(string message)
    {
        Logger?.TraceMethodCall(GetType());

        // this is not thread safe
        // this method should only be called from 'ClientBase.Send()'
        // where its call gets synchronized/locked
        // https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream?view=netstandard-2.0#remarks
        if (_writer == null)
        {
            var ex = new InvalidOperationException($"{nameof(_writer)} was null!");
            Logger?.LogExceptionAsError(GetType(), ex);
            await RaiseFatal(ex);
            throw ex;
        }

        await _writer.WriteLineAsync(message);
        await _writer.FlushAsync();
    }

    /// <inheritdoc/>
    protected override async Task ConnectClientAsync()
    {
        Logger?.TraceMethodCall(GetType());
        if (Client == null)
        {
            Exception ex = new InvalidOperationException($"{nameof(Client)} was null!");
            Logger?.LogExceptionAsError(GetType(), ex);
            throw ex;
        }

        try
        {
            // https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios
#if NET6_0_OR_GREATER
            // within the following thread:
            // https://stackoverflow.com/questions/4238345/asynchronously-wait-for-taskt-to-complete-with-timeout
            // the following answer
            // NET6_0_OR_GREATER: https://stackoverflow.com/a/68998339

            var connectTask = Client.ConnectAsync(Url, Port);
            var waitTask = connectTask.WaitAsync(TimeOutEstablishConnection, Token);
            await Task.WhenAny(connectTask, waitTask);
#else
                // within the following thread:
                // https://stackoverflow.com/questions/4238345/asynchronously-wait-for-taskt-to-complete-with-timeout
                // the following two answers:
                // https://stackoverflow.com/a/11191070
                // https://stackoverflow.com/a/22078975

                using (var delayTaskCancellationTokenSource = new CancellationTokenSource())
                {
                    var connectTask = Client.ConnectAsync(Url, Port);
                    var delayTask = Task.Delay(
                        (int)TimeOutEstablishConnection.TotalMilliseconds,
                        delayTaskCancellationTokenSource.Token);

                    await Task.WhenAny(connectTask, delayTask);
                    delayTaskCancellationTokenSource.Cancel();
                }
#endif
            if (!Client.Connected)
            {
                Logger?.TraceAction(GetType(), "Client couldn't establish connection");
                return;
            }

            Logger?.TraceAction(GetType(), "Client established connection successfully");
            Stream stream = Client.GetStream();
            if (Options.UseSsl)
            {
                var ssl = new SslStream(stream, false);
                await ssl.AuthenticateAsClientAsync(Url);
                stream = ssl;
            }
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream);
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            // occurs if the Tasks are canceled by the CancellationTokenSource.Token
            Logger?.LogExceptionAsInformation(GetType(), ex);
        }
        catch (Exception ex)
        {
            Logger?.LogExceptionAsError(GetType(), ex);
        }
    }

    /// <inheritdoc/>
    protected override System.Net.Sockets.TcpClient CreateClient()
    {
        Logger?.TraceMethodCall(GetType());

        return new System.Net.Sockets.TcpClient
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient.lingerstate?view=netstandard-2.0#remarks
            LingerState = new System.Net.Sockets.LingerOption(true, 0)
        };
    }

    /// <inheritdoc/>
    protected override void CloseClient()
    {
        Logger?.TraceMethodCall(GetType());
        _reader?.Dispose();
        _writer?.Dispose();
        Client?.Dispose();
    }
}