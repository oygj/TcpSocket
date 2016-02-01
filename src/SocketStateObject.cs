using System.Text;

namespace OG.TcpSocket
{
    class SocketStateObject
    {
        public System.Net.Sockets.Socket workSocket = null;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder sb = new StringBuilder();

    }
}
