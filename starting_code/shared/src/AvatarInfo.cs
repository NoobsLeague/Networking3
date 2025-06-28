using System;
using System.Collections.Generic;
using System.Text;

namespace shared
{
    public class AvatarInfo : ISerializable
    {
        public int id;
        public float x, y, z;
        public int skin;

        public AvatarInfo() { }

        public AvatarInfo(int id, int skin, float x, float y, float z)
        {
            this.id = id;
            this.skin = skin;
            this.x = x;
            this.y = y;
            this.z = z;

        }

        public void Serialize(Packet p)
        {
            p.Write(id);
            p.Write(skin);
            p.Write(x);
            p.Write(y);
            p.Write(z);

        }

        public void Deserialize(Packet p)
        {
            id = p.ReadInt();
            skin = p.ReadInt();
            x = p.ReadFloat();
            y = p.ReadFloat();
            z = p.ReadFloat();
        }
    }
}
