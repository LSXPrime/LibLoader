using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace LibLoader.Samples.Basic;

public class LocalFileServer(string resourceName, int speedLimitKbps)
{
    public int Port { get; private set; }
    private readonly int _speedLimitBytesPerSecond = speedLimitKbps * 1024 / 8;
    private bool _isRunning;
    private TcpListener? _listener;

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();

        _isRunning = true;
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Console.WriteLine($"Server listening on port {Port}");

        while (_isRunning)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                Console.WriteLine("Client connected.");
                _ = HandleClientAsync(client); // Handle client in the background
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting client: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            await using var stream = client.GetStream();
            await using var writer = new StreamWriter(stream);
            // Accessing embedded resource
            var assembly = Assembly.GetExecutingAssembly();
            await using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                await writer.WriteLineAsync($"HTTP/1.1 404 Not Found");
                await writer.WriteLineAsync("Content-Length: 0");
                await writer.WriteLineAsync("Connection: close");
                await writer.WriteLineAsync("");
                await writer.FlushAsync();

                return;
            }

            // Send HTTP headers
            await writer.WriteLineAsync("HTTP/1.1 200 OK");
            await writer.WriteLineAsync($"Content-Length: {resourceStream.Length}");
            await writer.WriteLineAsync("Content-Type: application/octet-stream"); // Or the correct MIME type
            await writer.WriteLineAsync(""); // Empty line to signify end of headers
            await writer.FlushAsync();

            var buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = await resourceStream.ReadAsync(buffer)) > 0)
            {
                var startTime = DateTime.Now.Ticks;
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead));

                var elapsedTicks = DateTime.Now.Ticks - startTime;
                var elapsedSeconds = elapsedTicks / (double)TimeSpan.TicksPerSecond;

                if (_speedLimitBytesPerSecond > 0)
                {
                    var expectedTransferTime = (double)bytesRead / _speedLimitBytesPerSecond;
                    if (elapsedSeconds < expectedTransferTime)
                    {
                        var sleepMilliseconds = (int)((expectedTransferTime - elapsedSeconds) * 1000);
                        if (sleepMilliseconds > 0)
                            await Task.Delay(sleepMilliseconds);
                    }
                }
            }

            Console.WriteLine("File transfer complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }


    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
    }
}