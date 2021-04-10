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
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Xml;
    using IoTDisplay.Common.Models;
    using SkiaSharp;
    using Svg.Skia;
    using TimeZoneConverter;

    #endregion Using

    public class RenderService : IRenderService
    {
        #region Properties and Events

        public event EventHandler ScreenChanged;

        public Stream Screen => GetScreen();

        #endregion Properties and Events

        #region Methods (Public)

        public void Create(RenderSettings settings) => SetScreen(settings);

        public IRenderService Clear() => ClearScreen();

        public IRenderService Refresh() => RefreshScreen();

        public Stream ScreenAt(RenderActions.ScreenAt area) => GetScreen(area);

        public IRenderService Image(RenderActions.Image image, bool persist = true) => AddImage(image, persist);

        public IRenderService Draw(RenderActions.Draw draw, bool persist = true) => AddDraw(draw, persist);

        public IRenderService Text(RenderActions.Text text, bool bold = false, bool persist = true) =>
            AddText(text, bold, persist);

        public IRenderService Clock(RenderActions.Clock clock) => AddClock(clock);

        public IRenderService ClockClear() => ClearClocks();

        public IRenderService ClockImage(RenderActions.ClockImage clockImage) => AddClock(clockImage);

        public IRenderService ClockDraw(RenderActions.ClockDraw clockDraw) => AddClock(clockDraw);

        public IRenderService ClockTime(RenderActions.ClockTime clockTime) => AddClock(clockTime);

        public IRenderService ClockDelete(RenderActions.ClockDelete clockDelete) => DeleteClock(clockDelete);

        #endregion Methods (Public)

        #region Fields

        private readonly object _exportLock = new ();

        private RenderSettings _settings;

        private SKBitmap _screen;

        private SKCanvas _canvas;

        private IDictionary<string, ClockService> _clocks;

        #endregion Fields

        #region Constructor

        public RenderService()
        {
        }

        #endregion Constructor

        #region Methods (Protected)

        protected virtual void OnScreenChanged(int x, int y, int width, int height, bool delay)
        {
            ScreenChangedEventArgs evt = new (x, y, width, height, delay);
            ScreenChanged?.Invoke(this, evt);
        }

        #endregion Methods (Protected)

        #region Methods (Private)

        private static SKTypeface GetTypeface(string font, int weight, int width)
        {
            SKTypeface typeface = null;
            if (!string.IsNullOrWhiteSpace(font))
            {
                try
                {
                    if (File.Exists(font))
                    {
                        typeface = SKTypeface.FromFile(font);
                    }
                    else if (weight > 0 && width > 0)
                    {
                        typeface = SKTypeface.FromFamilyName(font, weight, width, SKFontStyleSlant.Upright);
                    }
                    else if (weight > 0)
                    {
                        typeface = SKTypeface.FromFamilyName(font, weight, 5, SKFontStyleSlant.Upright);
                    }
                    else if (width > 0)
                    {
                        typeface = SKTypeface.FromFamilyName(font, 400, width, SKFontStyleSlant.Upright);
                    }
                    else
                    {
                        typeface = SKTypeface.FromFamilyName(font);
                    }
                }
                catch
                {
                    typeface = null;
                }
            }

            return typeface;
        }

        private static string CleanFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\/\\\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }

        private void SetScreen(RenderSettings settings)
        {
            _settings = settings;
            _screen = new (_settings.Width, _settings.Height);
            _canvas = new (_screen);
            _canvas.Clear(_settings.Background);
            _clocks = new Dictionary<string, ClockService>();
            Import(true);
        }

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

        private IRenderService ClearScreen()
        {
            _canvas.Clear(_settings.Background);

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

            OnScreenChanged(-1, -1, -1, -1, false);
            return this;
        }

        private IRenderService RefreshScreen()
        {
            _canvas.Clear(_settings.Background);
            Import(false);

            OnScreenChanged(-1, -1, -1, -1, false);
            return this;
        }

        private IRenderService AddImage(RenderActions.Image image, bool persist = true)
        {
            int width = 0;
            int height = 0;
            try
            {
                using SKBitmap img = GetImage(image.X, image.Y, image.Filename);
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
                throw new ArgumentException("An unknown exception occured trying to add image to the canvas:" + ex.Message, nameof(image.Filename));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            if (persist)
            {
                bool saveDelay = image.Delay;
                image.Delay = true;
                Export("image\t" + JsonSerializer.Serialize<RenderActions.Image>(image));
                image.Delay = saveDelay;
            }

            OnScreenChanged(image.X, image.Y, width, height, image.Delay);
            return this;
        }

        private IRenderService AddDraw(RenderActions.Draw draw, bool persist = true)
        {
            try
            {
                using SKImage img = GetPicture(draw.X, draw.Y, draw.Width, draw.Height, draw.SvgCommands);
                _canvas.DrawImage(img, draw.X, draw.Y);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentException("An unknown exception occured trying to add drawing to the canvas:" + ex.Message, nameof(draw.SvgCommands));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            if (persist)
            {
                bool saveDelay = draw.Delay;
                draw.Delay = true;
                Export("draw\t" + JsonSerializer.Serialize<RenderActions.Draw>(draw));
                draw.Delay = saveDelay;
            }

            OnScreenChanged(draw.X, draw.Y, draw.Width, draw.Height, draw.Delay);
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
            try
            {
                int hoffset, voffset;
                (paint, width, height, hoffset, voffset) = GetPaint(text.X, text.Y, text.Value, text.HorizAlign, text.VertAlign,
                    text.Font, text.FontSize, text.FontWeight, text.FontWidth, text.HexColor, bold);
                _canvas.DrawText(text.Value, text.X + hoffset, text.Y + voffset, paint);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("An unknown exception occured trying to add text to the canvas:" + ex.Message, nameof(text));
            }
            finally
            {
                if (paint != null)
                {
                    paint.Dispose();
                }
            }

            if (persist)
            {
                bool saveDelay = text.Delay;
                text.Delay = true;
                Export("text\t" + JsonSerializer.Serialize<RenderActions.Text>(text));
                text.Delay = saveDelay;
            }

            OnScreenChanged(text.X, text.Y, width, height, text.Delay);
            return this;
        }

        private IRenderService ClearClocks()
        {
            int clocksecond = DateTime.Now.Second;
            if (clocksecond > 48)
            {
                Thread.Sleep((61 - clocksecond) * 1000);
            }

            foreach (KeyValuePair<string, ClockService> clock in _clocks)
            {
                DeleteClock(new () { Timezone = clock.Value.TimeZoneId });
            }

            return this;
        }

        private IRenderService AddClock(RenderActions.Clock clock)
        {
            string tzId;
            try
            {
                tzId = GetTimeZoneID(clock.Timezone, false);
            }
            catch
            {
                throw;
            }

            if (_clocks.ContainsKey(tzId))
            {
                _clocks[tzId].Dispose();
                _clocks.Remove(tzId);
            }

            _clocks.Add(tzId, new (this, tzId, _settings.Background.ToString(), string.Empty));
            ExportClocks(false);
            return this;
        }

        private IRenderService AddClock(RenderActions.ClockImage clockImage)
        {
            string tzId;
            int width = 0;
            int height = 0;
            try
            {
                tzId = GetTimeZoneID(clockImage.Timezone, true);
                using SKBitmap img = GetImage(clockImage.X, clockImage.Y, clockImage.Filename);
                width = img.Width;
                height = img.Height;
            }
            catch (ArgumentException)
            {
                throw;
            }

            _clocks[tzId].AddImage(clockImage, width, height);
            ExportClock(tzId);
            return this;
        }

        private IRenderService AddClock(RenderActions.ClockDraw clockDraw)
        {
            string tzId;
            if (!string.IsNullOrEmpty(clockDraw.SvgCommands))
            {
                clockDraw.SvgCommands = clockDraw.SvgCommands.Replace("\r", " ").Replace("\n", string.Empty);
            }

            try
            {
                tzId = GetTimeZoneID(clockDraw.Timezone, true);
                using SKImage img = GetPicture(clockDraw.X, clockDraw.Y, clockDraw.Width, clockDraw.Height,
                    clockDraw.SvgCommands);
            }
            catch
            {
                throw;
            }

            _clocks[tzId].AddDraw(clockDraw);
            ExportClock(tzId);
            return this;
        }

        private IRenderService AddClock(RenderActions.ClockTime clockTime)
        {
            string tzId;
            int width;
            int height;
            if (clockTime.FontSize == 0)
            {
                clockTime.FontSize = 32;
            }

            try
            {
                tzId = GetTimeZoneID(clockTime.Timezone, true);
            }
            catch
            {
                throw;
            }

            string testtime;
            if (string.IsNullOrWhiteSpace(clockTime.Formatstring))
            {
                clockTime.Formatstring = "t";
            }
            else
            {
                clockTime.Formatstring = clockTime.Formatstring.Replace("\r", " ").Replace("\n", string.Empty);
            }

            try
            {
                testtime = new DateTime(2000, 10, 20, 20, 50, 50).ToString(clockTime.Formatstring);
            }
            catch
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentException("Invalid time format string", nameof(clockTime.Formatstring));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            try
            {
                (_, width, height, _, _) = GetPaint(clockTime.X, clockTime.Y, testtime, clockTime.HorizAlign, clockTime.VertAlign,
                    clockTime.Font, clockTime.FontSize, clockTime.FontWeight, clockTime.FontWidth, clockTime.TextColor, true);
            }
            catch (ArgumentException)
            {
                throw;
            }

            _clocks[tzId].AddTime(clockTime, width, height);
            ExportClock(tzId);
            return this;
        }

        private IRenderService DeleteClock(RenderActions.ClockDelete clockDelete)
        {
            string tzId;
            try
            {
                tzId = GetTimeZoneID(clockDelete.Timezone, true);
            }
            catch
            {
                throw;
            }

            _clocks[tzId].Dispose();
            _clocks.Remove(tzId);
            string filepath = _settings.Statefolder + "IoTDisplayClock-" + CleanFileName(tzId) + ".json";
            if (File.Exists(filepath))
            {
                File.Delete(filepath);
            }

            ExportClocks(false);
            return this;
        }

        private string GetTimeZoneID(string timezone, bool verifyExists)
        {
            TimeZoneInfo tzi;
            if (string.IsNullOrWhiteSpace(timezone))
            {
                tzi = TimeZoneInfo.Local;
            }
            else
            {
                try
                {
                    tzi = TZConvert.GetTimeZoneInfo(timezone) ?? throw new ArgumentException("Time zone could not be found", nameof(timezone));
                }
                catch
                {
                    throw new ArgumentException("Time zone could not be found", nameof(timezone));
                }
            }

            if (verifyExists && !_clocks.ContainsKey(tzi.Id))
            {
                throw new ArgumentException("Clock not found for this time zone", nameof(timezone));
            }

            return tzi.Id;
        }

        private SKBitmap GetImage(int x, int y, string filename)
        {
            SKBitmap img = null;
            if (x < 0 || x >= _settings.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "X coordinate is not within the screen");
            }

            if (y < 0 || y >= _settings.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "Y coordinate is not within the screen");
            }

            if (File.Exists(filename))
            {
                try
                {
                    img = SKBitmap.Decode(filename) ?? throw new ArgumentException("Unable to decode image", nameof(filename));
                }
                catch (Exception ex)
                {
                    if (img != null)
                    {
                        img.Dispose();
                    }

                    throw new ArgumentException("An unknown exception occured trying to load image:" + ex.Message, nameof(filename));
                }
            }
            else
            {
                throw new ArgumentException("File not found", nameof(filename));
            }

            return img;
        }

        private SKImage GetPicture(int x, int y, int width, int height, string svgCommands)
        {
            if (x < 0 || x >= _settings.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "X coordinate is not within the screen");
            }

            if (y < 0 || y >= _settings.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "Y coordinate is not within the screen");
            }

            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero");
            }

            if (string.IsNullOrWhiteSpace(svgCommands))
            {
                svgCommands = _settings.Foreground.ToString();
            }

            if (SKColor.TryParse(svgCommands, out _))
            {
                svgCommands = "<rect width=\"" + width.ToString() + "\" height=\"" + height.ToString() + "\" fill=\"" + svgCommands + "\" />";
            }

            byte[] fullSVG = Encoding.UTF8.GetBytes("<svg version=\"1.2\" baseProfile=\"full\" width=\"" + width.ToString() + "\" height=\"" +
                height.ToString() + "\" " + "xmlns=\"http://www.w3.org/2000/svg\">" + svgCommands + "</svg>");
            SKImage img;
            try
            {
                SKSvg svg = new ();
                using MemoryStream stream = new (fullSVG);
                using SKPicture pict = svg.Load(stream);
                SKSizeI dimen = new (
                    (int)Math.Ceiling(pict.CullRect.Width),
                    (int)Math.Ceiling(pict.CullRect.Height));
                img = SKImage.FromPicture(pict, dimen, SKMatrix.CreateScale(1, 1)) ?? throw new ArgumentException("Invalid SVG command", nameof(svgCommands));
                stream.Close();
            }
            catch (XmlException)
            {
                throw new ArgumentException("The SVG commands could not be parsed", nameof(svgCommands));
            }

            return img;
        }

        private (SKPaint paint, int width, int height, int hoffset, int voffset) GetPaint(int x, int y, string text, int horizAlign, int vertAlign,
            string font, float fontSize, int fontWeight, int fontWidth, string hexColor, bool bold)
        {
            if (x < 0 || x >= _settings.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "X coordinate is not within the screen");
            }

            if (y < 0 || y >= _settings.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "Y coordinate is not within the screen");
            }

            if (horizAlign < -1 || horizAlign > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(horizAlign), horizAlign, "Horizontal alignment must be -1, 0 or 1");
            }

            if (vertAlign < -1 || vertAlign > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(vertAlign), vertAlign, "Vertical alignment must be -1, 0 or 1");
            }

            if (fontSize <= 0 || fontSize > 9999)
            {
                throw new ArgumentOutOfRangeException(nameof(fontSize), fontSize, "Font size must be greater than zero and less than 10000");
            }

            if ((fontWeight < 100 && fontWeight != 0) || fontWeight > 900)
            {
                throw new ArgumentOutOfRangeException(nameof(fontWeight), fontWeight, "Font weight must be between 100 and 900");
            }

            if (fontWidth < 0 || fontWidth > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(fontWidth), fontWidth, "Font width must be between 1 and to 9");
            }

            SKPaint paint = new ()
            {
                TextSize = fontSize,
                IsAntialias = true,
            };
            if (!string.IsNullOrWhiteSpace(font))
            {
                paint.Typeface = GetTypeface(font, fontWeight, fontWidth) ?? throw new ArgumentException("Font not found", nameof(font));
            }

            if (string.IsNullOrWhiteSpace(hexColor))
            {
                paint.Color = new (0, 0, 0);
            }
            else
            {
                try
                {
                    paint.Color = SKColor.Parse(hexColor);
                }
                catch (ArgumentException)
                {
                    throw new ArgumentException("Invalid hexColor", nameof(hexColor));
                }
            }

            paint.FakeBoldText = bold;
            paint.IsStroke = false;
            SKRect bound = new ();
            float width = paint.MeasureText(text, ref bound);
            float height = bound.Height;
            float hoffset;
            if (horizAlign == -1)
            {
                hoffset = bound.Left - 1;
            }
            else if (horizAlign == 1)
            {
                hoffset = 1 - bound.Right;
            }
            else
            {
                hoffset = 1 - bound.MidX;
            }

            float voffset;
            if (vertAlign == -1)
            {
                voffset = 1 - bound.Top;
            }
            else if (vertAlign == 1)
            {
                voffset = bound.Bottom - 1;
            }
            else
            {
                voffset = 1 - bound.MidY;
            }

            return (paint, (int)Math.Round(width), (int)Math.Round(height), (int)Math.Round(hoffset), (int)Math.Round(voffset));
        }

        private void ExportClock(string TimeZoneId)
        {
            string filepath = _settings.Statefolder + "IoTDisplayClock-" + CleanFileName(TimeZoneId) + ".json";

            using FileStream fs = new (filepath, FileMode.Create, FileAccess.Write, FileShare.Read);
            fs.Write(Encoding.UTF8.GetBytes(_clocks[TimeZoneId].ToString()));
        }

        private void ExportClocks(bool clockState)
        {
            string filepath = _settings.Statefolder + "IoTDisplayClocks.txt";

            using FileStream fs = new (filepath, FileMode.Create, FileAccess.Write, FileShare.Read);
            foreach (KeyValuePair<string, ClockService> clock in _clocks)
            {
                fs.Write(Encoding.UTF8.GetBytes(clock.Key + "\n"));
                if (clockState)
                {
                    ExportClock(clock.Key);
                }
            }
        }

        private void Export(string command)
        {
            lock (_exportLock)
            {
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
                    ExportClocks(true);
                }
            }
        }

        private void Import(bool addClocks)
        {
            lock (_exportLock)
            {
                string screenpath = _settings.Statefolder + "IoTDisplayScreen.png";
                string commandpath = _settings.Statefolder + "IoTDisplayCommands.txt";
                string clockpath = _settings.Statefolder + "IoTDisplayClocks.txt";
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
                    JsonSerializerOptions options = new () { AllowTrailingCommas = true };
                    string command = string.Empty;
                    while ((command = sr.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(command))
                        {
                            string[] cmd = command.Split('\t');
                            switch (cmd[0])
                            {
                                case "image":
                                    AddImage(JsonSerializer.Deserialize<RenderActions.Image>(cmd[1], options), false);
                                    updated = true;
                                    break;
                                case "draw":
                                    AddDraw(JsonSerializer.Deserialize<RenderActions.Draw>(cmd[1], options), false);
                                    updated = true;
                                    break;
                                case "text":
                                    AddText(JsonSerializer.Deserialize<RenderActions.Text>(cmd[1], options), false, false);
                                    updated = true;
                                    break;
                                default:
                                    Console.WriteLine("Unknown render command in state file: " + cmd[0]);
                                    break;
                            }
                        }
                    }
                }

                if (addClocks && File.Exists(clockpath))
                {
                    using StreamReader sr = File.OpenText(clockpath);
                    string clock = string.Empty;
                    while ((clock = sr.ReadLine()) != null)
                    {
                        string filepath = _settings.Statefolder + "IoTDisplayClock-" + CleanFileName(clock) + ".json";
                        string json = string.Empty;
                        if (File.Exists(filepath))
                        {
                            json = File.ReadAllText(filepath, Encoding.UTF8);
                        }

                        Console.WriteLine("Adding clock " + clock + " with state: " + json);
                        _clocks.Add(clock, new (this, clock, _settings.Background.ToString(), json));
                    }
                }

                if (updated)
                {
                    OnScreenChanged(-1, -1, -1, -1, false);
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
        }

        #endregion Methods (Private)
    }
}
