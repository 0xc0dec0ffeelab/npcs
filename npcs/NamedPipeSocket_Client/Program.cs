using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;

using var client = new NamedPipeClientStream(".", "mypipe", PipeDirection.InOut);
await client.ConnectAsync();
Console.WriteLine("server connected");

string message = "Hello from Client!";
await client.WriteAsync(Encoding.UTF8.GetBytes(message));



byte[] buffer = new byte[256];
int bytesRead = await client.ReadAsync(buffer);
message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
Console.WriteLine($"message from server: {message}");
;