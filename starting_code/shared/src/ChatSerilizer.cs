using System;
using System.Collections.Generic;
using System.Text;
using shared;

namespace shared
{
    public class ChatSerilizer : ISerializable
    {
        public int avatarId;
        public string text;

        public ChatSerilizer() { }
        public ChatSerilizer(int avatarId, string text)
        {
            this.avatarId = avatarId;
            this.text = text;
        }

        public void Serialize(Packet p)
        {
            p.Write(avatarId);
            p.Write(text);
        }

        public void Deserialize(Packet p)
        {
            avatarId = p.ReadInt();
            text = p.ReadString();
        }
    }
}

