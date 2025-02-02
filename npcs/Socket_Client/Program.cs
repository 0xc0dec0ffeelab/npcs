using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks.Dataflow;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

Console.Title = "Socket Client"; // console title

TcpClient? client = null;
byte[] buffer = new byte[1024];

client = await ConnectAsync(client);

CancellationTokenSource cts = new();
Task receiveTask = CreateReceiveTask(client, buffer, cts);

while (true)
{
    string input = Console.ReadLine() ?? string.Empty;
    if (input.Length == 1 && input.ToLower() == "d") // disconnect
    {
        client = Disconnect(client);
        cts.Cancel();
        Console.WriteLine($"disconnect");
    }
    else if (input.Length == 1 && input.ToLower() == "r") // reconnect
    {
        cts.Cancel();
        client = await ConnectAsync(client);

        cts = new CancellationTokenSource();
        receiveTask = CreateReceiveTask(client, buffer, cts);
        Console.WriteLine($"reconnect");
    }
    else if (input.StartsWith("send")) // send message to a client
    {
        
        var parts = input.Split(" ");

        if (parts.Length > 1)
        {
            string message = string.Join(" ", parts.Skip(1));
            await SendMessageAsync(client, message, client.GetStream());
        }
    }
}

static Task CreateReceiveTask(TcpClient client, byte[] buffer, CancellationTokenSource cts)
{
    var receiveTask = Task.Run(async () =>
    {
        while (cts.Token.IsCancellationRequested == false)
        {
            var message = await ReceiveMessageAsync(client, buffer, cts);

            if (string.IsNullOrEmpty(message) == false)
            {
                Console.WriteLine($"Received from server: {message}");
            }
            else
            {
                // server closed
                Console.WriteLine("server closed");
                break;
            }
            
        }
    }, cts.Token);

    return receiveTask;
}
static async Task SendMessageAsync(TcpClient? client, string message, NetworkStream stream)
{
    if (client == null) return;
    await WriteMessageAsync(stream, message);
}

static async Task<string> ReceiveMessageAsync(TcpClient? client, byte[] buffer, CancellationTokenSource cts)
{
    ArgumentNullException.ThrowIfNull(client);

    int bytesRead;
    NetworkStream? stream = client.GetStream();
    ValueTask<int> bytesReadTask = stream.ReadAsync(buffer);

    if ((bytesRead = await bytesReadTask) > 0)
    {
        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        return message;
    }

    client = Disconnect(client);
    cts.Cancel();
    return string.Empty;
}


static async Task WriteMessageAsync(NetworkStream? stream, string message)
{
    if (stream == null) return;
    byte[] data = Encoding.UTF8.GetBytes(message);
    await stream.WriteAsync(data);
}

static async Task<TcpClient> ConnectAsync(TcpClient? client)
{
    if (client != null && client.Connected) return client;
    client = new();
    await client.ConnectAsync($"{IPAddress.Loopback}", 12345);
    return client;
}

static TcpClient? Disconnect(TcpClient? client)
{
    if (client != null && client.Connected)
    {
        Console.WriteLine("disconnecting from server...");
        client.Close();
    }
    return default;
}