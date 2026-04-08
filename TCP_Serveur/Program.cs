using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

class Program
{
    static readonly object _lock = new();

    static void Log(string message)
    {
        lock (_lock)
        {
            File.AppendAllText("log.txt",
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
        }
    }

    static async Task Main()
    {
        int port = 1234;

        bool isRunning = true;

        // Configuration
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, port);

        // Création du socket serveur
        using Socket listener = new Socket(
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Log($"FATAL: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log($"TASK ERROR: {e.Exception}");
            e.SetObserved();
        };

        // Bind + écoute
        listener.Bind(ipEndPoint);
        listener.Listen(2); // 2 connexions en attente max


        // Boucle d'acceptation des clients
        while (isRunning)
        {
            Socket handler = await listener.AcceptAsync();

            Log($"Client connecté : {handler.RemoteEndPoint}");

            // Gestion du client dans une tâche séparée
            _ = HandleClientAsync(handler);
        }

        // Fonction de gestion d'un client
        async Task HandleClientAsync(Socket handler)
        {
            byte[] buffer = new byte[1024];
            //string eom = "<|EOM|>";

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
                    Log($"Message reçu : {message}");


                    // Envoi de l'accusé de réception
                    string ackMessage = "Résultat reçu";
                    byte[] ackBytes = Encoding.UTF8.GetBytes(ackMessage);
                    await handler.SendAsync(new ArraySegment<byte>(ackBytes), SocketFlags.None);

                }
            }

            catch (Exception ex)
            {
                Log($"Erreur client : {ex}");
            }

            finally
            {
                Log("Client déconnecté");
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
        }
    }
}
