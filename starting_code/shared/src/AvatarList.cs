using System;
using System.Collections.Generic;
using System.Text;

namespace shared
{
    public class AvatarList : ISerializable
    {
        public List<AvatarInfo> avatars = new List<AvatarInfo>();

        public AvatarList() { }
        public AvatarList(List<AvatarInfo> avatars)
        {
            this.avatars = avatars;
        }

        public void Serialize(Packet p)
        {
            p.Write(avatars.Count);
            foreach (var a in avatars)
            {
                p.Write(a);
            }
        }

        public void Deserialize(Packet p)
        {
            int count = p.ReadInt();
            avatars = new List<AvatarInfo>(count);
            for (int i = 0; i < count; i++)
            {
                avatars.Add(p.Read<AvatarInfo>());
            }
        }
    }
}
