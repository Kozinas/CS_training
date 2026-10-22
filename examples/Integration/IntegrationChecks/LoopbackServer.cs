using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Course.IntegrationChecks;

internal sealed record Reply(int Status, string Body, string Type = "application/json",
    bool OmitLength = false, int DelayBodyMs = 0);

internal sealed class LoopbackServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<int, string, Reply> _reply;
    private readonly Task _run;
    private int _requests;
    public int Requests => Volatile.Read(ref _requests);
    public Uri Address { get; }

    public LoopbackServer(Func<int, string, Reply> reply)
    {
        _reply = reply;
        _listener.Start();
        Address = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/data");
        _run = ServeAsync();
    }

    private async Task ServeAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var socket = await _listener.AcceptTcpClientAsync(_stop.Token);
                await using var stream = socket.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                string firstLine = await reader.ReadLineAsync(_stop.Token) ?? "";
                int headerLength = firstLine.Length;
                while (await reader.ReadLineAsync(_stop.Token) is { Length: > 0 } line)
                {
                    headerLength += line.Length;
                    if (headerLength > 8192) throw new InvalidDataException("Test request too large.");
                }
                var reply = _reply(Interlocked.Increment(ref _requests), firstLine);
                byte[] body = Encoding.UTF8.GetBytes(reply.Body);
                string headers = $"HTTP/1.1 {reply.Status} Test\r\nContent-Type: {reply.Type}\r\nConnection: close\r\n"
                    + (reply.OmitLength ? "" : $"Content-Length: {body.Length}\r\n") + "\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), _stop.Token);
                if (reply.DelayBodyMs > 0) await Task.Delay(reply.DelayBodyMs, _stop.Token);
                await stream.WriteAsync(body, _stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
        catch (IOException) when (_stop.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        try { await _run; }
        finally { _stop.Dispose(); }
    }
}
