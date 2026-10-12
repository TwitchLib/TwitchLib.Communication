using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchLib.Communication.Tests.Helpers;

/// <summary>
///     A WebSocket server on the loopback interface,
///     to test how a client behaves when the server closes or drops the connection.
/// </summary>
internal sealed class LocalWebSocketServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly SemaphoreSlim _connected = new(0);
    private readonly CancellationTokenSource _stop = new();
    private TcpClient? _currentTcp;
    private WebSocket? _current;
    private int _connectionCount;
    private int _openConnectionCount;

    public LocalWebSocketServer()
    {
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    public string Url => $"ws://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";

    public int ConnectionCount => Volatile.Read(ref _connectionCount);

    /// <summary>Connections the client has not closed yet.</summary>
    public int OpenConnectionCount => Volatile.Read(ref _openConnectionCount);

    public Task<bool> WaitForConnectionAsync(TimeSpan timeout)
    {
        return _connected.WaitAsync(timeout);
    }

    public Task SendAsync(string message)
    {
        return _current!.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    /// <summary>Closes the current connection with a close frame, as a server does on a clean shutdown.</summary>
    public Task CloseAsync()
    {
        return _current!.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
    }

    /// <summary>Resets the current TCP connection, as on a network failure.</summary>
    public void Drop()
    {
        var tcp = _currentTcp!;
        tcp.Client.LingerState = new LingerOption(true, 0);
        tcp.Close();
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _currentTcp?.Close();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient tcp;
            try
            {
                tcp = await _listener.AcceptTcpClientAsync();
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                return;
            }

            var stream = tcp.GetStream();
            var key = await ReadWebSocketKeyAsync(stream);
            var accept = Convert.ToBase64String(
                SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            var response = "HTTP/1.1 101 Switching Protocols\r\n" +
                           "Upgrade: websocket\r\n" +
                           "Connection: Upgrade\r\n" +
                           $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));

            var webSocket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true });
            _currentTcp = tcp;
            _current = webSocket;
            Interlocked.Increment(ref _connectionCount);
            Interlocked.Increment(ref _openConnectionCount);
            _ = DrainAsync(webSocket);
            _connected.Release();
        }
    }

    private static async Task<string> ReadWebSocketKeyAsync(Stream stream)
    {
        // read the upgrade request byte by byte up to the empty line, the client sends nothing else before the response
        var request = new StringBuilder();
        var buffer = new byte[1];
        while (!request.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (await stream.ReadAsync(buffer) == 0) throw new IOException("connection closed during the handshake");
            request.Append((char)buffer[0]);
        }

        foreach (var line in request.ToString().Split("\r\n"))
        {
            if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                return line.Substring("Sec-WebSocket-Key:".Length).Trim();
        }

        throw new InvalidOperationException("no Sec-WebSocket-Key in the upgrade request");
    }

    private async Task DrainAsync(WebSocket webSocket)
    {
        var buffer = new byte[1024];
        try
        {
            while (webSocket.State is WebSocketState.Open or WebSocketState.CloseSent)
            {
                await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
        }
        catch (Exception)
        {
            // the connection is gone, nothing to drain anymore
        }
        finally
        {
            Interlocked.Decrement(ref _openConnectionCount);
        }
    }
}
