using System.Text;
using TouchSocketSlim.Sockets;

var client = new TcpClient(new TcpClientConfig("127.0.0.1:61000"));

client.Connected = (tcpClient, eventArgs) =>
{
    Console.WriteLine("Connected");
};

client.Received = (tcpClient, eventArgs) =>
{
    Console.WriteLine($"Received: {Encoding.UTF8.GetString(eventArgs.ByteBlock.Buffer)}");
};

client.Connect(1000);
var data = "TouchSocketSlim"u8.ToArray();
client.Send(data, 0, data.Length);

Console.ReadLine();