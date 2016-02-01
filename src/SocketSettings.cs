using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace OG.TcpSocket
{
    public static class SocketSettings
    {
        public static int PortNumber { get; set; }
        public static IPAddress IP { get; set; }
        public static char EOL { get; set; }

        public static int CurrentCommandNumber { get; private set; }
    }
}
