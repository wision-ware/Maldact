namespace Maldact.Core.Streaming;

public interface IStreamingClient
{
    Task ConnectAsync(string host, int port, string? token = null);
    Task DisconnectAsync();
    Task SendAsync(float[] data);
    bool IsConnected { get; }
}