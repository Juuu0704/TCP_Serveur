using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

public class TcpServer
{
    // Sert à éviter que plusieurs threads écrivent dans le log en même temps
    private readonly object _lock = new();

    //Socket principal du serveur (écoute des connexions)
    private Socket? listener;

    //Indique si le serveur est en cours de fonctionnement
    private bool isRunning;

    //Fonction de log (écrit dans un fichier texte)
    public void Log(string message)
    {
        lock (_lock)
        {
            File.AppendAllText("log.txt",
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
        }
    }

    //Démarrage du serveur
    public async Task StartAsync(int port)
    {
        // Évite de démarrer 2 fois
        if (isRunning) return;

        isRunning = true;

        //Configure l'adresse IP + port
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, port);

        //Création du socket serveur
        listener = new Socket(
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        //Associe le socket à l'adresse
        listener.Bind(ipEndPoint);

        //Mise en écoute (2 connexions max en attente)
        listener.Listen(2);

        Log($"Serveur démarré sur le port {port}");

        //Lance la boucle d'acceptation en tâche de fond (non bloquante)
        _ = AcceptLoop(); 
    }

    //Arrêt du serveur
    public void Stop()
    {
        // Si déjà arrêté → ne rien faire
        if (!isRunning) return;

        isRunning = false;

        Log("Arrêt du serveur...");

        try 
        {
            //Ferme le socket → débloque AcceptAsync()
            listener?.Close(); 
        } 
        catch { }

        Log("Serveur arrêté");
    }

    //Boucle principale : accepte les clients
    private async Task AcceptLoop()
    {
        while (isRunning)
        {
            try
            {
                // Attend qu’un client se connecte
                Socket handler = await listener!.AcceptAsync();

                Log($"Client connecté : {handler.RemoteEndPoint}");

                //Lance la gestion du client dans une tâche séparée
                _ = HandleClientAsync(handler);
            }
            catch (ObjectDisposedException)
            {
                // serveur arrêté → normal
                break;
            }
            catch (Exception ex)
            {
                Log($"Erreur serveur : {ex}");
            }
        }
    }

    //Gestion d’un client
    private async Task HandleClientAsync(Socket handler)
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (isRunning)
            {
                //Réception des données
                int received = await handler.ReceiveAsync(buffer, SocketFlags.None);

                //Si 0 → client déconnecté
                if (received == 0)
                    break;

                //Conversion des bytes en texte
                string message = Encoding.UTF8.GetString(buffer, 0, received);

                Log($"Message reçu : {message}");

                //Réponse au client
                string response = "Résultat reçu";
                byte[] data = Encoding.UTF8.GetBytes(response);

                await handler.SendAsync(data, SocketFlags.None);
            }
        }
        catch (Exception ex)
        {
            Log($"Erreur client : {ex}");
        }
        finally
        {
            //Nettoyage de la connexion client
            Log("Client déconnecté");

            try 
            {
                // Coupe envoi + réception
                handler.Shutdown(SocketShutdown.Both); 
            } 
            catch { }

            // Ferme le socket
            handler.Close();
        }
    }
}