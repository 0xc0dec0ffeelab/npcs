using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


MyTcpListener? listener = null;
MyConcurrentDictionary clientsDic = new();


listener = Open(listener);
CancellationTokenSource cts = new();


/* client console */
CreateClients(clientNum: 3);

Task acceptTask = CreateAcceptTask(listener, clientsDic, cts.Token);

while (true)
{
    string input = Console.ReadLine() ?? string.Empty;
    if (input.Length == 1 && input.ToLower() == "d") // close
    {
        Disconnect(clientsDic);
        cts.Cancel();
        Close(listener);
        Console.WriteLine("close the listener");
    }
    else if (input.Length == 1 && input.ToLower() == "r") // reopen
    {
        cts.Cancel();
        listener = Open(listener);

        cts = new();
        acceptTask = CreateAcceptTask(listener, clientsDic, cts.Token);
        Console.WriteLine("reopen the listener");
    }
    else if (input.StartsWith("send")) // send message to a client
    {
        var parts = input.Split(" ");
        if (parts.Length > 2)
        {
            string clientId = parts[1];
            string message = string.Join(" ", parts.Skip(2));
            await SendMessageToClientAsync(clientsDic, clientId, message);
        }
    }
}

static Task CreateAcceptTask(MyTcpListener? listener, MyConcurrentDictionary clientsDic,  CancellationToken token)
{
    var acceptTask = Task.Run(async () =>
    {
        while (token.IsCancellationRequested == false)
        {
            await AcceptAsync(listener, clientsDic, token);
        }
    }, token);

    return acceptTask;
}

