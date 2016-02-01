using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OG.TcpSocket
{
    public class SocketAsync
    {
        private int portNumber = 6100;
#if (DEBUG)
    private IPAddress ipAddress = IPAddress.Parse("10.74.191.59");
#else
    private IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
#endif
        private readonly SynchronizationContext syncContext;

        private char terminator = char.MinValue;    //null terminator
        private Socket clientSocket;
        private int commandCounter = 0;

        private ManualResetEvent connectDone = new ManualResetEvent(false);
        private ManualResetEvent sendDone = new ManualResetEvent(false);
        private ManualResetEvent receiveDone = new ManualResetEvent(false);
        private ManualResetEvent disconnectDone = new ManualResetEvent(false);

        #region Events
        public event EventHandler clientSocketConnected;
        public event EventHandler<DataReceivedEventArgs> clientSocketDataReceived;
        public event EventHandler clientSocketDisconnected;
        public event EventHandler<SocketExceptionEventArgs> clientSocketError;
        

        #endregion

        #region Constructors

        public SocketAsync() 
        {
            this.syncContext = AsyncOperationManager.SynchronizationContext;
        }

        public SocketAsync(IPAddress ipAddress, int portNumber)
        {
            this.syncContext = AsyncOperationManager.SynchronizationContext;

            this.HostIpAddress = ipAddress;
            this.HostPortNumber = portNumber;
        }

        //public AsynchronousSocket(Type SocketSettings)
        //{
        //    this.syncContext = AsyncOperationManager.SynchronizationContext;

        //    this.ipAddress = SocketSettings.IP;
        //    this.HostPortNumber = SocketSettings.PortNumber;
        //}

        #endregion

        #region Properties

        public IPAddress HostIpAddress
        {
            get
            {
                return ipAddress;
            }
            set
            {
                ipAddress = value;
            }
        }
        public int HostPortNumber
        {
            get
            {
                return portNumber;
            }
            set
            {
                portNumber = value;
            }
        }
        public bool IsConnected 
        {
            get { return clientSocket.Connected; }
        }
        public int CurrentCommandNumber
        {
            get
            {
                return commandCounter;

            }
        }

        #endregion

        #region Connect

        public void Connect()
        {
            try
            {
                if (clientSocket == null)
                    clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                
                IPEndPoint _ep = new IPEndPoint(this.HostIpAddress, this.HostPortNumber);
                clientSocket.BeginConnect(_ep, new AsyncCallback(OnSocketConnected), clientSocket);

                connectDone.WaitOne();
            }
            catch (Exception exception)
            {
                EventHandler<SocketExceptionEventArgs> exceptionHandler = clientSocketError;
                if (exceptionHandler != null)
                    exceptionHandler(this, new SocketExceptionEventArgs() { SocketException = exception });
            }
        }

        private void OnSocketConnected(IAsyncResult result)
        {
            try
            {
                connectDone.Set();

                //Finalize the connect...
                clientSocket.EndConnect(result);

                //Raise the connected event to any subscribers...
                syncContext.Post(e => this.OnConnect((EventArgs)e), new EventArgs());

                //Start listening for messages...
                BeginReceive();
            }
            catch (Exception exception)
            {
                EventHandler<SocketExceptionEventArgs> exceptionHandler = clientSocketError;
                if (exceptionHandler != null)
                    exceptionHandler(this, new SocketExceptionEventArgs() { SocketException = exception });
            }
        }

        private void OnConnect(EventArgs e)
        {
            if (clientSocketConnected != null)
                clientSocketConnected(this, e);
        }
        #endregion

        #region Close
        public void CloseConnection()
        {
            try
            {
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.BeginDisconnect(true, new AsyncCallback(OnSocketDisconnect), clientSocket);
                disconnectDone.WaitOne();
            }
            catch (Exception exception)
            {
                EventHandler<SocketExceptionEventArgs> exceptionHandler = clientSocketError;
                if (exceptionHandler != null)
                    exceptionHandler(this, new SocketExceptionEventArgs() { SocketException = exception });
            }
        }

        private void OnSocketDisconnect(IAsyncResult result)
        {
            try
            {
                disconnectDone.Set();
                clientSocket.EndDisconnect(result);

                //Raise the disconnect event
                //Not needed since the DataReceived will raise event on disconnect...
                //syncContext.Post(e => this.OnDisconnect((EventArgs)e), new EventArgs());
            }
            catch (Exception exception)
            {
                EventHandler<SocketExceptionEventArgs> exceptionHandler = clientSocketError;
                if (exceptionHandler != null)
                    exceptionHandler(this, new SocketExceptionEventArgs() { SocketException = exception });
            }
        }

        private void OnDisconnect(EventArgs e)
        {
            if (clientSocketDisconnected != null)
                clientSocketDisconnected(this, e);
        }
        #endregion

        #region Send
        public void SendAsync(string command)
        {
            try
            {
                if (clientSocket.Connected)
                {
                    Interlocked.Increment(ref commandCounter);
                    string _command = string.Concat(commandCounter.ToString(), " ", command, terminator);
                    byte[] _commandArray = Encoding.UTF8.GetBytes(_command);

                    clientSocket.BeginSend(_commandArray, 0, _commandArray.Length, SocketFlags.None, new AsyncCallback(OnSendComplete), clientSocket);

                    sendDone.WaitOne();
                }
            }
            catch (Exception exception)
            {
                EventHandler<SocketExceptionEventArgs> exceptionHandler = clientSocketError;
                if (exceptionHandler != null)
                    exceptionHandler(this, new SocketExceptionEventArgs() { SocketException = exception });
            }
        }

        //public string Send(string outCommand)
        //{
        //    Interlocked.Increment(ref commandCounter);
        //    clientSocket.EndReceive();
        //    byte[] _outbuffer = Encoding.UTF8.GetBytes(string.Concat(commandCounter.ToString(), " ", outCommand, terminator));
        //    int _bytesSent = clientSocket.Send(_outbuffer);
        //    byte[] _inBuffer = new byte[1024];
        //    //clientSocket.Blocking
        //    int _bytesReceived = clientSocket.Receive(_inBuffer);
        //    string _outCommand = Encoding.UTF8.GetString(_inBuffer);

        //    return _outCommand;
        //}

        private void OnSendComplete(IAsyncResult result)
        {
            sendDone.Set();
            Console.WriteLine("Send complete");
        }
        #endregion

        #region Receive
        private void BeginReceive()
        {
            SocketStateObject _state = new SocketStateObject();
            _state.workSocket = clientSocket;

            clientSocket.BeginReceive(_state.buffer, 0, SocketStateObject.BufferSize, 0, new AsyncCallback(OnSocketReceived), _state);
            receiveDone.WaitOne();
        }

        private void OnSocketReceived(IAsyncResult result)
        {
            try
            {
                SocketStateObject _state = (SocketStateObject)result.AsyncState;
                int _bytesRead = clientSocket.EndReceive(result);

                if (_bytesRead > 0)
                {
                    _state.sb.Append(Encoding.UTF8.GetString(_state.buffer, 0, _bytesRead));

                    //If more data is available in the readb buffer, call the method again...
                    if (clientSocket.Available > 0)
                    {
                        clientSocket.BeginReceive(_state.buffer, 0, SocketStateObject.BufferSize, 0, new AsyncCallback(OnSocketReceived), _state);
                    }
                    else
                    {
                        if (_state.sb.Length > 1)
                            Console.WriteLine(_state.sb.ToString());

                        //Return the data to the subscribers...
                        var args = new DataReceivedEventArgs() { Message = _state.sb.ToString() };
                        syncContext.Post(e => this.OnDataReceived((DataReceivedEventArgs)e), args);

                        receiveDone.Set();
                        BeginReceive();
                    }
                }
                else
                {
                    syncContext.Post(e => this.OnDisconnect((EventArgs)e), new EventArgs());
                }
            }
            catch (SocketException)
            {

            }
        }

        private void OnDataReceived(DataReceivedEventArgs e)
        {
            if (clientSocketDataReceived != null)
                clientSocketDataReceived(this, e);
        }
        #endregion
    }

  }
