using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks.Dataflow;
using System;


TcpClient? client = null;
byte[] buffer = new byte[1024];

client =await ConnectAsync(client);

CancellationTokenSource cts = new();
Task receiveTask = CreateReceiveTask(client, buffer, cts.Token);

while (true)
{
    string? input = Console.ReadLine();

    if (input?.Length == 1 && input?.ToLower() == "d") // disconnect
    {
        Disconnect(client);
        cts.Cancel();
    }
    else if (input?.Length == 1 && input?.ToLower() == "r") // reconnect
    {
        cts.Cancel();
        client = await ConnectAsync(client);


        cts = new CancellationTokenSource();
        receiveTask = CreateReceiveTask(client, buffer, cts.Token);
    }
    else if (input.StartsWith("send")) // send message to a client
    {
        var parts = input.Split(' ');

        if (parts.Length == 2)
        {
            string message = parts[1];
            await SendMessageAsync(client, message, client.GetStream());
        }
    }
}

static Task CreateReceiveTask(TcpClient client, byte[] buffer, CancellationToken token)
{
    var receiveTask = Task.Run(async () =>
    {
        while (token.IsCancellationRequested == false)
        {
            await foreach (var message in ReceiveMessageAsync(client, buffer))
            {
                Console.WriteLine($"Received from server: {message}");
            }
        }
    }, token);

    return receiveTask;
}
static async Task SendMessageAsync(TcpClient? client, string message, NetworkStream stream)
{
    if (client == null) return;
    await WriteMessageAsync(stream, message);
}

static async IAsyncEnumerable<string> ReceiveMessageAsync(TcpClient? client, byte[] buffer)
{
    ArgumentNullException.ThrowIfNull(client);

    int bytesRead;
    NetworkStream? stream = client.GetStream();
    ValueTask<int> bytesReadTask = stream.ReadAsync(buffer);

    if ((bytesRead = await bytesReadTask) > 0)
    {
        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        yield return message;
    }
}


static async Task WriteMessageAsync(NetworkStream? stream, string message)
{
    if (stream == null) return;
    byte[] data = Encoding.UTF8.GetBytes(message);
    await stream.WriteAsync(data);
    //await stream.FlushAsync();
}

static async Task<TcpClient> ConnectAsync(TcpClient? client)
{
    if (client != null && client.Connected) return client;
    client ??= new();
    await client.ConnectAsync($"{IPAddress.Loopback}", 12345);
    return client;
}

static void Disconnect(TcpClient? client)
{
    if (client != null && client.Connected)
    {
        Console.WriteLine("Disconnecting from server...");
        client.Close();
    }
    client = null;
}