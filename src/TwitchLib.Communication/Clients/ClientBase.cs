using Microsoft.Extensions.Logging;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Extensions;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Services;

namespace TwitchLib.Communication.Clients;

/// <summary>
///     This <see langword="class"/> bundles almost everything that <see cref="TcpClient"/> and <see cref="WebSocketClient"/> have in common
///     to be able to 
///     <list>
///         <item>
///             pass instances of this <see langword="class"/> to <see cref="NetworkServices{T}"/>
///         </item>
///         <item>
///             and to access Methods of this instance within <see cref="NetworkServices{T}"/>
///         </item>
///     </list>
/// </summary>
public abstract class ClientBase<T> : IClient
    where T : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly NetworkServices<T> _networkServices;
    private CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    ///     This <see cref="_cancellationTokenSource"/> is used for <see cref="_networkServices.ListenTask"/>
    ///     whenever a call to <see cref="CancellationTokenSource.Cancel()"/> is made
    /// </summary>
    internal CancellationToken Token => _cancellationTokenSource.Token;

    internal static TimeSpan TimeOutEstablishConnection => TimeSpan.FromSeconds(15);

    protected readonly ILogger<ClientBase<T>>? Logger;

    protected abstract string Url { get; }

    /// <summary>
    ///     The underlying <typeparamref name="T"/> client.
    /// </summary>
    public T? Client { get; private set; }

    /// <inheritdoc/>
    public abstract bool IsConnected { get; }

    /// <inheritdoc/>
    public IClientOptions Options { get; }

    /// <inheritdoc/>
    public event AsyncEventHandler<OnConnectedEventArgs>? OnConnected;

    /// <inheritdoc/>
    public event AsyncEventHandler<OnDisconnectedEventArgs>? OnDisconnected;
    
    /// <inheritdoc/>
    public event AsyncEventHandler<OnErrorEventArgs>? OnError;
    
    /// <inheritdoc/>
    public event AsyncEventHandler<OnFatalErrorEventArgs>? OnFatality;
    
    /// <inheritdoc/>
    public event AsyncEventHandler<OnMessageEventArgs>? OnMessage;
    
    /// <inheritdoc/>
    public event AsyncEventHandler<OnSendFailedEventArgs>? OnSendFailed;
    
    /// <inheritdoc/>
    public event AsyncEventHandler<OnConnectedEventArgs>? OnReconnected;

    internal ClientBase(
        IClientOptions? options,
        ILogger<ClientBase<T>>? logger)
    {
        Logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();
        Options = options ?? new ClientOptions();
        _networkServices = new NetworkServices<T>(this, logger);
    }

    /// <summary>
    ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
    /// </summary>
    private async Task RaiseSendFailed(OnSendFailedEventArgs eventArgs)
    {
        Logger?.TraceMethodCall(GetType());
        if (Token.IsCancellationRequested)
        {
            return;
        }

        await InvokeSubscribersAsync(OnSendFailed, eventArgs);
    }

    /// <summary>
    ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
    ///     <br></br>
    ///     An exception thrown by a subscriber is logged and swallowed: there is nowhere left to report it.
    /// </summary>
    internal async Task RaiseError(OnErrorEventArgs eventArgs)
    {
        Logger?.TraceMethodCall(GetType());
        if (Token.IsCancellationRequested || OnError == null)
        {
            return;
        }

        foreach (var subscriber in OnError.GetInvocationList().Cast<AsyncEventHandler<OnErrorEventArgs>>())
        {
            try
            {
                await subscriber(this, eventArgs);
            }
            catch (Exception ex)
            {
                Logger?.LogExceptionAsError(GetType(), ex);
            }
        }
    }

    /// <summary>
    ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
    /// </summary>
    private async Task RaiseReconnected()
    {
        Logger?.TraceMethodCall(GetType());
        if (Token.IsCancellationRequested)
        {
            return;
        }

        await InvokeSubscribersAsync(OnReconnected, new OnConnectedEventArgs());
    }

    /// <summary>
    ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
    /// </summary>
    internal async Task RaiseMessage(OnMessageEventArgs eventArgs)
    {
        Logger?.TraceMethodCall(GetType());
        if (Token.IsCancellationRequested)
        {
            return;
        }

        await InvokeSubscribersAsync(OnMessage, eventArgs);
    }

    /// <summary>
    ///     Wont raise the given <see cref="EventArgs"/> if <see cref="Token"/>.IsCancellationRequested
    /// </summary>
    internal async Task RaiseFatal(Exception? ex = null)
    {
        Logger?.TraceMethodCall(GetType());
        if (Token.IsCancellationRequested)
        {
            return;
        }

        var onFatalErrorEventArgs = ex != null
            ? new OnFatalErrorEventArgs(ex)
            : new OnFatalErrorEventArgs("Fatal network error.");

        await InvokeSubscribersAsync(OnFatality, onFatalErrorEventArgs);
    }

    private async Task RaiseDisconnected()
    {
        Logger?.TraceMethodCall(GetType());
        await InvokeSubscribersAsync(OnDisconnected, new OnDisconnectedEventArgs());
    }

    private async Task RaiseConnected()
    {
        Logger?.TraceMethodCall(GetType());
        await InvokeSubscribersAsync(OnConnected, new OnConnectedEventArgs());
    }

    /// <summary>
    ///     Awaits every subscriber of <paramref name="handler"/> one after another
    ///     and reports an exception thrown by a subscriber through <see cref="OnError"/> instead of rethrowing it.
    ///     <br></br>
    ///     <br></br>
    ///     The events are raised from the listen task and from the <see cref="ConnectionWatchDog{T}"/>.
    ///     An exception escaping from a subscriber used to end them silently:
    ///     <see cref="IsConnected"/> kept returning <see langword="true"/>,
    ///     while no more messages were read and no reconnect was made.
    /// </summary>
    private async Task InvokeSubscribersAsync<TEventArgs>(AsyncEventHandler<TEventArgs>? handler, TEventArgs eventArgs)
    {
        if (handler == null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList().Cast<AsyncEventHandler<TEventArgs>>())
        {
            try
            {
                await subscriber(this, eventArgs);
            }
            catch (Exception ex)
            {
                Logger?.LogExceptionAsError(GetType(), ex);
                await RaiseError(new OnErrorEventArgs(ex));
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SendAsync(string message)
    {
        Logger?.TraceMethodCall(GetType());

        await _semaphore.WaitAsync(Token);
        try
        {
            await ClientSendAsync(message);
            return true;
        }
        catch (Exception e)
        {
            await RaiseSendFailed(new OnSendFailedEventArgs(e, message));
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public Task<bool> OpenAsync()
    {
        Logger?.TraceMethodCall(GetType());
        return OpenPrivateAsync(false);
    }

    /// <inheritdoc/>
    public async Task CloseAsync()
    {
        Logger?.TraceMethodCall(GetType());

        // ClosePrivate() also handles IClientOptions.DisconnectWait
        await ClosePrivateAsync();
    }

    /// <summary>
    ///     <inheritdoc cref="CloseAsync"/>
    /// </summary>
    public void Dispose()
    {
        Logger?.TraceMethodCall(GetType());
        CloseAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async Task<bool> ReconnectAsync()
    {
        Logger?.TraceMethodCall(GetType());

        return await ReconnectInternalAsync();
    }

    private async Task<bool> OpenPrivateAsync(bool isReconnect)
    {
        Logger?.TraceMethodCall(GetType());
        try
        {
            if (Token.IsCancellationRequested)
            {
                return false;
            }

            if (IsConnected)
            {
                return true;
            }

            var first = true;
            Options.ReconnectionPolicy.Reset(isReconnect);

            while (!IsConnected &&
                   !Options.ReconnectionPolicy.AreAttemptsComplete())
            {
                Logger?.TraceAction(GetType(), "try to connect");

                // Always create new client when opening new connection
                Client = CreateClient();

                if (!first)
                {
                    await Task.Delay(Options.ReconnectionPolicy.GetReconnectInterval(), CancellationToken.None);
                }

                await ConnectClientAsync();
                Options.ReconnectionPolicy.ProcessValues();
                first = false;
            }

            if (!IsConnected)
            {
                Logger?.TraceAction(GetType(), "Client couldn't establish a connection");
                await RaiseFatal();
                return false;
            }

            Logger?.TraceAction(GetType(), "Client established a connection");
            Options.ReconnectionPolicy.ConnectionEstablished();
            _networkServices.Start();

            if (!isReconnect)
            {
                await RaiseConnected();
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger?.LogExceptionAsError(GetType(), ex);
            await RaiseError(new OnErrorEventArgs(ex));
            await RaiseFatal();
            return false;
        }
    }

    /// <summary>
    ///     Stops <see cref="_networkServices.ListenTaskAsync"/>
    ///     by calling <see cref="CancellationTokenSource.Cancel()"/>
    ///     <br></br>
    ///     and enforces the <see cref="CloseClient"/>
    ///     <br></br>
    ///     afterwards it waits for the via <see cref="IClientOptions.DisconnectWait"/> given amount of milliseconds
    ///     <br></br>
    ///     <br></br>
    ///     <see cref="ConnectionWatchDog{T}"/> will keep running,
    ///     because itself issued this call by calling <see cref="ReconnectInternalAsync()"/>
    /// </summary>
    private async Task ClosePrivateAsync()
    {
        Logger?.TraceMethodCall(GetType());

        await _networkServices.StopAsync();

        // This cancellation traverse up to NetworkServices.ListenTask
        _cancellationTokenSource.Cancel();
        Logger?.TraceAction(GetType(),
            $"{nameof(_cancellationTokenSource)}.{nameof(_cancellationTokenSource.Cancel)} is called");

        CloseClient();
        await RaiseDisconnected();
        _cancellationTokenSource = new CancellationTokenSource();

        await Task.Delay(TimeSpan.FromMilliseconds(Options.DisconnectWait), CancellationToken.None);
    }

    /// <summary>
    ///     Send method for the client.
    /// </summary>
    /// <param name="message">
    ///     Message to be send
    /// </param>
    protected abstract Task ClientSendAsync(string message);

    /// <summary>
    ///     Instantiate the underlying client.
    /// </summary>
    protected abstract T CreateClient();

    /// <summary>
    ///     one of the following specific methods
    ///     <list>
    ///         <item>
    ///             <see cref="System.Net.Sockets.TcpClient.Close"/>
    ///         </item>
    ///         <item>
    ///             <see cref="System.Net.WebSockets.ClientWebSocket.Abort"/>
    ///         </item>
    ///     </list>
    ///     calls to one of the methods mentioned above,
    ///     also Dispose() the respective client,
    ///     so no additional Dispose() is needed
    /// </summary>
    protected abstract void CloseClient();

    /// <summary>
    ///     Connect the client.
    /// </summary>
    protected abstract Task ConnectClientAsync();

    /// <summary>
    ///     To issue a reconnect
    ///     <br></br>
    ///     especially for the <see cref="ConnectionWatchDog{T}"/>
    ///     <br></br>
    ///     it stops all <see cref="NetworkServices{T}"/> but <see cref="ConnectionWatchDog{T}"/>!
    ///     <br></br>
    ///     <br></br>
    ///     see also <seealso cref="OpenAsync"/>:
    ///     <br></br>
    ///     <inheritdoc cref="OpenAsync"/>
    /// </summary>
    /// <returns>
    ///     <see langword="true"/> if a connection could be established, <see langword="false"/> otherwise
    /// </returns>
    internal async Task<bool> ReconnectInternalAsync()
    {
        Logger?.TraceMethodCall(GetType());

        await ClosePrivateAsync();
        var reconnected = await OpenPrivateAsync(true);
        if (reconnected)
        {
            await RaiseReconnected();
        }

        return reconnected;
    }

    /// <summary>
    ///     just the Action that listens for new Messages
    ///     the corresponding <see cref="Task"/> is held by <see cref="NetworkServices{T}"/>
    /// </summary>
    internal abstract Task ListenTaskActionAsync();
}
