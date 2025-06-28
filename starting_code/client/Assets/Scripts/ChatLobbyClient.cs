using shared;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

public class ChatLobbyClient : MonoBehaviour
{
    private AvatarAreaManager _avatarAreaManager;
    private PanelWrapper _panelWrapper;
    [SerializeField] private string _server = "localhost";
    [SerializeField] private int _port = 55555;

    private TcpClient _client;
    private int localId = -1;
    private bool _avatarsInitialized = false;


    private void Start()
    {
        ConnectToServer();

        _avatarAreaManager = FindFirstObjectByType<AvatarAreaManager>();
        _panelWrapper = FindFirstObjectByType<PanelWrapper>();

        _avatarAreaManager.OnAvatarAreaClicked += OnAvatarAreaClicked;
        _panelWrapper.OnChatTextEntered += OnChatTextEntered;
    }

    private void ConnectToServer()
    {
        try
        {
            _client = new TcpClient(_server, _port);
            Debug.Log("Connected to server.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not connect: {e.Message}");
        }
    }

    private void OnChatTextEntered(string text)
    {
        _panelWrapper.ClearInput();

        if (text.Equals("/Setkin", StringComparison.InvariantCultureIgnoreCase))
        {
            SendMessageToServer(new ChangeSkinSerilizer());
            return;
        }

        SendMessageToServer(new CommandSerilizer(text));
    }

    private void OnAvatarAreaClicked(Vector3 pos)
    {
        SendMessageToServer(new MoveAvatar(pos.x, pos.y, pos.z));
    }

private void SendMessageToServer(ISerializable msg)
{
    if (_client == null || !_client.Connected)
    {
        Debug.LogWarning("Tried to send message while not connected.");
        return;
    }

    try
    {
        var packet = new Packet();
        packet.Write(msg);
        StreamUtil.Write(_client.GetStream(), packet.GetBytes());
    }
    catch (Exception e)
    {
        Debug.LogError($"Send failed: {e.Message}");
        _client.Close();
        ConnectToServer(); 
    }
}


    private void Update()
    {
        try
        {
            if (_client.Available > 0)
            {
                var inBytes = StreamUtil.Read(_client.GetStream());
                if (inBytes == null) return;

                var packet = new Packet(inBytes);
                var obj = packet.ReadObject();

                switch (obj)
                {
                    case ClientIdSerilizer assign:
                        localId = assign.id;
                        Debug.Log($"This client is avatar #{localId}");
                        break;

                    case AvatarList list:
                        HandleAvatarList(list);
                        Debug.Log("List Updated :)");
                        break;

                    case ChatSerilizer chat:
                        HandleChat(chat);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Receive failed: {e.Message}");
            _client.Close();
            ConnectToServer();
        }
    }

        private void HandleAvatarList(AvatarList msg)
        {
            if (localId == -1)
            {
                Debug.LogWarning("Received AvatarList before localId was assigned. Ignoring.");
                return;
            }

            var newIds = new HashSet<int>();
            foreach (var a in msg.avatars)
                newIds.Add(a.id);

            // Remove avatars not present in the new list (except local player)
            foreach (int existing in _avatarAreaManager.GetAllAvatarIds())
            {
                if (!newIds.Contains(existing) && existing != localId)
                {
                    Debug.Log($"Removing avatar view for id {existing}");
                    _avatarAreaManager.RemoveAvatarView(existing);
                }
            }

            // Add or update avatars
            foreach (var a in msg.avatars)
            {
                var pos = new Vector3(a.x, a.y, a.z);
                AvatarView view;

                if (_avatarAreaManager.HasAvatarView(a.id))
                {
                    view = _avatarAreaManager.GetAvatarView(a.id);
                    view.Move(pos);
                    view.SetSkin(a.skin);
                }
                else
                {
                    view = _avatarAreaManager.AddAvatarView(a.id);
                    view.transform.localPosition = pos;
                    view.SetSkin(a.skin);
                }

                bool me = (a.id == localId);
                view.WhereMe(me);
            }

            _avatarsInitialized = true;
        }


        private void HandleChat(ChatSerilizer chat)
        {
            if (!_avatarsInitialized)
            {
                Debug.LogWarning($"Chat received before avatars were initialized: {chat.text}");
                return;
            }

            if (_avatarAreaManager.HasAvatarView(chat.avatarId))
                _avatarAreaManager.GetAvatarView(chat.avatarId).Say(chat.text);
            else
                Debug.LogWarning($"Chat for unknown avatar {chat.avatarId}");
        }

}