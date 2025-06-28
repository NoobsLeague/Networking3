using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using shared;

class TCPServerSample
{
    private TcpListener _listener;
    private List<TcpClient> _clients = new List<TcpClient>();
    private Dictionary<TcpClient, AvatarInfo> _clientAvatars = new Dictionary<TcpClient, AvatarInfo>();
    private int _nextAvatarId = 0;
    private Random _random = new Random();

    private const float MaxRadius = 20f;
    private const float WhisperRange = 3f;

    public static void Main(string[] args)
    {
        new TCPServerSample().Run();
    }

    private void Run()
    {
        Console.WriteLine("Server started on port 55555");
        _listener = new TcpListener(IPAddress.Any, 55555);
        _listener.Start();

        while (true)
        {
            ProcessNewClients();
            ProcessExistingClients();
            Thread.Sleep(100);
        }
    }

    private void ProcessNewClients()
    {
        while (_listener.Pending())
        {
            var client = _listener.AcceptTcpClient();
            _clients.Add(client);

            int id = _nextAvatarId++;
            int skin = _random.Next(0, 10);
            double angle = _random.NextDouble() * Math.PI * 2;
            double distance = _random.NextDouble() * 10;
            float x = (float)(Math.Cos(angle) * distance);
            float z = (float)(Math.Sin(angle) * distance);
            float y = 0;

            var avatar = new AvatarInfo(id, skin, x, y, z);
            _clientAvatars[client] = avatar;
            Console.WriteLine($"Client {id} connected.");

            var assign = new ClientIdSerilizer(id);
            var pAssign = new Packet();
            pAssign.Write(assign);

            // Safe send for new client assignment
            if (!SafeSendToClient(client, pAssign.GetBytes()))
            {
                // If we can't even send the initial assignment, disconnect immediately
                DisconnectClient(client);
                return;
            }

            // Send full avatar list to everyone when new client connects
            BroadcastAvatarList();
        }
    }

    private void ProcessExistingClients()
    {
        for (int i = _clients.Count - 1; i >= 0; i--)
        {
            var client = _clients[i];
            try
            {
                var sock = client.Client;

                // Enhanced disconnection detection
                if (!IsClientConnected(client))
                {
                    DisconnectClient(client);
                    continue;
                }

                if (client.Available == 0) continue;

                byte[] data = StreamUtil.Read(client.GetStream());
                if (data == null)
                {
                    DisconnectClient(client);
                    continue;
                }

                var packet = new Packet(data);
                var obj = packet.ReadObject();

                switch (obj)
                {
                    case CommandSerilizer creq:
                        HandleChatRequest(client, creq);
                        break;
                    case MoveAvatar mreq:
                        HandleMoveRequest(client, mreq);
                        break;
                    case ChangeSkinSerilizer sreq:
                        HandleSkinRequest(client);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Server exception: " + e.Message);
                DisconnectClient(client);
            }
        }
    }

    // Enhanced connection checking
    private bool IsClientConnected(TcpClient client)
    {
        try
        {
            if (client == null || !client.Connected)
                return false;

            var sock = client.Client;

            // Poll for connection status
            if (sock.Poll(0, SelectMode.SelectRead) && sock.Available == 0)
                return false;

            // Additional check: try to peek at the socket
            if (sock.Poll(0, SelectMode.SelectError))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    // Safe method to send data to a specific client with error handling
    private bool SafeSendToClient(TcpClient client, byte[] data)
    {
        try
        {
            if (!IsClientConnected(client))
                return false;

            StreamUtil.Write(client.GetStream(), data);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to send to client: {e.Message}");
            return false;
        }
    }

    private void BroadcastAvatarList()
    {
        var msg = new AvatarList(new List<AvatarInfo>(_clientAvatars.Values));
        var packet = new Packet();
        packet.Write(msg);
        var bytes = packet.GetBytes();

        // Keep track of clients to disconnect due to send failures
        var clientsToDisconnect = new List<TcpClient>();

        foreach (var client in _clients)
        {
            if (!SafeSendToClient(client, bytes))
            {
                clientsToDisconnect.Add(client);
            }
        }

        // Disconnect failed clients
        foreach (var client in clientsToDisconnect)
        {
            DisconnectClient(client);
        }
    }

    // IMPROVED: Safe broadcast for chat messages
    private void SafeBroadcastToClients(byte[] data, List<TcpClient> targetClients = null)
    {
        var targets = targetClients ?? _clients;
        var clientsToDisconnect = new List<TcpClient>();

        foreach (var client in targets)
        {
            if (!SafeSendToClient(client, data))
            {
                clientsToDisconnect.Add(client);
            }
        }

        // Disconnect failed clients
        foreach (var client in clientsToDisconnect)
        {
            DisconnectClient(client);
        }
    }

    private void HandleChatRequest(TcpClient client, CommandSerilizer creq)
    {
        if (!_clientAvatars.TryGetValue(client, out var avatar)) return;

        string text = creq.text;
        bool whisper = false;
        const string prefix = "/whisper ";

        if (text.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
        {
            whisper = true;
            text = text.Substring(prefix.Length);
        }

        var chat = new ChatSerilizer(avatar.id, text);
        var pChat = new Packet();
        pChat.Write(chat);
        var bytes = pChat.GetBytes();

        if (whisper)
        {
            // Build list of clients in whisper range
            var whisperTargets = new List<TcpClient>();

            foreach (var other in _clients)
            {
                if (_clientAvatars.TryGetValue(other, out var o))
                {
                    float dx = o.x - avatar.x, dz = o.z - avatar.z;
                    if (Math.Sqrt(dx * dx + dz * dz) <= WhisperRange)
                        whisperTargets.Add(other);
                }
            }

            // Use safe broadcast for whisper
            SafeBroadcastToClients(bytes, whisperTargets);
        }
        else
        {
            // Use safe broadcast for normal chat
            SafeBroadcastToClients(bytes);
        }
    }

    private void HandleMoveRequest(TcpClient client, MoveAvatar mreq)
    {
        if (!_clientAvatars.TryGetValue(client, out var avatar)) return;

        float nx = mreq.x, nz = mreq.z, ny = mreq.y;
        if (Math.Sqrt(nx * nx + nz * nz) <= MaxRadius)
        {
            avatar.x = nx; avatar.y = ny; avatar.z = nz;
            BroadcastAvatarList();
        }
        else
        {
            Console.WriteLine($"Client {avatar.id} illegal move to ({nx},{nz})");
        }
    }

    private void HandleSkinRequest(TcpClient client)
    {
        if (!_clientAvatars.TryGetValue(client, out var avatar)) return;

        avatar.skin = _random.Next(0, 1000);
        BroadcastAvatarList();
    }

    private void DisconnectClient(TcpClient client)
    {
        if (_clientAvatars.TryGetValue(client, out var avatar))
            Console.WriteLine($"Client {avatar.id} disconnected.");
        else
            Console.WriteLine("Unknown client disconnected.");

        _clients.Remove(client);
        _clientAvatars.Remove(client);

        try
        {
            client.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error closing client connection: {e.Message}");
        }

        // Send full avatar list when someone disconnects
        if (avatar != null)
        {
            BroadcastAvatarList();
        }
    }
}