using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataService.ModbusTCP
{
    internal class ModbusTcpListener : System.Net.Sockets.TcpListener
    {

        public ModbusTcpListener(IPAddress localaddr, int port) : base(localaddr, port)
        {
        }

        public bool IsActive => Active;
    }
}
