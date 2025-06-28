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

            var avatar = new AvatarInfo(id, skin, x, y ,z);
            _clientAvatars[client] = avatar;
            Console.WriteLine($"Client {id} connected.");

            var assign = new ClientIdSerilizer(id);
            var pAssign = new Packet();
            pAssign.Write(assign);
            StreamUtil.Write(client.GetStream(), pAssign.GetBytes());

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
                // TODO (maybe): uncomment this:
                if (sock.Poll(0, SelectMode.SelectRead) && sock.Available == 0)
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

        //whisper
        if (whisper)
        {
            foreach (var other in _clients)
            {
                if (_clientAvatars.TryGetValue(other, out var o))
                {
                    float dx = o.x - avatar.x, dz = o.z - avatar.z;
                    if (Math.Sqrt(dx * dx + dz * dz) <= WhisperRange)
                        StreamUtil.Write(other.GetStream(), bytes);
                }
            }
        }
        else
        {
            foreach (var c in _clients)
                StreamUtil.Write(c.GetStream(), bytes);
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
        client.Close();
        BroadcastAvatarList();
    }

    private void BroadcastAvatarList()
    {
        var msg = new AvatarList(new List<AvatarInfo>(_clientAvatars.Values));
        var packet = new Packet();
        packet.Write(msg);
        var bytes = packet.GetBytes();

        foreach (var c in _clients)
            StreamUtil.Write(c.GetStream(), bytes);
    }
}
// in BroadcastAvatarList:
// What happens if client 3 is disconnected (and polling hasn't detected it yet)
//  what will client 2 and 4 see?