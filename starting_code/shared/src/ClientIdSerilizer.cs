using System;
using System.Collections.Generic;
using System.Text;

namespace shared
{
    public class ClientIdSerilizer : ISerializable
    {
        public int id;
        public ClientIdSerilizer() { }

        public ClientIdSerilizer(int id)
        {
            this.id = id;
        }

        public void Serialize(Packet p)
        {
            p.Write(id);
        }

        public void Deserialize(Packet p)
        {
            id = p.ReadInt();
        }
    }
}

