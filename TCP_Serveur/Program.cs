using System.Net;
using System.Net.Sockets;
using System.Text;

// Configuration
IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 1234);

// Création du socket serveur
using Socket listener = new Socket(
    ipEndPoint.AddressFamily,
    SocketType.Stream,
    ProtocolType.Tcp);

// Bind + écoute
listener.Bind(ipEndPoint);
listener.Listen(10); // 10 connexions en attente max

Console.WriteLine($"Serveur TCP démarré sur le port 1234...");

// Boucle d'acceptation des clients
while (true)
{
    Console.WriteLine("En attente d'un client...");
    Socket handler = await listener.AcceptAsync();

    Console.WriteLine($"Client connecté : {handler.RemoteEndPoint}");

    // Gestion du client dans une tâche séparée
    _ = Task.Run(() => HandleClientAsync(handler));
}

// Fonction de gestion d'un client
async Task HandleClientAsync(Socket handler)
{
    byte[] buffer = new byte[1024];
    string eom = "<|EOM|>";

    try
    {
        while (true)
        {
            // Réception du message
            int received = await handler.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                SocketFlags.None);

            // Client déconnecté
            if (received == 0) break;

            string message = Encoding.UTF8.GetString(buffer, 0, received);
            Console.WriteLine($"Message reçu : \"{message.Replace(eom, "")}\"");

            // Vérifier fin de message
            if (message.Contains(eom))
            {
                // Envoi de l'accusé de réception
                string ackMessage = "<|ACK|>";
                byte[] ackBytes = Encoding.UTF8.GetBytes(ackMessage);
                await handler.SendAsync(ackBytes, 0);
                Console.WriteLine($"ACK envoyé : \"{ackMessage}\"");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur client : {ex.Message}");
    }
    finally
    {
        Console.WriteLine($"Client déconnecté.");
        handler.Close();
    }
}