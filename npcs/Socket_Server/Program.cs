using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


MyTcpListener? listener = null;
IDictionary<string, MyTcpClient> clientsDic = new Dictionary<string, MyTcpClient>();

listener = Open(listener);
CancellationTokenSource cts = new();
Task receiveTask = CreateReceiveTask(clientsDic, cts.Token);


while (true)
{
    await AcceptAsync(listener, clientsDic);

    string input = Console.ReadLine() ?? string.Empty;

    if (input.Length == 1 && input.ToLower() == "d") // disconnect
    {
        Disconnect(clientsDic);
        cts.Cancel();
        Close(listener);
    }
    else if (input.Length == 1 && input.ToLower() == "r") // reconnect
    {
        cts.Cancel();
        listener = Open(listener);

        cts = new();
        receiveTask = CreateReceiveTask(clientsDic, cts.Token);
    }
    else if (input.StartsWith("send")) // send message to a client
    {
        var parts = input.Split(' ');
        if (parts.Length == 3)
        {
            string clientId = parts[1];
            string message = parts[2];
            await SendMessageToClientAsync(clientsDic, clientId, message);
        }
    }
}

static Task CreateReceiveTask(IDictionary<string, MyTcpClient> clientsDic, CancellationToken token)
{
    var receiveTask = Task.Run(async () =>
    {
        while (token.IsCancellationRequested == false)
        {
            await foreach (var (clientId, message) in ReceiveMessagesFromClientsAsync(clientsDic))
            {
                Console.WriteLine($"Received from {clientId}: {message}");
            }
        }
    }, token);

    return receiveTask;
}

static async Task AcceptAsync(MyTcpListener? listener, IDictionary<string, MyTcpClient> clientsDic)
{
    if (listener == null) return;
    if (listener.IsListened == false) return;
    //if (listener.Pending() == false) return;

    MyTcpClient newClient = new(await listener.AcceptTcpClientAsync());
    bool isOk = clientsDic.TryGetValue(newClient.ClientId, out _);
    
    // 測試重複的client 是否排除
    if (isOk) return;

    clientsDic.Add(newClient.ClientId, newClient);
    await SendMessageToClientAsync(clientsDic, newClient.ClientId, newClient.ClientId);
    Console.WriteLine("Client connected...");

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
static void DisconnectByClientId(IDictionary<string, MyTcpClient> clientsDic, string clientId)
{
    if (clientsDic.Any() == false || string.IsNullOrWhiteSpace(clientId)) return;

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

    clientsDic.Remove(clientId);
}
static void Disconnect(IDictionary<string, MyTcpClient> clientsDic)
{
    if (clientsDic.Any() == false) return;

    var keys = clientsDic.Keys;

    foreach(var key in keys)
    {
        if (clientsDic[key].Connected)
        {
            clientsDic[key].Close();
        }
        clientsDic.Remove(key);
    }
}

static async Task SendMessageToClientAsync(IDictionary<string, MyTcpClient> clientsDic, string clientId, string message)
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
    await WriteMessageAsync(stream, message);
}

static async IAsyncEnumerable<(string, string)> ReceiveMessagesFromClientsAsync(IDictionary<string, MyTcpClient> clientsDic)
{
    foreach (var clientKV in clientsDic)
    {
        byte[] buffer = new byte[1024];

        if (clientKV.Value.Connected)
        {
            await foreach (var clientMessage in ReceiveMessageAsync(clientKV.Value, buffer))
            {
                yield return (clientKV.Value.ClientId, clientMessage);
            }
        }
    }
}

static async IAsyncEnumerable<string> ReceiveMessageAsync(MyTcpClient? client, byte[] buffer)
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
    ArgumentNullException.ThrowIfNull(stream);
    byte[] data = Encoding.UTF8.GetBytes(message);
    await stream.WriteAsync(data);
    //await stream.FlushAsync();
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
}




// IO multiplexing, Select/Poll/native epoll/IOCP/kqueue
// 如果傳輸過來的字串大於1024 => 當buffer長度=1024 這樣是否會覆蓋過去原本在 buffer 的字串
// multi threading clients => concurrent dictionary
