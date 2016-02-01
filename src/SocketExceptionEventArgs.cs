using System;

namespace OG.TcpSocket
{
    public class SocketExceptionEventArgs: EventArgs
    {
        public Exception SocketException { get; set; }
    }
}
