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

namespace IoTDisplay.Common.Services
{
    #region Using

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using IoTDisplay.Common.Models;

    #endregion Using

    public class SocketDisplayService : IDisplayService
    {
        #region Properties

        public string DriverName => _driverName;

        public DateTime LastUpdated => _lastUpdated;

        #endregion Properties

        #region Methods (Public)
        public void Configure(IRenderService renderer, RenderSettings setting) => Create(renderer, setting);

        #endregion Methods (Public)

        #region Fields

        private const int _displayLockTimeout = 60000;
        private const int _updateLockTimeout = 60000;
        private const int _maxListenerRestarts = 20;
        private static readonly ManualResetEvent _readyNext = new (false);
        private static Task listenerTask = null;
        private static bool _listenerStarted = false;
        private static TimerService _updateTimer;
        private readonly object _displaylock = new ();
        private readonly object _updatelock = new ();
        private readonly List<Socket> _graphicClients = new ();
        private readonly List<Socket> _commandClients = new ();
        private readonly string _driverName;
        private readonly Socket _display;
        private IRenderService _renderer;
        private RenderSettings _settings;
        private int _threshold;
        private int _sectionX1 = int.MaxValue;
        private int _sectionY1 = int.MaxValue;
        private int _sectionX2 = int.MinValue;
        private int _sectionY2 = int.MinValue;
        private int _listenerRestarts;
        private DateTime _lastRestart;
        private DateTime _lastUpdated;
        private bool _updating = false;

        #endregion Fields

        #region Constructor / Dispose / Finalizer

        public SocketDisplayService(Socket driver)
        {
            _driverName = driver.LocalEndPoint.AddressFamily.ToString() + ", " + driver.LocalEndPoint.ToString();
            _display = driver;
        }

