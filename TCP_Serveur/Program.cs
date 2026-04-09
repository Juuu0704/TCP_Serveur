using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System;
using System.Security.Cryptography;

class Program
{
    static readonly object _lock = new();
    static Socket? listener;
    static bool isRunning;

    static void Log(string message)
    {
        lock (_lock)
        {
            File.AppendAllText("log.txt",
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
        }
    }

    // Clé secrète (à stocker ailleurs en prod)
    static string secretKey = "CLEE_VR@1M3NT_SECRETE";


    static string Sign(string data, string secretKey)      //!!!!!!!!!!!!!!!!!!changer secretKey (dans un fichier pour plus de sécurité)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(data))
                throw new ArgumentException("Data invalide.");

            if (string.IsNullOrWhiteSpace(secretKey))      //!!!!!!!!!!!!!!!!!!changer secretKey (dans un fichier pour plus de sécurité)
                throw new ArgumentException("Clé secrète invalide.");

            var keyBytes = Encoding.UTF8.GetBytes(secretKey);      //!!!!!!!!!!!!!!!!!!changer secretKey (dans un fichier pour plus de sécurité)
            var dataBytes = Encoding.UTF8.GetBytes(data);

            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hash = hmac.ComputeHash(dataBytes);
                return Convert.ToBase64String(hash);
            }
        }
        catch (Exception ex)
        {
            Log($"Erreur lors de la signature : {ex.Message}");
            return null; // ou throw;
        }
    }

    static bool VerifyCard(string card, string secretKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(card))
                throw new ArgumentException("Carte invalide.");

            if (string.IsNullOrWhiteSpace(secretKey))      //!!!!!!!!!!!!!!!!!!changer secretKey (dans un fichier pour plus de sécurité)
                throw new ArgumentException("Clé secrète invalide.");

            var parts = card.Split(';');

            if (parts.Length != 3)
                return false;

            string payload = parts[0] + ";" + parts[1];
            string signature = parts[2];

            string expectedSignature = Sign(payload, secretKey);      //!!!!!!!!!!!!!!!!!!changer secretKey (dans un fichier pour plus de sécurité)

            if (expectedSignature == null)
                return false;

            byte[] sigBytes;
            byte[] expectedBytes;

            try
            {
                sigBytes = Convert.FromBase64String(signature);
                expectedBytes = Convert.FromBase64String(expectedSignature);
            }
            catch (FormatException)
            {
                // Base64 invalide
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(sigBytes, expectedBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la vérification : {ex.Message}");
            return false;
        }
    }

    static void Stop()
    {
        if (!isRunning) return;

        isRunning = false;
        Log("Arrêt du serveur...");

        try { listener?.Close(); } catch { }

        Log("Serveur arrêté");
    }

    [STAThread]
    static void Main()
    {
        // Arrêt propre quand Windows ferme l'application
        AppDomain.CurrentDomain.ProcessExit += (s, e) => Stop();

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            Log($"FATAL: {e.ExceptionObject}");

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log($"TASK ERROR: {e.Exception}");
            e.SetObserved();
        };

        // Lance le serveur en arrière-plan
        _ = StartServerAsync();

        // Empêche le programme de se terminer
        Thread.Sleep(Timeout.Infinite);
    }

    static async Task StartServerAsync()
    {
        int port = 1234;
        isRunning = true;

        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, port);

        listener = new Socket(
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        listener.Bind(ipEndPoint);
        listener.Listen(2);

        Log($"Serveur démarré sur le port {port}");

        while (isRunning)
        {
            try
            {
                Socket handler = await listener.AcceptAsync();
                Log($"Client connecté : {handler.RemoteEndPoint}");
                _ = HandleClientAsync(handler);
            }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { Log($"Erreur accept : {ex.Message}"); }
        }
    }

    static async Task HandleClientAsync(Socket handler)
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (isRunning)
            {
                int received = await handler.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    SocketFlags.None);

                if (received == 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, received);

                if (VerifyCard(message, secretKey))
                {
                    Log($"QR Code valide. Message reçu : {message}");
                }
                else { 
                    Log($"Erreur : QR Code non valide !");
                }

                byte[] ackBytes = Encoding.UTF8.GetBytes("Résultat reçu");
                await handler.SendAsync(new ArraySegment<byte>(ackBytes), SocketFlags.None);
            }
        }
        catch (Exception ex) { Log($"Erreur client : {ex}"); }
        finally
        {
            Log("Client déconnecté");
            try { handler.Shutdown(SocketShutdown.Both); } catch { }
            handler.Close();
        }
    }
}