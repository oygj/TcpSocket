using System;

namespace OG.TcpSocket
{
    public class DataReceivedEventArgs : EventArgs
    {
        public string Message { get; set; }
    }
}
