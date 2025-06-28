using System;
using System.Collections.Generic;
using System.Text;

namespace shared
{
    public class CommandSerilizer : ISerializable
    {
        public string text;
        public CommandSerilizer() { }
        public CommandSerilizer(string text) { 
            this.text = text; 
        }
        public void Serialize(Packet p)
        {
           p.Write(text);
        }
        public void Deserialize(Packet p)
        {
           text = p.ReadString();
        } 
    }
}
