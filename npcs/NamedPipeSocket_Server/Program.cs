using System.IO.Pipes;
using System.Text;

//Windows Named Pipes
using var server = new NamedPipeServerStream("mypipe", PipeDirection.InOut, 1);
Console.WriteLine("wait for client...");
await server.WaitForConnectionAsync();
Console.WriteLine("client connected !");

byte[] buffer = new byte[256];
int bytesRead = await server.ReadAsync(buffer);
var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
Console.WriteLine($"message from client: {message}");

string response = "Hello from Server!";
await server.WriteAsync(Encoding.UTF8.GetBytes(response));
//await server.FlushAsync();
;