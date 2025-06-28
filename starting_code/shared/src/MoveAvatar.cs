using System;
using System.Collections.Generic;
using System.Text;

namespace shared
{
    public class MoveAvatar : ISerializable
    {
        public float x, y, z;

        public MoveAvatar() { }
        public MoveAvatar(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public void Serialize(Packet p)
        {
            p.Write(x);
            p.Write(y);
            p.Write(z);
        }

        public void Deserialize(Packet p)
        {
            x = p.ReadFloat();
            y = p.ReadFloat();
            z = p.ReadFloat();
        }
    }
}

