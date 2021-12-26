#region Copyright
// --------------------------------------------------------------------------
// Copyright 2021 Greg Cannon
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// --------------------------------------------------------------------------
#endregion Copyright

namespace IoTDisplay.Desktop
{
    #region Using

    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Timers;
    using Microsoft.IO;

    #endregion Using

    /// <summary>
    /// Socket communication for IoTDisplay SocketDisplayService
    /// </summary>
    public class CommunicationService : IDisposable
    {
        #region Delegates

        public delegate void ChangeStatus(bool connected, string message);
        public delegate void HandleGraphics(Stream graphic, int x, int y, int width, int height);
        public delegate void HandleCommand(string command, string value);

        #endregion Delegates

        #region Constants

        // Buffer size used for communications
        private const int _bufferSize = 25600;
        // Heartbeat timeout in milliseconds
        private const int _beatTimeout = 120000;
        // Size of length area in socket protocol
        private const int _lengthSize = 8;

        #endregion Constants

        #region Fields

        private static readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager = new ();
        private static Timer _beatTimer = new () { AutoReset = false, Enabled = false, Interval = _beatTimeout };
        private readonly ChangeStatus _connectionChanged;
        private readonly HandleGraphics _handleGraphics;
        private readonly HandleCommand _handleCommand;
        private EndPoint _endpoint;
        private ProtocolType _protocolType;
        private Socket _socket;
        private bool _disposed = false;

        #endregion Fields

        #region Constructor / Dispose / Finalizer

        /// <summary>
        /// Communication service constructor
        /// </summary>
        /// <param name="connectionChanged">Delegate to handle connection changed</param>
        /// <param name="handleGraphics">Delegate to handle graphics stream</param>
        /// <param name="handleCommand">Delegate to handle commands</param>
        public CommunicationService(ChangeStatus connectionChanged, HandleGraphics handleGraphics, HandleCommand handleCommand)
        {
            _connectionChanged = connectionChanged;
            _handleGraphics = handleGraphics;
            _handleCommand = handleCommand;
        }

        /// <summary>
        /// Class dispose handler
        /// </summary>
        /// <param name="disposing">Dispose was explicitly called</param>
        protected void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _beatTimer.Enabled = false;
                _beatTimer.Dispose();
                _beatTimer = null;
                Shutdown("Client shutting down.");
                _socket?.Close();
                _socket?.Dispose();
                _socket = null;
            }

