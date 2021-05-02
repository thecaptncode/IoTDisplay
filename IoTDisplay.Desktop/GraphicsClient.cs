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
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media;
    using Avalonia.Media.Imaging;
    using Avalonia.Skia;
    using Microsoft.Extensions.Configuration;
    using Microsoft.IO;
    using SkiaSharp;

    #endregion Using

    public class GraphicsClient : Control
    {
        #region Fields and Events

        public event EventHandler ConnectionChanged;
        private const int _beatTimeout = 120000;
        private static readonly Timer _beatTimer = new () { AutoReset = false, Enabled = false, Interval = _beatTimeout };
        private static readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager = new ();
        private readonly EndPoint _endpoint;
        private readonly ProtocolType _protocolType;
        private bool _isConnected = false;
        private string _connectionMessage = null;
        private RenderTargetBitmap _renderTarget;
        private ISkiaDrawingContextImpl _skiaContext;
        private Socket _socket;

        #endregion Fields and Events

        #region Properties

        public bool IsConnected => _isConnected;

        public string ConnectionMessage => _connectionMessage;

        #endregion Properties

        #region Constructor / Finalizer

        public GraphicsClient()
        {
            IConfigurationRoot configuration;
            try
            {
                configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false)
                    .AddEnvironmentVariables()
                    .Build();
            }
            catch (Exception ex)
            {
                StatusChange(false, "Exception occurred reading configuration file: " + ex.Message);
                return;
            }

            IConfigurationSection clientconfig = configuration.GetSection("Desktop");
            if (clientconfig == null)
            {
                StatusChange(false, "Desktop section not found in configuration file.");
                return;
            }

            string socketType = clientconfig.GetSection("SocketType")?.Value;
            string hostName = clientconfig.GetSection("Host")?.Value;
            if (hostName == null)
            {
                StatusChange(false, "Host not found in configuration file.");
                return;
            }

            if (socketType == null)
            {
                StatusChange(false, "SocketType not found in configuration file.");
                return;
            }
            else if (socketType.Equals("TCPSocket", StringComparison.OrdinalIgnoreCase))
            {
                _protocolType = ProtocolType.Tcp;
                Uri host;
                try
                {
                    host = new ("net.tcp://" + (string.IsNullOrWhiteSpace(hostName) ? Dns.GetHostName() : hostName));
                }
                catch (Exception ex)
                {
                    StatusChange(false, "Exception occurred trying to resolve host " + hostName + ": " + ex.Message);
                    return;
                }

                try
                {
                    IPAddress ipAddress;
                    if (host.HostNameType == UriHostNameType.Dns)
                    {
                        ipAddress = Dns.GetHostEntry(host.DnsSafeHost).AddressList[0];
                    }
                    else
                    {
                        ipAddress = IPAddress.Parse(host.DnsSafeHost);
                    }

                    _endpoint = new IPEndPoint(ipAddress, host.Port < 0 || host.Port == 808 ? 11000 : host.Port);
                }
                catch (Exception ex)
                {
                    StatusChange(false, "Unable to establish TCPSocket end point: " + ex.Message);
                    return;
                }
            }
            else if (socketType.Equals("IPCSocket", StringComparison.OrdinalIgnoreCase))
            {
                _protocolType = ProtocolType.Unspecified;
                try
                {
                    _endpoint = new UnixDomainSocketEndPoint(clientconfig.GetSection("Host").Value);
                }
                catch (Exception ex)
                {
                    StatusChange(false, "Unable to establish IPCSocket end point: " + ex.Message);
                    return;
                }
            }
            else
            {
                StatusChange(false, "Unknown socket type: " + socketType);
                return;
            }

            _socket = new Socket(_endpoint.AddressFamily, SocketType.Stream, _protocolType);
            _beatTimer.Elapsed += HeartbeatSkipped;

            Connect();
        }

        ~GraphicsClient()
        {
            Shutdown("Client shutting down.");
            _socket.Dispose();
            _beatTimer.Enabled = false;
            _beatTimer.Dispose();
            _skiaContext.Dispose();
            _renderTarget.Dispose();
        }

        #endregion Constructor / Finalizer

        #region Methods (Public)

        public override void Render(DrawingContext context)
        {
            if (_renderTarget != null)
            {
                context.DrawImage(_renderTarget,
                    new Rect(0, 0, _renderTarget.PixelSize.Width, _renderTarget.PixelSize.Height),
                    new Rect(0, 0, Width, Height));
            }
        }

        public void Disconnect() => Shutdown("Disconnect requested.");

        public void Reconnect()
        {
            Shutdown("Reconnect requested.");
            _socket = new Socket(_endpoint.AddressFamily, SocketType.Stream, _protocolType);
            Connect();
        }

        #endregion Methods (Public)

        #region Methods (Private)

        private void Connect()
        {
            try
            {
                _socket.BeginConnect(_endpoint,
                    new AsyncCallback(ConnectCallback), _socket);
            }
            catch (SocketException ex)
            {
                StatusChange(false, "A SocketException was received trying to connect to remote server: " + ex.Message);
            }
        }

        private void HeartbeatSkipped(object source, ElapsedEventArgs e)
        {
            Shutdown("Server timed out.");
        }

        private void Shutdown(string reason)
        {
            if (_socket.Connected == true)
            {
                _socket.Shutdown(SocketShutdown.Both);
            }

            _socket.Close();
            StatusChange(false, reason);
        }

        private void StatusChange(bool connectStatus, string message)
        {
            Trace.WriteLine(DateTime.Now.ToLongTimeString() + " " + message);
            _beatTimer.Enabled = connectStatus;
            _beatTimer.Interval = _beatTimeout;
            _isConnected = connectStatus;
            _connectionMessage = message;
            ConnectionChangedEventArgs args = new () { Connected = connectStatus, Message = message };
            ConnectionChanged?.Invoke(this, args);
        }

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
                server.EndConnect(ar);

                message = "Socket connected to " + server.RemoteEndPoint.AddressFamily.ToString() + ": " + server.RemoteEndPoint.ToString();
                success = true;
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
                    // Create the state object.
                    StateObject state = new ();
                    state.MemStream = new RecyclableMemoryStream(_recyclableMemoryStreamManager);
                    state.Server = server;

                    byte[] mode = Encoding.UTF8.GetBytes("graphicmode");
                    server.BeginSend(mode, 0, mode.Length, 0,
                        new AsyncCallback(SendCallback), state);
                }
                catch (Exception ex)
                {
                    message = "Exception in starting send in ConnectCallback: " + ex.Message;
                    success = false;
                }
            }

            StatusChange(success, message);
        }

        private void SendCallback(IAsyncResult ar)
        {
            Socket server = null;
            try
            {
                // Retrieve the state object and the server socket
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                server = state.Server;
                // Complete sending the data to the remote device.
                int bytesSent = server.EndSend(ar);
                Trace.WriteLine($"{DateTime.Now.ToLongTimeString()} Sent {bytesSent} bytes to server.");

                // Begin receiving the data from the remote device.
                server.BeginReceive(state.Buffer, 0, sizeof(int) * 2, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (SocketException ex)
            {
                if (server != null)
                {
                    if (server.Connected == true)
                    {
                        server.Shutdown(SocketShutdown.Both);
                    }

                    server.Close();
                    StatusChange(false, "SocketException in SendCallback: " + ex.Message);
                }
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            const int lenSize = sizeof(int) * 2;
            try
            {
                // Retrieve the state object and the server socket
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket server = state.Server;

                // Read data from the remote device.
                int bytesRead = server.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.
                    state.MemStream.Write(state.Buffer, 0, bytesRead);
                    state.Remaining -= bytesRead;

                    if (state.CommandSize == 0 && state.MemStream.Length >= lenSize)
                    {
                        byte[] lengths = new byte[lenSize];
                        int[] lenArr = new int[2];
                        state.MemStream.Position = 0;
                        state.MemStream.Read(lengths, 0, lenSize);
                        bytesRead = state.MemStream.Read(state.Buffer, 0, state.Buffer.Length);
                        state.MemStream.SetLength(0);
                        if (bytesRead > 0)
                        {
                            state.MemStream.Write(state.Buffer, 0, bytesRead);
                        }

                        Buffer.BlockCopy(lengths, 0, lenArr, 0, lengths.Length);
                        state.Remaining = state.CommandSize = lenArr[0];
                        state.ValueSize = lenArr[1];
                    }

                    if (state.Command.Length == 0 && state.CommandSize > 0 && state.MemStream.Length >= state.CommandSize)
                    {
                        byte[] command = new byte[state.CommandSize];
                        state.MemStream.Position = 0;
                        state.MemStream.Read(command, 0, command.Length);
                        if (state.ValueSize > 0)
                        {
                            bytesRead = state.MemStream.Read(state.Buffer, 0, state.Buffer.Length);
                            state.MemStream.SetLength(0);
                            if (bytesRead > 0)
                            {
                                state.MemStream.Write(state.Buffer, 0, bytesRead);
                            }
                        }

                        state.Command = Encoding.UTF8.GetString(command);
                        state.Remaining = state.ValueSize;
                        Trace.WriteLine($"{DateTime.Now.ToLongTimeString()} Found command {state.Command} and data length {state.ValueSize}");
                    }

                    if (state.Remaining > 0)
                    {
                        // Get the rest of the data.
                        server.BeginReceive(state.Buffer, 0, Math.Min(state.Remaining, StateObject.BufferSize), 0,
                            new AsyncCallback(ReceiveCallback), state);
                    }
                    else
                    {
                        if (state.MemStream.Length == state.ValueSize)
                        {
                            _beatTimer.Interval = _beatTimeout;
                            if (state.Command.Length > 8 && state.Command.Substring(0, 9) == "graphics ")
                            {
                                // All the data has arrived; write image to screen.
                                GraphicCommand(state);
                            }
                        }
                        else
                        {
                            Trace.WriteLine($"{DateTime.Now.ToLongTimeString()} Receive incomplete for command {state.Command}: " +
                                $"Received {state.MemStream.Length} bytes instead of {state.ValueSize} bytes");
                        }

                        state.MemStream.SetLength(0);
                        state.Remaining = lenSize;
                        state.CommandSize = state.ValueSize = 0;
                        state.Command = string.Empty;

                        Trace.WriteLine($"{DateTime.Now.ToLongTimeString()} Waiting for next command.");
                        server.BeginReceive(state.Buffer, 0, state.Remaining, 0,
                            new AsyncCallback(ReceiveCallback), state);
                    }
                }
            }
            catch (SocketException ex)
            {
                if (_socket != null)
                {
                    if (_socket.Connected == true)
                    {
                        _socket.Shutdown(SocketShutdown.Both);
                    }

                    _socket.Close();
                    StatusChange(false, "SocketExeception occurred in ReceiveCallback: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                StatusChange(false, "Exeception occurred in ReceiveCallback: " + ex.Message);
            }
        }

        private void GraphicCommand(StateObject state)
        {
            string[] command = state.Command.Split(' ', 2);
            if (command.Length < 2)
            {
                Trace.WriteLine($"{DateTime.Now.ToLongTimeString()} no options were found on command");
            }
            else if (state.MemStream.Length == 0)
            {
                Trace.WriteLine($"{DateTime.Now.ToLongTimeString()} image data was not found");
            }
            else
            {
                string[] options = command[1].Split(',');
                if (options.Length < 4)
                {
                    Trace.WriteLine($"{DateTime.Now.ToLongTimeString()} not enough options were found on command ({state.Command})");
                }
                else
                {
                    if (int.TryParse(options[0], out int x) &&
                        int.TryParse(options[1], out int y) &&
                        int.TryParse(options[2], out int width) &&
                        int.TryParse(options[3], out int height))
                    {
                        SKRect source = new (0, 0, width, height);
                        SKRect target = new (x, y, width + x - 1, height + y - 1);
                        Trace.WriteLine($"{DateTime.Now.ToLongTimeString()} Received image. Size={state.MemStream.Length}, X={x}, " +
                            $"Y={y}, Width={width}, Height={height}");
                        state.MemStream.Position = 0;
                        if (_renderTarget == null)
                        {
                            _renderTarget = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
                            _skiaContext = _renderTarget.CreateDrawingContext(null) as ISkiaDrawingContextImpl;
                            _skiaContext?.SkCanvas.Clear(SKColors.Transparent);
                        }

                        _skiaContext?.SkCanvas.DrawBitmap(SKBitmap.Decode(state.MemStream), source, target);
                        state.MemStream = new RecyclableMemoryStream(_recyclableMemoryStreamManager);
                        InvalidateVisual();
                        Trace.WriteLine($"{DateTime.Now.ToLongTimeString()} Receive complete");
                    }
                    else
                    {
                        Trace.WriteLine($"{DateTime.Now.ToLongTimeString()} Unable to parse command");
                    }
                }
            }
        }

        #endregion Methods (Private)

        #region Subclasses

        // State object for receiving data from remote device.
        private class StateObject
        {
            // Size of receive buffer.
            public const int BufferSize = 25600;
            // Server socket.
            public Socket Server;
            // Receive buffer.
            public byte[] Buffer = new byte[BufferSize];
            // Received data string.
            public RecyclableMemoryStream MemStream;
            // Data bytes remaining
            public int Remaining = sizeof(int) * 2;
            // Header
            public int CommandSize = 0;
            public int ValueSize = 0;
            public string Command = string.Empty;
        }

        public class ConnectionChangedEventArgs : EventArgs
        {
            public bool Connected { get; init; } = false;
            public string Message { get; init; } = string.Empty;
        }

        #endregion Subclasses
    }
}
