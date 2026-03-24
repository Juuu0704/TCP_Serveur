using System.Net;
using System.Net.Sockets;
using System.Text;

IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 12345);       //ecoute sur le port 12345, peu importe d'où vient la connexion.

using Socket listener = new(
    ipEndPoint.AddressFamily,
    SocketType.Stream,
    ProtocolType.Tcp);

listener.Bind(ipEndPoint);
listener.Listen(2);

var handler = await listener.AcceptAsync();
while (true)
{
    //Receive message.
    byte[] buffer = new byte[1_024];
    int received = await handler.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
    var reponse = Encoding.UTF8.GetString(buffer, 0, received);

    var eom = "<|EOM|>";
    if (reponse.IndexOf(eom) > -1 /*is end of message  */)
    {
        Console.WriteLine(
            $"Socket server received message : \" {reponse.Replace(eom, "")}\"");
        var ackMessage = "<|ACK|>";
        var echoBytes = Encoding.UTF8.GetBytes(ackMessage);
        await handler.SendAsync(echoBytes, 0);
        Console.WriteLine(
            $"Socket server sent acknowledgment : \"{ackMessage}\"");
        break;
    }
    // Sample output :
    //      Socket server received message : "Hi friends !"
    //      Socket server sent acknowledgment : "<|ACK|>"
}