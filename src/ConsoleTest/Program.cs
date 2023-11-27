using System.Text;
using TouchSocketSlim.Core;
using TouchSocketSlim.Sockets;

var service = new TcpService(() => new FixedHeaderPackageAdapter(), 61000);

service.Connected = (socketClient, _) =>
{
    Console.WriteLine($"[Server] Client connected, Id {socketClient.Id}");
    var data = "Hello world!"u8.ToArray();
    socketClient.Send(data, 0, data.Length);
};

service.Received = (socketClient, buffer, offset, length) =>
{
    Console.WriteLine($"[Server] Service received, client id: {socketClient.Id}, context: {Encoding.UTF8.GetString(buffer.AsSpan(offset, length))}");
};

service.Start();

var client = new TcpClient(new TcpClientConfig("127.0.0.1:61000", () => new FixedHeaderPackageAdapter()));

await Task.Delay(1000);

client.Connected = (_, _) =>
{
    Console.WriteLine("[Client] Connected to server.");
};

client.Received = (_, buffer, offset, length) =>
{
    Console.WriteLine($"[Client] Client received: {Encoding.UTF8.GetString(buffer.AsSpan(offset, length))}");
};

client.Connect(1000);
var data = "TouchSocketSlim"u8.ToArray();
client.Send(data, 0, data.Length);
Console.ReadLine();