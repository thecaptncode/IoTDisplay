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
    using System.Text.Json;
    using System.Threading;
    using IoTDisplay.Common.Models;
    using SkiaSharp;

    #endregion Using

    public class RenderService : IRenderService
    {
        #region Properties and Events

        public event EventHandler ScreenChanged;

        public RenderSettings Settings => _settings;

        public List<IDisplayService> Displays => _displays;

        public IClockManagerService Clocks => _clocks;

        public Stream Screen => GetScreen();

        #endregion Properties and Events

        #region Methods (Public)

        public IRenderService Clear() => ClearScreen(true);

        public IRenderService Refresh() => RefreshScreen();

        public Stream ScreenAt(RenderActions.ScreenAt area) => GetScreen(area);

        public IRenderService Image(RenderActions.Image image, bool persist = true) => AddImage(image, persist);

        public IRenderService Draw(RenderActions.Draw draw, bool persist = true) => AddDraw(draw, persist);

        public IRenderService Text(RenderActions.Text text, bool bold = false, bool persist = true) =>
            AddText(text, bold, persist);

        public bool RenderCommand(RenderActions.RenderCommand command) => ProcessCommand(command);

        #endregion Methods (Public)

        #region Fields

        private readonly object _exportLock = new ();

        private readonly int _exportLockTimeout = 10000;

        private readonly RenderSettings _settings = null;

        private readonly IClockManagerService _clocks;

        private readonly List<IDisplayService> _displays;

        private readonly SKBitmap _screen;

        private readonly SKCanvas _canvas;

        #endregion Fields

        #region Constructor

        public RenderService(RenderSettings settings, IClockManagerService clocks, List<IDisplayService> displays)
        {
            foreach (IDisplayService display in displays)
            {
                display.Configure(this, settings);
            }

            clocks.Configure(this, settings);

            _displays = displays;
            _settings = settings;
            _clocks = clocks;
            _screen = new (_settings.Width, _settings.Height);
            _canvas = new (_screen);
            _canvas.Clear(_settings.Background);
            Import(true);
        }

        #endregion Constructor

        #region Methods (Protected)

        protected virtual void OnScreenChanged(int x, int y, int width, int height, bool delay, bool persist, string command, string values)
        {
            if (persist)
            {
                Export(command + (values == null ? string.Empty : "\t" + values));
            }

            // Clip to ensure dimensions are within screen
            int hoffset = x < 0 ? 0 - x : 0;
            x += hoffset;
            width -= hoffset;

            int voffset = y < 0 ? 0 - y : 0;
            y += voffset;
            height -= voffset;

            if (x < _settings.Width && y < _settings.Height)
            {
                ScreenChangedEventArgs evt = new ()
                {
                    X = x,
                    Y = y,
                    Width = Math.Min(width, _settings.Width - x),
                    Height = Math.Min(height, _settings.Height - y),
                    Delay = delay,
                    Command = _settings.IncludeCommand ? new RenderActions.RenderCommand { CommandName = command, CommandValues = values } : null
                };
                ScreenChanged?.Invoke(this, evt);
            }
        }

        #endregion Methods (Protected)

        #region Methods (Private)

        private Stream GetScreen()
        {
            MemoryStream memStream = new ();
            using (SKManagedWStream wstream = new (memStream))
            {
                if (_settings.Rotation == 0)
                {
                    _screen.Encode(wstream, SKEncodedImageFormat.Png, 100);
                }
                else
                {
                    int newWidth = _settings.Width;
                    int newHeight = _settings.Height;
                    if (_settings.IsPortrait)
                    {
                        newWidth = _settings.Height;
                        newHeight = _settings.Width;
                    }

                    using SKBitmap image = new (newWidth, newHeight, _screen.ColorType, _screen.AlphaType, _screen.ColorSpace);

                    using SKCanvas surface = new (image);
                    surface.Translate(newWidth, 0);
                    surface.RotateDegrees(_settings.Rotation);
                    surface.DrawBitmap(_screen, 0, 0);
                    image.Encode(wstream, SKEncodedImageFormat.Png, 100);
                }
            }

            memStream.Position = 0;
            return memStream;
        }

        private Stream GetScreen(RenderActions.ScreenAt area)
        {
            if (area.X < 0 || area.X >= _settings.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(area.X), area.X, "X coordinate is not within the screen");
            }

            if (area.Y < 0 || area.Y >= _settings.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(area.Y), area.Y, "Y coordinate is not within the screen");
            }

            if (area.Width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(area.Width), area.Width, "Width must be greater than zero");
            }

            if (area.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(area.Height), area.Height, "Height must be greater than zero");
            }

            if (area.Width + area.X > _settings.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(area.Width), area.Width, "Width area is wider than the screen");
            }

            if (area.Height + area.Y > _settings.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(area.Height), area.Height, "Height area is taller than the screen");
            }

            MemoryStream memStream = new ();
            using (SKManagedWStream wstream = new (memStream))
            {
                if (_settings.Rotation == 0)
                {
                    using SKBitmap image = new (area.Width, area.Height, _screen.ColorType, _screen.AlphaType, _screen.ColorSpace);
                    if (!_screen.ExtractSubset(image, SKRectI.Create(area.X, area.Y, area.Width, area.Height)))
                    {
                        throw new ArgumentException("Unable to extract an area of the canvas");
                    }

                    image.Encode(wstream, SKEncodedImageFormat.Png, 100);
                }
                else
                {
                    int newWidth = area.Width;
                    int newHeight = area.Height;
                    if (_settings.IsPortrait)
                    {
                        newWidth = area.Height;
                        newHeight = area.Width;
                    }

                    SKBitmap image = new (newWidth, newHeight, _screen.ColorType, _screen.AlphaType, _screen.ColorSpace);
                    using SKCanvas surface = new (image);
                    surface.Translate(newWidth, 0);
                    surface.RotateDegrees(_settings.Rotation);
                    surface.DrawBitmap(_screen, SKRectI.Create(area.X, area.Y, area.Width, area.Height), SKRectI.Create(0, 0, newWidth, newHeight));
                    image.Encode(wstream, SKEncodedImageFormat.Png, 100);
                }
            }

            memStream.Position = 0;
            return memStream;
        }

        private IRenderService ClearScreen(bool clearState)
        {
            _canvas.Clear(_settings.Background);

            if (clearState)
            {
                string screenpath = _settings.Statefolder + "IoTDisplayScreen.png";
                string commandpath = _settings.Statefolder + "IoTDisplayCommands.txt";

                if (File.Exists(screenpath))
                {
                    File.Delete(screenpath);
                }

                if (File.Exists(commandpath))
                {
                    File.Delete(commandpath);
                }
            }

            OnScreenChanged(0, 0, _settings.Width, _settings.Height, false, false, "clear", null);
            return this;
        }

        private IRenderService RefreshScreen()
        {
            _canvas.Clear(_settings.Background);
            Import(false);

            OnScreenChanged(0, 0, _settings.Width, _settings.Height, false, false, "refresh", null);
            return this;
        }

        private IRenderService AddImage(RenderActions.Image image, bool persist = true)
        {
            int width = 0;
            int height = 0;
            try
            {
                using SKBitmap img = RenderTools.GetImage(_settings, image.X, image.Y, image.Filename);
                _canvas.DrawBitmap(img, image.X, image.Y);
                width = img.Width;
                height = img.Height;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentException("An exception occurred trying to add image to the canvas: " + ex.Message, nameof(image.Filename), ex);
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            OnScreenChanged(image.X, image.Y, width, height, image.Delay, persist, "image", JsonSerializer.Serialize<RenderActions.Image>(image));
            return this;
        }

        private IRenderService AddDraw(RenderActions.Draw draw, bool persist = true)
        {
            try
            {
                using SKImage img = RenderTools.GetPicture(_settings, draw.X, draw.Y, draw.Width, draw.Height, draw.SvgCommands);
                _canvas.DrawImage(img, draw.X, draw.Y);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentException("An exception occurred trying to add drawing to the canvas: " + ex.Message, nameof(draw.SvgCommands), ex);
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            OnScreenChanged(draw.X, draw.Y, draw.Width, draw.Height, draw.Delay, persist, "draw", JsonSerializer.Serialize<RenderActions.Draw>(draw));
            return this;
        }

        private IRenderService AddText(RenderActions.Text text, bool bold = false, bool persist = true)
        {
            int width = 0;
            int height = 0;
            if (string.IsNullOrWhiteSpace(text.Value))
            {
                return this;
            }

            if (text.FontSize == 0)
            {
                text.FontSize = 32;
            }

            text.Value = text.Value.Replace("\r", " ").Replace("\n", string.Empty);
            SKPaint paint = null;
            int hoffset, voffset, left, top;
            try
            {
                (paint, width, height, hoffset, voffset, left, top) = RenderTools.GetPaint(_settings, text.X, text.Y, text.Value,
                    text.HorizAlign, text.VertAlign, text.Font, text.FontSize, text.FontWeight, text.FontWidth, text.HexColor, bold);
                _canvas.DrawText(text.Value, text.X + hoffset, text.Y + voffset, paint);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("An exception occurred trying to add text to the canvas:" + ex.Message, nameof(text), ex);
            }
            finally
            {
                if (paint != null)
                {
                    paint.Dispose();
                }
            }

            OnScreenChanged(left, top, width, height, text.Delay, persist, "text", JsonSerializer.Serialize<RenderActions.Text>(text));
            return this;
        }

        private bool ProcessCommand(RenderActions.RenderCommand command)
        {
            JsonSerializerOptions options = new () { AllowTrailingCommas = true };
            try
            {
                switch (command.CommandName.ToLower())
                {
                    case "image":
                        RenderActions.Image image = JsonSerializer.Deserialize<RenderActions.Image>(command.CommandValues, options);
                        image.Delay = true;
                        AddImage(image, false);
                        break;
                    case "draw":
                        RenderActions.Draw draw = JsonSerializer.Deserialize<RenderActions.Draw>(command.CommandValues, options);
                        draw.Delay = true;
                        AddDraw(draw, false);
                        break;
                    case "text":
                        RenderActions.Text text = JsonSerializer.Deserialize<RenderActions.Text>(command.CommandValues, options);
                        text.Delay = true;
                        AddText(text, false, false);
                        break;
                    case "clear":
                        ClearScreen(true);
                        break;
                    case "refresh":
                        RefreshScreen();
                        break;
                    case "update":
                        break;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException("An exception occurred processing render command", nameof(command), ex);
            }

            return true;
        }

        private void Export(string command)
        {
            bool lockSuccess = false;
            try
            {
                Monitor.TryEnter(_exportLock, _exportLockTimeout, ref lockSuccess);
                if (!lockSuccess)
                {
                    throw new TimeoutException("A wait for export lock timed out.");
                }

                string screenpath = _settings.Statefolder + "IoTDisplayScreen.png";
                string commandpath = _settings.Statefolder + "IoTDisplayCommands.txt";

                if (!File.Exists(commandpath))
                {
                    using StreamWriter sw = File.CreateText(commandpath);
                    sw.WriteLine(command.Replace("\r", " ").Replace("\n", string.Empty));
                }
                else if (new FileInfo(commandpath).Length < 4096)
                {
                    using StreamWriter sw = File.AppendText(commandpath);
                    sw.WriteLine(command.Replace("\r", " ").Replace("\n", string.Empty));
                }
                else
                {
                    using (SKData currentScreen = _screen.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        using FileStream stream = File.Open(screenpath, FileMode.Create);
                        currentScreen.SaveTo(stream);
                        stream.Close();
                    }

                    File.Delete(commandpath);
                    _clocks.Export();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception occurred exporting command " + command + ". " + ex.Message);
            }
            finally
            {
                if (lockSuccess)
                {
                    Monitor.Exit(_exportLock);
                }
            }
        }

        private void Import(bool addClocks)
        {
            bool lockSuccess = false;
            try
            {
                Monitor.TryEnter(_exportLock, _exportLockTimeout, ref lockSuccess);
                if (!lockSuccess)
                {
                    throw new TimeoutException("A wait for export lock timed out.");
                }

                ClearScreen(false);
                string screenpath = _settings.Statefolder + "IoTDisplayScreen.png";
                string commandpath = _settings.Statefolder + "IoTDisplayCommands.txt";
                bool updated = false;
                if (File.Exists(screenpath))
                {
                    AddImage(new () { X = 0, Y = 0, Filename = screenpath, Delay = true }, false);
                    Console.WriteLine("Previous screen restored");
                    updated = true;
                }

                if (File.Exists(commandpath))
                {
                    using StreamReader sr = File.OpenText(commandpath);
                    string command = string.Empty;
                    while ((command = sr.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(command))
                        {
                            string[] cmd = command.Split('\t', 2);
                            try
                            {
                                if (ProcessCommand(new RenderActions.RenderCommand { CommandName = cmd[0], CommandValues = cmd[1] }))
                                {
                                    updated = true;
                                }
                                else
                                {
                                    Console.WriteLine("Unknown render command in state file: " + cmd[0]);
                                }
                            }
                            catch (ArgumentException ex)
                            {
                                Console.Write(ex.Message + " during import: " + ex.InnerException.Message);
                            }
                        }
                    }
                }

                if (addClocks)
                {
                    _clocks.Import();
                }

                if (updated)
                {
                    OnScreenChanged(0, 0, _settings.Width, _settings.Height, false, false, "update", null);
                }
                else
                {
                    string execpath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    if (execpath.StartsWith("file:"))
                    {
                        execpath = execpath[5..];
                    }

                    execpath = execpath.Trim('/').Trim('\\') + "/splash.png";
                    if (File.Exists(execpath))
                    {
                        AddImage(new () { X = 0, Y = 0, Filename = execpath, Delay = false }, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception occurred importing commands. " + ex.Message);
            }
            finally
            {
                if (lockSuccess)
                {
                    Monitor.Exit(_exportLock);
                }
            }
        }

        #endregion Methods (Private)
    }
}
