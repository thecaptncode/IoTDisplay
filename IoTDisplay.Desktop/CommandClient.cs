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
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media;
    using Avalonia.Media.Imaging;
    using IoTDisplay.Common.Models;
    using IoTDisplay.Common.Services;
    using SkiaSharp;

    #endregion Using

    /// <summary>
    /// Control for IoTDisplay Command Client
    /// </summary>
    public class CommandClient : Control
    {
        #region Fields and Events

        public event EventHandler ConnectionChanged;
        private CommunicationService _communications = null;
        private bool _isConnected = false;
        private string _connectionMessage = null;
        private Bitmap _renderTarget;
        private RenderSettings _settings;
        private IRenderService _renderer = null;

        #endregion Fields and Events

        #region Properties

        /// <summary>
        /// Communications socket type
        /// </summary>
        public string SocketType { get; set; }

        /// <summary>
        /// Socket host string
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Is client connected to server
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Connection message
        /// </summary>
        public string ConnectionMessage => _connectionMessage;

        #endregion Properties

        #region Constructor / Finalizer

        /// <summary>
        /// Constructor for command client
        /// </summary>
        public CommandClient()
        {
            AttachedToVisualTree += CommandClient_AttachedToVisualTree;
            DetachedFromVisualTree += CommandClient_DetachedFromVisualTree;
        }

        /// <summary>
        /// Finalizer for command clientS
        /// </summary>
        ~CommandClient()
        {
            _renderTarget.Dispose();
        }

        #endregion Constructor / Finalizer

        #region Methods (Public)

        /// <summary>
        /// Create communications client when control is attached
        /// </summary>
        /// <param name="sender">Control attached to</param>
        /// <param name="args">Event Arguments</param>
        private void CommandClient_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs args)
        {
            _communications = new (StatusChanged, AddGraphics, ProcessCommand);
            _communications.Configure(SocketType, Host);
        }

        /// <summary>
        /// Dispose of communications client and renderer when control is detached
        /// </summary>
        /// <param name="sender">Control detached from</param>
        /// <param name="args">Event Arguments</param>
        private void CommandClient_DetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs args)
        {
            if (_communications != null)
            {
                _communications.Dispose();
                _communications = null;
            }

            if (_renderer != null)
            {
                _renderer.Dispose();
            }
        }

        /// <summary>
        /// Render control to Avalonia drawing context
        /// </summary>
        /// <param name="context">Drawing context</param>
        public override void Render(DrawingContext context)
        {
            if (_renderTarget != null)
            {
                context.DrawImage(_renderTarget,
                    new Rect(0, 0, _renderTarget.PixelSize.Width, _renderTarget.PixelSize.Height),
                    new Rect(0, 0, Width, Height));
            }
        }

        /// <summary>
        /// Disconnect from server
        /// </summary>
        public void Disconnect() => _communications.Shutdown("Disconnect requested.");

        /// <summary>
        /// Reconnect to server
        /// </summary>
        public void Reconnect() => _communications.Reconnect();

        #endregion Methods (Public)

        #region Methods (Private)

        /// <summary>
        /// Handle connection status change
        /// </summary>
        /// <param name="connected">Status of connection</param>
        /// <param name="message">Status message</param>
        private void StatusChanged(bool connected, string message)
        {
            _isConnected = connected;
            _connectionMessage = message;
            CommunicationService.ConnectionChangedEventArgs args = new () { Connected = connected, Message = message };
            ConnectionChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Process graphics stream and add to canvas
        /// </summary>
        /// <param name="stream">Stream of graphic data</param>
        /// <param name="x">X coordinate to place graphic</param>
        /// <param name="y">Y coordinate to place graphic</param>
        /// <param name="width">Width of graphic</param>
        /// <param name="height">Height of graphic</param>
        private void AddGraphics(Stream stream, int x, int y, int width, int height)
        {
            if (_renderTarget == null)
            {
                _settings = new ()
                {
                    Rotation = 0,
                    Background = SKColor.Parse("#ffffff"),
                    Foreground = SKColor.Parse("#000000"),
                    IncludeCommand = false,
                };
                _settings.Resize(width, height);
                _renderer = new RenderService(_settings, null, null);
            }

            _renderer.Graphic(new RenderActions.Graphic() { X = x, Y = y, Data = stream });
            _renderTarget = new Bitmap(_renderer.Screen);
            InvalidateVisual();
        }

        /// <summary>
        /// Process command and render to canvas
        /// </summary>
        /// <param name="command">Command name</param>
        /// <param name="value">Command values</param>
        private void ProcessCommand(string command, string value)
        {
            RenderActions.RenderCommand cmd = new () { CommandName = command, CommandValues = value };
            try
            {
                _renderer.RenderCommand(cmd);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} Exeception occurred in ProcessCommand: {ex.Message}");
            }

            _renderTarget = new Bitmap(_renderer.Screen);
            InvalidateVisual();
        }

        #endregion Methods (Private)
    }
}