            _disposed = true;
        }

        /// <summary>
        /// Dispose of communication service class
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Communication service class finalizer
        /// </summary>
        ~CommunicationService() => Dispose(false);

        #endregion Constructor / Dispose / Finalizer

        #region Methods (Public)

        /// <summary>
        /// Configure communications
        /// </summary>
        /// <param name="socketType">Communications socket type</param>
        /// <param name="host">Socket host string</param>
        public void Configure(string socketType, string host)
        {
            if (string.IsNullOrWhiteSpace(socketType))
            {
                throw new ArgumentException("No Socket Type provided", nameof(socketType));
            }
            else if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("No host provided", nameof(host));
            }
            else if (socketType.Equals("TCPSocket", StringComparison.OrdinalIgnoreCase))
            {
                _protocolType = ProtocolType.Tcp;
                Uri hostUri;
                try
                {
                    hostUri = new ("net.tcp://" + (string.IsNullOrWhiteSpace(host) ? Dns.GetHostName() : host));
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Exception occurred trying to resolve host", nameof(host), ex);
                }

                try
                {
                    IPAddress ipAddress;
                    if (hostUri.HostNameType == UriHostNameType.Dns)
                    {
                        ipAddress = Dns.GetHostEntry(hostUri.DnsSafeHost).AddressList[0];
                    }
                    else
                    {
                        ipAddress = IPAddress.Parse(hostUri.DnsSafeHost);
                    }

                    _endpoint = new IPEndPoint(ipAddress, hostUri.Port < 0 || hostUri.Port == 808 ? 11000 : hostUri.Port);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Unable to establish TCPSocket end point", nameof(host), ex);
                }
            }
            else if (socketType.Equals("IPCSocket", StringComparison.OrdinalIgnoreCase))
            {
                _protocolType = ProtocolType.Unspecified;
                if (!File.Exists(host))
                {
                    throw new ArgumentException("IPC Socket not found", nameof(host));
                }

                try
                {
                    _endpoint = new UnixDomainSocketEndPoint(host);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Unable to establish IPCSocket end point", nameof(host), ex);
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(socketType), socketType, "Unknown socket type");
            }

            _beatTimer.Elapsed += HeartbeatSkipped;
            Connect();
        }

        /// <summary>
        /// Reconnect to host
        /// </summary>
        public void Reconnect()
        {
            Shutdown("Reconnect requested.");
            Connect();
        }

        /// <summary>
        /// Shutdown socket communications
        /// </summary>
        /// <param name="reason">Reason for shutdown</param>
        public void Shutdown(string reason)
        {
            if (_socket != null)
            {
                StatusChange(false, reason);
            }
        }

        #endregion Methods (Public)

        #region Methods (Private)

        /// <summary>
        /// Async callback for socket connection start
        /// </summary>
        /// <param name="ar">Async result</param>
        private void ConnectCallback(IAsyncResult ar)
        {
            bool success = false;
            string message = null;
            Socket server = null;
            try
            {
                // Retrieve the socket from the state object.
                server = (Socket)ar.AsyncState;

                // Complete the connection.
                if (server.Connected)
                {
                    server.EndConnect(ar);

                    message = "Socket connected to " + server.RemoteEndPoint.AddressFamily.ToString() + ": " + server.RemoteEndPoint.ToString();
                    success = true;
                }
                else
                {
                    message = "ConnectCallback socket not connected";
                    success = false;
                }
            }
            catch (Exception ex)
            {
                message = "Exception in ConnectCallback: " + ex.Message;
                success = false;
            }

            if (success)
            {
                try
                {
                    byte[] mode = Encoding.UTF8.GetBytes(_handleCommand == null ? "graphicmode" : "commandmode");
                    if (server.Connected)
                    {
                        server.BeginSend(mode, 0, mode.Length, 0,
                        new AsyncCallback(SendCallback), server);
                    }
                    else
                    {
                        message = "ConnectCallback starting send socket not connected";
                        success = false;
                    }
                }
                catch (Exception ex)
                {
                    message = "Exception in starting send in ConnectCallback: " + ex.Message;
                    success = false;
                }
            }

            if (server != null)
            {
                StatusChange(success, message);
            }
        }

        /// <summary>
        /// Async callback for socket send
        /// </summary>
        /// <param name="ar">Async result</param>
        private void SendCallback(IAsyncResult ar)
        {
            Socket server = null;
            try
            {
                // Retrieve the state object and the server socket
                // from the asynchronous state object.
                server = (Socket)ar.AsyncState;
                // Complete sending the data to the remote device.
                int bytesSent = server.EndSend(ar);
                Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} Sent {bytesSent} bytes to server.");
                StateObject state = new ()
                {
                    Server = server,
                    MemStream = new RecyclableMemoryStream(_recyclableMemoryStreamManager)
                };

                // Begin receiving the data from the remote device.
                server.BeginReceive(state.Buffer, 0, _lengthSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (SocketException ex)
            {
                if (server != null)
                {
                    StatusChange(false, "SocketException in SendCallback: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Async callback for socket receive
        /// </summary>
        /// <param name="ar">Async result</param>
        private void ReceiveCallback(IAsyncResult ar)
        {
            StateObject state = null;
            try
            {
                // Retrieve the state object and the server socket
                // from the asynchronous state object.
                state = (StateObject)ar.AsyncState;
                Socket server = state.Server;

                // Read data from the remote device.
                int bytesRead = server.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.
                    state.MemStream.Write(state.Buffer, 0, bytesRead);
                    state.Remaining -= bytesRead;

                    if (state.CommandSize == 0 && state.MemStream.Length >= _lengthSize)
                    {
                        byte[] lengths = new byte[_lengthSize];
                        state.MemStream.Position = 0;
                        state.MemStream.Read(lengths, 0, _lengthSize);
                        bytesRead = state.MemStream.Read(state.Buffer, 0, state.Buffer.Length);
                        state.MemStream.SetLength(0);
                        if (bytesRead > 0)
                        {
                            state.MemStream.Write(state.Buffer, 0, bytesRead);
                        }

                        state.Remaining = state.CommandSize = ((lengths[0] * 256 + lengths[1]) * 256 + lengths[2]) * 256 + lengths[3];
                        state.ValueSize = ((lengths[4] * 256 + lengths[5]) * 256 + lengths[6]) * 256 + lengths[7];
                    }

                    if (state.Command.Length == 0 && state.CommandSize > 0 && state.MemStream.Length >= state.CommandSize)
                    {
                        byte[] command = new byte[state.CommandSize];
                        state.MemStream.Position = 0;
                        state.MemStream.Read(command, 0, command.Length);
                        if (state.MemStream.Length > command.Length)
                        {
                            bytesRead = state.MemStream.Read(state.Buffer, 0, state.Buffer.Length);
                            state.MemStream.SetLength(0);
                            if (bytesRead > 0)
                            {
                                state.MemStream.Write(state.Buffer, 0, bytesRead);
                            }
                        }
                        else
                        {
                            state.MemStream.SetLength(0);
                        }

                        state.Command = Encoding.UTF8.GetString(command);
                        state.Remaining = state.ValueSize;
                        if (state.ValueSize == 0)
                        {
                            Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} Received command {state.Command}");
                        }
                        else
                        {
                            Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} Strting command {state.Command} and data length {state.ValueSize}");
                        }
                    }

                    if (state.Remaining > 0)
                    {
                        // Get the rest of the data.
                        server.BeginReceive(state.Buffer, 0, Math.Min(state.Remaining, _bufferSize), 0,
                            new AsyncCallback(ReceiveCallback), state);
                    }
                    else
                    {
                        if (state.MemStream.Length == state.ValueSize)
                        {
                            _beatTimer.Interval = _beatTimeout;
                            state.MemStream.Position = 0;
                            CommandHandler(state.Command, ref state.MemStream);
                        }
                        else
                        {
                            Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} Receive incomplete for command {state.Command}: " +
                                $"Received {state.MemStream.Length} bytes instead of {state.ValueSize} bytes");
                        }

                        if (state.MemStream != null)
                        {
                            state.MemStream.Dispose();
                        }

                        state = new ()
                        {
                            Server = server,
                            MemStream = new RecyclableMemoryStream(_recyclableMemoryStreamManager)
                        };

                        server.BeginReceive(state.Buffer, 0, state.Remaining, 0,
                            new AsyncCallback(ReceiveCallback), state);
                    }
                }
            }
            catch (SocketException ex)
            {
                if (_socket != null)
                {
                    StatusChange(false, "SocketExeception occurred in ReceiveCallback: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                if (_socket != null)
                {
                    StatusChange(false, "Exeception occurred in ReceiveCallback: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Handle received command
        /// </summary>
        /// <param name="command">Command to process</param>
        /// <param name="memStream">MemoryStream with command values</param>
        private void CommandHandler(string command, ref RecyclableMemoryStream memStream)
        {
            if (command.Length > 8 && command.Substring(0, 9) == "graphics ")
            {
                // All the data has arrived; write image to screen.
                string[] cmd = command.Split(' ', 2);
                if (cmd.Length < 2)
                {
                    Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} no options were found on command");
                }
                else if (memStream.Length == 0)
                {
                    Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} image data was not found");
                }
                else
                {
                    string[] options = cmd[1].Split(',');
                    if (options.Length < 4)
                    {
                        Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} not enough options were found on command ({command})");
                    }
                    else
                    {
                        if (int.TryParse(options[0], out int x) &&
                            int.TryParse(options[1], out int y) &&
                            int.TryParse(options[2], out int width) &&
                            int.TryParse(options[3], out int height))
                        {
                            _handleGraphics(memStream, x, y, width, height);
                            memStream = null;
                        }
                        else
                        {
                            Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} Unable to parse command");
                        }
                    }
                }
            }
            else if (command == "heartbeat")
            {
                // Let it go
            }
            else if (_handleCommand == null)
            {
                Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} Unknown command received: {command}");
            }
            else
            {
                string values = Encoding.UTF8.GetString(memStream.ToArray());
                Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} Applying command {command} with values {values}");
                _handleCommand(command, values);
                Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} Success!");
            }
        }

        /// <summary>
        /// Start connection to socket server
        /// </summary>
        private void Connect()
        {
            try
            {
                _socket = new Socket(_endpoint.AddressFamily, SocketType.Stream, _protocolType);
                // _socket.NoDelay = true;
                _socket.BeginConnect(_endpoint,
                    new AsyncCallback(ConnectCallback), _socket);
            }
            catch (SocketException ex)
            {
                if (_socket != null)
                {
                    StatusChange(false, "A SocketException was received trying to connect to remote server: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Handle heartbeat not received event
        /// </summary>
        /// <param name="source">Event source</param>
        /// <param name="args">Event arguments</param>
        private void HeartbeatSkipped(object source, ElapsedEventArgs args)
        {
            Shutdown("Server timed out.");
        }

        /// <summary>
        /// Process connection status changed
        /// </summary>
        /// <param name="connectStatus">Status of connection</param>
        /// <param name="message">Status message</param>
        private void StatusChange(bool connectStatus, string message)
        {
            if (!connectStatus && _socket != null)
            {
                if (_socket.Connected == true)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _socket.Disconnect(false);
                }
            }

            if (_beatTimer != null)
            {
                _beatTimer.Enabled = connectStatus;
                _beatTimer.Interval = _beatTimeout;
            }

            _connectionChanged(connectStatus, message);
        }

        #endregion Methods (Private)

        #region Subclasses

        /// <summary>
        /// State object for receiving data from remote device.
        /// </summary>
        private class StateObject
        {
            // Server socket
            public Socket Server;
            // Receive buffer
            public byte[] Buffer = new byte[_bufferSize];
            // Received data stream
            public RecyclableMemoryStream MemStream;
            // Data bytes remaining
            public int Remaining = _lengthSize;
            // Header values
            public int CommandSize = 0;
            public int ValueSize = 0;
            public string Command = string.Empty;
        }

        /// <summary>
        /// Event arguments for connection status changes
        /// </summary>
        public class ConnectionChangedEventArgs : EventArgs
        {
            /// <summary>
            /// Connection status
            /// </summary>
            public bool Connected { get; init; } = false;

            /// <summary>
            /// Status message
            /// </summary>
            public string Message { get; init; } = string.Empty;
        }

        #endregion Subclasses
    }
}