        public void Create(IRenderService renderer, RenderSettings settings)
        {
            Console.WriteLine($"Starting SocketDisplayService driver: {_driverName}");
            settings.IncludeCommand = true;
            _settings = settings;
            _threshold = settings.Width * settings.Height / 2;
            _renderer = renderer;
            renderer.ScreenChanged += Renderer_ScreenChanged;
            _updateTimer = new ()
            {
                TargetMillisecond = 300000,
                ToleranceMillisecond = 5000,
                Enabled = true
            };
            _updateTimer.Elapsed += UpdateScreen;
            _lastUpdated = new (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            _display.Listen(5);
            listenerTask = Task.Factory.StartNew(Listener);
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~SocketDisplayService()
        {
            if (_updateTimer != null)
            {
                _updateTimer.Elapsed -= UpdateScreen;
                _updateTimer.Enabled = false;
                _updateTimer.Dispose();
            }

            CloseAllConnections();

            if (_display != null)
            {
                _display.Shutdown(SocketShutdown.Both);
                _display.Close();
                _display.Dispose();
            }
        }

        #endregion Constructor / Dispose / Finalizer

        #region Methods (Private)

        private static byte[] BuildHeader(string command, int dataLen)
        {
            int lenSize = sizeof(int) * 2;
            byte[] cmdArr = command == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(command);
            int[] lengths = new int[] { cmdArr.Length, dataLen };
            byte[] header = new byte[lenSize + cmdArr.Length];
            Buffer.BlockCopy(lengths, 0, header, 0, lenSize);
            Buffer.BlockCopy(cmdArr, 0, header, lenSize, command.Length);
            return header;
        }

        private void Listener()
        {
            // Listen for incoming connections.
            while (true)
            {
                // Set the event to nonsignaled state.
                _readyNext.Reset();

                // Start an asynchronous socket to listen for connections.
                Console.WriteLine("Waiting for a connection...");
                _display.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    _display);

                // Wait until a connection is made before continuing.
                _listenerStarted = true;
                _readyNext.WaitOne();
            }
        }

        private void CloseAllConnections()
        {
            Console.WriteLine("Closing all server connections");
            foreach (Socket handler in _commandClients)
            {
                try
                {
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                    handler.Dispose();
                }
                catch
                {
                    // Let it go
                }
            }

            _commandClients.Clear();

            foreach (Socket handler in _graphicClients)
            {
                try
                {
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                    handler.Dispose();
                }
                catch
                {
                    // Let it go
                }
            }

            _graphicClients.Clear();
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            bool success = true;
            Socket handler = null;
            try
            {
                // Signal the main thread to continue.
                _readyNext.Set();

                // Get the socket that handles the client request.
                Socket socket = (Socket)ar.AsyncState;
                handler = socket.EndAccept(ar);
            }
            catch
            {
                success = false;
            }

            if (success)
            {
                try
                {
                    // Create the state object.
                    StateObject state = new ();
                    state.Client = handler;

                    handler.BeginReceive(state.Buffer, 0, state.Remaining, SocketFlags.None,
                        new AsyncCallback(ReceiveCallback), state);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now.ToLongTimeString()} Exception starting receive {ex.Message}");
                    success = false;
                }
            }

            if (!success)
            {
                handler.Close();
                handler.Dispose();
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the server socket
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state?.Client;

                // Read data from the remote device.
                int bytesRead = handler.EndReceive(ar);
                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.
                    state.BufferPos += bytesRead;
                    state.Remaining -= bytesRead;

                    if (state.Remaining > 0)
                    {
                        // Get the rest of the data.
                        handler.BeginReceive(state.Buffer, state.BufferPos, state.Remaining, SocketFlags.None,
                            new AsyncCallback(ReceiveCallback), state);
                    }
                    else
                    {
                        bool success = false;
                        // All the data has arrived; Check send mode.
                        string mode = Encoding.UTF8.GetString(state.Buffer);
                        state.IsCommandMode = mode.Equals("commandmode", StringComparison.OrdinalIgnoreCase);

                        // Send full screen upon connect
                        (byte[] header, byte[] data) = GetScreen(0, 0, _settings.Width, _settings.Height);
                        if (handler != null && header != null)
                        {
                            try
                            {
                                handler.BeginSend(header, 0, header.Length, SocketFlags.None,
                                        new AsyncCallback(SendCallback), handler);
                                handler.BeginSend(data, 0, data.Length, SocketFlags.None,
                                    new AsyncCallback(SendCallback), handler);
                                success = true;
                            }
                            catch (SocketException ex)
                            {
                                Console.WriteLine($"{DateTime.Now.ToLongTimeString()} Exception starting to send data {ex.Message}");
                                success = false;
                            }
                        }

                        if (success)
                        {
                            if (state.IsCommandMode)
                            {
                                Console.WriteLine("Client using Command Mode");
                                _commandClients.Add(handler);
                            }
                            else
                            {
                                Console.WriteLine("Client using Graphic Mode");
                                _graphicClients.Add(handler);
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now.ToLongTimeString()} Exception receiving data {ex.Message}");
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            Socket handler = null;
            try
            {
                // Retrieve the socket from the state object.
                handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = handler.EndSend(ar);
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Exception occured in SendCallBack " + ex.Message);
                if (handler != null)
                {
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                    handler.Dispose();
                    _graphicClients.Remove(handler);
                    _commandClients.Remove(handler);
                    Console.WriteLine("Client closed");
                }
            }
        }

        private (byte[] header, byte[] data) GetScreen(int x, int y, int width, int height)
        {
            try
            {
                if (_settings.IsPortrait)
                {
                    int tmp = x;
                    x = y;
                    y = tmp;
                    tmp = width;
                    width = height;
                    height = tmp;
                }

                byte[] data;
                using (MemoryStream memStream = (MemoryStream)_renderer.ScreenAt(
                    new RenderActions.GetAt
                    {
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height
                    }))
                {
                    data = memStream.ToArray();
                    memStream.Close();
                }

                return (BuildHeader($"graphics {x},{y},{width},{height}", data.Length), data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception occurred trying to get a screen area. " + ex.Message);
                return (null, null);
            }
        }

        private void SendToClients(byte[] header, byte[] data, List<Socket> clientList)
        {
            List<Socket> removeList = new ();
            // Begin sending the data to each remote device.
            foreach (Socket handler in clientList)
            {
                try
                {
                    if (header.Length > 0)
                    {
                        handler.BeginSend(header, 0, header.Length, SocketFlags.None,
                            new AsyncCallback(SendCallback), handler);
                    }

                    if (data.Length > 0)
                    {
                        handler.BeginSend(data, 0, data.Length, SocketFlags.None,
                            new AsyncCallback(SendCallback), handler);
                    }
                }
                catch (SocketException)
                {
                    removeList.Add(handler);
                }
            }

            foreach (Socket rmv in removeList)
            {
                rmv.Shutdown(SocketShutdown.Both);
                rmv.Close();
                rmv.Dispose();
                clientList.Remove(rmv);
            }
        }

        private void Renderer_ScreenChanged(object sender, EventArgs e)
        {
            bool lockSuccess = false;
            if (_listenerStarted &&
               (listenerTask?.Status == null || listenerTask.Status != TaskStatus.Running))
            {
                try
                {
                    Monitor.TryEnter(_display, 2000, ref lockSuccess);
                    if (lockSuccess)
                    {
                        CloseAllConnections();
                        if (++_listenerRestarts <= _maxListenerRestarts || _lastRestart.Date != DateTime.Now.Date)
                        {
                            Console.WriteLine($"Listener at status {listenerTask?.Status.ToString()}.  Restarting...");
                            if (_lastRestart.Date != DateTime.Now.Date)
                            {
                                _listenerRestarts = 1;
                            }

                            listenerTask = Task.Factory.StartNew(Listener);
                            _lastRestart = DateTime.Now;
                        }
                        else
                        {
                            throw new InvalidOperationException("Maximum number of listener restarts per day reached.");
                        }
                    }
                }
                finally
                {
                    if (lockSuccess)
                    {
                        Monitor.Exit(_display);
                    }
                }
            }

            ScreenChangedEventArgs args = (ScreenChangedEventArgs)e;
            if (_commandClients.Count > 0 && args.Command.CommandName != "refresh" && args.Command.CommandName != "update")
            {
                // Remaining commands are clear, image, draw, text, graphics
                byte[] data = args.Command.CommandValues == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(args.Command.CommandValues);
                byte[] header = BuildHeader(args.Command.CommandName, data.Length);
                SendToClients(header, data, _commandClients);
            }

            lockSuccess = false;
            try
            {
                Monitor.TryEnter(_updatelock, _updateLockTimeout, ref lockSuccess);
                if (!lockSuccess)
                {
                    throw new TimeoutException("A wait for update lock timed out.");
                }

                int delay = 1000;
                if (args.Delay)
                {
                    delay = 5000;
                }

                int x2 = args.Width + args.X - 1;
                int y2 = args.Height + args.Y - 1;
                _sectionX1 = Math.Min(args.X, _sectionX1);
                _sectionY1 = Math.Min(args.Y, _sectionY1);
                _sectionX2 = Math.Max(x2, _sectionX2);
                _sectionY2 = Math.Max(y2, _sectionY2);

                if (!_updating)
                {
                    _updating = true;
                    _updateTimer.Interval = delay;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception occurred setting update timer. " + ex.Message);
            }
            finally
            {
                if (lockSuccess)
                {
                    Monitor.Exit(_updatelock);
                }
            }
        }

        private void UpdateScreen(Object source, ElapsedEventArgs e)
        {
            if (_updating)
            {
                bool lockSuccess = false;
                try
                {
                    Monitor.TryEnter(_displaylock, _displayLockTimeout, ref lockSuccess);
                    if (!lockSuccess)
                    {
                        throw new TimeoutException("A wait for display lock timed out.");
                    }

                    int x;
                    int y;
                    int width;
                    int height;
                    try
                    {
                        lockSuccess = false;
                        Monitor.TryEnter(_updatelock, _updateLockTimeout, ref lockSuccess);
                        if (!lockSuccess)
                        {
                            throw new TimeoutException("A wait for update lock timed out.");
                        }

                        if (_graphicClients.Count > 0)
                        {
                            x = _sectionX1;
                            y = _sectionY1;
                            width = _sectionX2 - _sectionX1 + 1;
                            height = _sectionY2 - _sectionY1 + 1;
                            if (width * height > _threshold)
                            {
                                x = 0;
                                y = 0;
                                width = _settings.Width;
                                height = _settings.Height;
                            }

                            (byte[] header, byte[] data) = GetScreen(x, y, width, height);
                            SendToClients(header, data, _graphicClients);
                        }

                        _lastUpdated = DateTime.UtcNow;
                        _sectionX1 = int.MaxValue;
                        _sectionY1 = int.MaxValue;
                        _sectionX2 = int.MinValue;
                        _sectionY2 = int.MinValue;
                        _updating = false;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("An exception occurred sending screen to socket client. " + ex.Message);
                    }
                    finally
                    {
                        if (lockSuccess)
                        {
                            Monitor.Exit(_updatelock);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An exception occurred updating the screens. " + ex.Message);
                }
                finally
                {
                    if (lockSuccess)
                    {
                        Monitor.Exit(_displaylock);
                    }
                }
            }
        }
        #endregion Methods (Private)

        // State object for remote device.
        public class StateObject
        {
            // Size of receive buffer
            public const int BufferSize = 11;
            // Server socket.
            public Socket Client;
            // Client mode
            public bool IsCommandMode = false;
            // Receive buffer.
            public byte[] Buffer = new byte[BufferSize];
            // Buffer position
            public int BufferPos = 0;
            // Data bytes remaining
            public int Remaining = BufferSize;
        }
    }
}
