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
    
    // Track server positions to avoid false movement triggers
    private Dictionary<int, Vector3> _serverPositions = new Dictionary<int, Vector3>();

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
        // OPTIMISTIC UPDATE: Immediately update our own position before sending to server
        if (localId != -1 && _avatarAreaManager.HasAvatarView(localId))
        {
            var localView = _avatarAreaManager.GetAvatarView(localId);
            localView.Move(pos);
            
            // Update our stored server position to match what we expect the server to have
            _serverPositions[localId] = pos;
            
            Debug.Log($"Optimistic update: Moving local avatar {localId} to {pos}");
        }
        
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
                        Debug.Log("Avatar List Updated");
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

    // DEBUG: Check for avatar ID confusion
    private void HandleAvatarList(AvatarList msg)
    {
        if (localId == -1)
        {
            Debug.LogWarning("Received AvatarList before localId was assigned. Ignoring.");
            return;
        }

        Debug.Log($"=== LOCAL ID = {localId} ===");
        Debug.Log($"Received avatar list with {msg.avatars.Count} avatars:");
        foreach (var a in msg.avatars)
        {
            Debug.Log($"  Avatar ID {a.id} at position ({a.x:F2}, {a.y:F2}, {a.z:F2})");
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
                _serverPositions.Remove(existing);
            }
        }

        // Add or update avatars
        foreach (var a in msg.avatars)
        {
            var serverPos = new Vector3(a.x, a.y, a.z);
            AvatarView view;

            Debug.Log($"\n--- Processing Avatar {a.id} ---");
            Debug.Log($"Is this my avatar? {a.id == localId}");

            if (_avatarAreaManager.HasAvatarView(a.id))
            {
                view = _avatarAreaManager.GetAvatarView(a.id);
                Debug.Log($"Existing avatar {a.id} current position: {view.transform.localPosition}");
                
                // Check if this is our own avatar (local player)
                if (a.id == localId)
                {
                    Debug.Log($"This is MY avatar ({localId}) - handling as local");
                    // For local player, only sync if there's a significant difference
                    if (_serverPositions.ContainsKey(a.id))
                    {
                        Vector3 expectedPos = _serverPositions[a.id];
                        float difference = Vector3.Distance(expectedPos, serverPos);
                        
                        Debug.Log($"Expected: {expectedPos}, Server: {serverPos}, Diff: {difference:F3}");
                        
                        if (difference > 0.1f)
                        {
                            Debug.Log($">>> Server corrected local avatar position");
                            view.Move(serverPos);
                        }
                        else
                        {
                            Debug.Log($">>> Local avatar position OK, no movement needed");
                        }
                    }
                    _serverPositions[a.id] = serverPos;
                }
                else
                {
                    Debug.Log($"This is REMOTE avatar {a.id} - handling as remote");
                    // For other avatars, check if server position actually changed
                    if (_serverPositions.ContainsKey(a.id))
                    {
                        Vector3 lastServerPos = _serverPositions[a.id];
                        float serverMovement = Vector3.Distance(lastServerPos, serverPos);
                        
                        Debug.Log($"Last server pos: {lastServerPos}, New server pos: {serverPos}");
                        Debug.Log($"Server movement distance: {serverMovement:F4}");
                        
                        if (serverMovement > 0.01f)
                        {
                            Debug.Log($">>> REMOTE avatar {a.id} moved - animating to {serverPos}");
                            view.Move(serverPos);
                        }
                        else
                        {
                            Debug.Log($">>> REMOTE avatar {a.id} didn't move - silent sync");
                            view.transform.localPosition = serverPos;
                        }
                    }
                    else
                    {
                        Debug.Log($">>> First time seeing remote avatar {a.id}");
                        view.transform.localPosition = serverPos;
                    }
                    
                    _serverPositions[a.id] = serverPos;
                }
                
                view.SetSkin(a.skin);
            }
            else
            {
                Debug.Log($">>> CREATING new avatar {a.id} at {serverPos}");
                view = _avatarAreaManager.AddAvatarView(a.id);
                view.transform.localPosition = serverPos;
                view.SetSkin(a.skin);
                _serverPositions[a.id] = serverPos;
            }

            bool me = (a.id == localId);
            view.WhereMe(me);
            Debug.Log($"WhereMe({me}) called for avatar {a.id}");
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