static void CreateClients(int clientNum)
{
    var directory = Directory.GetParent(Directory.GetCurrentDirectory())?.Parent?.Parent?.Parent;

    string clientPath = Path.Combine(directory?.FullName!, @"./Socket_Client/bin/Debug/net8.0/Socket_Client.exe");
    if (File.Exists(clientPath))
    {

        for (int i = 0; i < clientNum; i++)
        {
            ProcessStartInfo psi = new()
            {
                FileName = clientPath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
    }
    else
    {
        Console.WriteLine("Socket_Client.exe not found");
    }
}

static async Task AcceptAsync(MyTcpListener? listener, MyConcurrentDictionary clientsDic, CancellationToken token)
{
    if (listener == null) return;
    if (listener.IsListened == false) return;

    while (true)
    {
        MyTcpClient newClient = new(await listener.AcceptTcpClientAsync(token));
        bool isOk = clientsDic.TryGetValue(newClient.ClientId, out _);

        if (isOk) continue;

        var isAdd = clientsDic.TryAdd(newClient.ClientId, newClient, token);
        if (isAdd == false) return;
        await SendMessageToClientAsync(clientsDic, newClient.ClientId, newClient.ClientId);
        Console.WriteLine($"Client: {newClient.ClientId} connected...");

    }
}
static MyTcpListener Listen(MyTcpListener? listener)
{
    listener ??= new(IPAddress.Loopback, 12345);
    if (listener.IsListened) return listener;
    listener.Start();
    listener.IsListened = true;
    return listener;
}
static MyTcpListener Open(MyTcpListener? listener)
{
    return Listen(listener);
}
static void Close(MyTcpListener? listener)
{
    if (listener == null) return;
    if (listener.IsListened == false) return;
    listener.Stop();
    listener.IsListened = false;
}
static void DisconnectByClientId(MyConcurrentDictionary clientsDic, string clientId)
{
    if (clientsDic.IsEmpty || string.IsNullOrWhiteSpace(clientId)) return;

    clientsDic.TryGetValue(clientId, out var client);
    
    ArgumentNullException.ThrowIfNull(client);

    if (client.Connected == false)
    {
        throw new ArgumentException($"{client.ClientId}: client.Connected is false");
    }

    if (client.Connected)
    {
        client.Close();
    }

    clientsDic.TryRemove(clientId, out _);
}
static void Disconnect(MyConcurrentDictionary clientsDic)
{
    if (clientsDic.IsEmpty) return;

    var keys = clientsDic.Keys;

    foreach(var key in keys)
    {
        if (clientsDic[key].Connected)
        {
            clientsDic[key].Close();
        }
        clientsDic.TryRemove(key, out _);
    }
}

static async Task SendMessageToClientAsync(MyConcurrentDictionary clientsDic, string clientId, string message)
{
    clientsDic.TryGetValue(clientId, out var client);

    ArgumentNullException.ThrowIfNull(client);

    if (client.Connected)
    {
        await SendMessageAsync(client, message);
    }
}
static async Task SendMessageAsync(MyTcpClient? client, string message)
{
    ArgumentNullException.ThrowIfNull(client);

    NetworkStream stream = client.GetStream();
    await Extension.WriteMessageAsync(stream, message);
}


//https://devblogs.microsoft.com/dotnet/system-io-pipelines-high-performance-io-in-net/
static async Task ProcessLineAsync(NetworkStream stream)
{
    byte[] buffer = new byte[1024];
    var bytesBuffered = 0;
    var bytesConsumed = 0;

    while (true)
    {
        var bytesRead = await stream.ReadAsync(buffer, bytesBuffered, buffer.Length - bytesBuffered);

        if (bytesRead == 0) { break; } // EOF

        // Keep track of the amount of buffered bytes
        bytesBuffered += bytesRead;

        var linePosition = -1;

        do
        {
            // Look for EOF in the buffered data
            linePosition = Array.IndexOf(buffer, (byte)'\n', bytesConsumed, bytesBuffered - bytesConsumed);

            if (linePosition > 0)
            {
                // Calculate the length of the line based on the offset
                var lineLength = linePosition - bytesConsumed;

                // Process the line
                // ProcessLine(buffer, bytesConsumed, lineLength)

                // Move the bytesConsumed to skip the past line we consumed (including \n)
                bytesConsumed += lineLength + 1;
            }

        } while (linePosition >= 0);

    }

}

public class MyTcpListener : TcpListener
{
    public MyTcpListener(IPAddress localaddr, int port) : base(localaddr, port)
    {
    }
    public bool IsListened { get; set; }
}

public class MyTcpClient
{
    private readonly TcpClient _tcpClient;
    public string ClientId { get; set; } = Guid.NewGuid().ToString();

    public MyTcpClient(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;
    }
 
    public NetworkStream GetStream() => _tcpClient.GetStream();
    public void Close() => _tcpClient.Close();
    public bool Connected => _tcpClient.Connected;

    public bool IsSocketConnected()
    {
        try
        {
            
            return (_tcpClient.Client.Poll(1, SelectMode.SelectRead) && _tcpClient.Client.Available == 0) == false;
        }
        catch
        {
            return false;
        }
    }
}

public class MyConcurrentDictionary
{
    private readonly ConcurrentDictionary<string, MyTcpClient> _dictionary = new();
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out MyTcpClient value)
    {
        return _dictionary.TryGetValue(key, out value);
    }

    public MyTcpClient this[string key]
    {
        get => _dictionary[key];
        //set => _dictionary[key] = value;
    }
    public ICollection<string> Keys => _dictionary.Keys;
    public bool IsEmpty => _dictionary.IsEmpty;
    public bool TryAdd(string key, MyTcpClient value, CancellationToken token)
    {
        bool added = _dictionary.TryAdd(key, value);

        if(added)
        {
            Extension.CreateReceiveTask(value, token);
            CreateConnectionCheckTask(value, token);
        }
        return added;
    }

    public bool TryRemove(string key, out MyTcpClient value)
    {
        bool removed = _dictionary.TryRemove(key, out value);
        if (removed)
        {

        }
        return removed;
    }

    Task CreateConnectionCheckTask(MyTcpClient client, CancellationToken token)
    {
        var task = Task.Run(async () =>
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }
                if (client.IsSocketConnected() == false)
                {
                    Console.WriteLine($"[Server] Client {client.ClientId} disconnected.");
                    _dictionary.TryRemove(client.ClientId, out _);
                    break;
                }
                await Task.Delay(1000);
            }
        }, token);

        return task;
    }
}

public static class Extension
{

    public static Task CreateReceiveTask(MyTcpClient client, CancellationToken token)
    {
        var task = Task.Run(async () =>
        {
            while (token.IsCancellationRequested == false)
            {
                var (clientId, message) = await ReceiveMessagesFromClientsAsync(client);
                if (!string.IsNullOrEmpty(message))
                {
                    Console.WriteLine($"Received from {clientId}: {message}");
                }
            }
        }, token);

        return task;

    }
    public static async Task<(string, string)> ReceiveMessagesFromClientsAsync(MyTcpClient client)
    {

        if (client.Connected)
        {
            byte[] buffer = new byte[1024];
            var clientMessage = await ReceiveMessageAsync(client, buffer);
            return (client.ClientId, clientMessage);
        }
        return (string.Empty, string.Empty);

    }

    public static async Task<string> ReceiveMessageAsync(MyTcpClient? client, byte[] buffer)

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
        return string.Empty;
    }

    public static async Task WriteMessageAsync(NetworkStream? stream, string message)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] data = Encoding.UTF8.GetBytes(message);
        await stream.WriteAsync(data);
    }
}
// 如果傳輸過來的字串大於1024 => 當buffer長度=1024 這樣是否會覆蓋過去原本在 buffer 的字串