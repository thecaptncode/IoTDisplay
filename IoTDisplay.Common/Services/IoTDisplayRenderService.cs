#region Copyright

// --------------------------------------------------------------------------
// Copyright 2020 Greg Cannon
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

#region Using

using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using TimeZoneConverter;

#endregion Using

namespace IoTDisplay.Common.Services
{
    public class IoTDisplayRenderService : IIoTDisplayRenderService
    {
        #region Properties and Events

        public event EventHandler ScreenChanged;

        public Bitmap Screen => GetScreen();

        #endregion Properties and Events

        #region Methods (Public)

        public void Create(int width, int height, int rotation, string statefolder, string background, string foreground) =>
            SetScreen(width, height, rotation, statefolder, background, foreground);

        public IIoTDisplayRenderService Clear() => ClearScreen();

        public IIoTDisplayRenderService Refresh() => RefreshScreen();

        public IIoTDisplayRenderService Image(IoTDisplayActionService.Image image, bool persist = true) => AddImage(image, persist);

        public IIoTDisplayRenderService Draw(IoTDisplayActionService.Draw draw, bool persist = true) => AddDraw(draw, persist);

        public IIoTDisplayRenderService Text(IoTDisplayActionService.Text text, bool bold = false, bool persist = true) =>
            AddText(text, bold, persist);

        public IIoTDisplayRenderService Clock(IoTDisplayActionService.Clock clock) => AddClock(clock);

        public IIoTDisplayRenderService ClockClear() => ClearClocks();

        public IIoTDisplayRenderService ClockImage(IoTDisplayActionService.ClockImage clockImage) => AddClock(clockImage);

        public IIoTDisplayRenderService ClockDraw(IoTDisplayActionService.ClockDraw clockDraw) => AddClock(clockDraw);

        public IIoTDisplayRenderService ClockTime(IoTDisplayActionService.ClockTime clockTime) => AddClock(clockTime);

        public IIoTDisplayRenderService ClockDelete(IoTDisplayActionService.ClockDelete clockDelete) => DeleteClock(clockDelete);

        #endregion Methods (Public)

        #region Fields

        private int ScreenWidth;

        private int ScreenHeight;

        private RotateFlipType ScreenRotation;

        private string StateFolder;

        private string BackgroundColor = "#ffffff";

        private string ForegroundColor = "#000000";

        private SKBitmap screen;

        private SKCanvas canvas;

        private IDictionary<string, IoTDisplayClock> clocks;

        private readonly object exportlock = new object();

        #endregion Fields

        #region Constructor

        public IoTDisplayRenderService()
        {
        }

        #endregion Constructor

        #region Methods (Protected)

        protected virtual void OnScreenChanged(int x, int y, int width, int height, bool delay)
        {
            ScreenChangedEventArgs evt = new ScreenChangedEventArgs(x, y, width, height, delay);
            ScreenChanged?.Invoke(this, evt);
        }

        #endregion Methods (Protected)

        #region Methods (Private)

        private void SetScreen(int width, int height, int rotation, string statefolder, string background, string foreground)
        {
            ScreenWidth = width;
            ScreenHeight = height;
            switch (rotation)
            {
                case 90:
                    ScreenRotation = RotateFlipType.Rotate90FlipNone;
                    break;
                case 180:
                    ScreenRotation = RotateFlipType.Rotate180FlipNone;
                    break;
                case 270:
                    ScreenRotation = RotateFlipType.Rotate270FlipNone;
                    break;
                default:
                    ScreenRotation = RotateFlipType.RotateNoneFlipNone;
                    break;
            }
            StateFolder = statefolder;
            if (!string.IsNullOrWhiteSpace(background) && SKColor.TryParse(background, out SKColor tempcolor))
                BackgroundColor = background;
            if (!string.IsNullOrWhiteSpace(foreground) && SKColor.TryParse(foreground, out tempcolor))
                ForegroundColor = foreground;
            screen = new SKBitmap(width, height);
            canvas = new SKCanvas(screen);
            canvas.Clear(SKColor.Parse(BackgroundColor));
            clocks = new Dictionary<string, IoTDisplayClock>();
            Import(true);
        }

        private Bitmap GetScreen(bool useRotation = true)
        {
            using (MemoryStream memStream = new MemoryStream())
            using (SKManagedWStream wstream = new SKManagedWStream(memStream))
            {
                screen.Encode(wstream, SKEncodedImageFormat.Png, 100);
                memStream.Position = 0;
                Bitmap image = new Bitmap(memStream);
                if (useRotation)
                    image.RotateFlip(ScreenRotation);
                return image;
            }
        }

        private IIoTDisplayRenderService ClearScreen()
        {
            canvas.Clear(SKColor.Parse(BackgroundColor));

            string screenpath = StateFolder + "IoTDisplayScreen.png";
            string commandpath = StateFolder + "IoTDisplayCommands.txt";

            if (File.Exists(screenpath))
                File.Delete(screenpath);

            if (File.Exists(commandpath))
                File.Delete(commandpath);

            OnScreenChanged(-1, -1, -1, -1, false);
            return this;
        }

        private IIoTDisplayRenderService RefreshScreen()
        {
            canvas.Clear(SKColor.Parse(BackgroundColor));
            Import(false);

            OnScreenChanged(-1, -1, -1, -1, false);
            return this;
        }


        private IIoTDisplayRenderService AddImage(IoTDisplayActionService.Image image, bool persist = true)
        {
            int width = 0;
            int height = 0;
            try
            {
                using (SKBitmap img = GetImage(image.X, image.Y, image.Filename))
                {
                    canvas.DrawBitmap(img, image.X, image.Y);
                    width = img.Width;
                    height = img.Height;
                }
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("An unknown exception occured trying to add image to the canvas:" + ex.Message, nameof(image.Filename));
            }

            if (persist)
            {
                bool saveDelay = image.Delay;
                image.Delay = true;
                Export("image\t" + JsonSerializer.Serialize<IoTDisplayActionService.Image>(image));
                image.Delay = saveDelay;
            }
            OnScreenChanged(image.X, image.Y, width, height, image.Delay);
            return this;
        }

        private IIoTDisplayRenderService AddDraw(IoTDisplayActionService.Draw draw, bool persist = true)
        {
            try
            {
                using (SKImage img = GetPicture(draw.X, draw.Y, draw.Width, draw.Height, draw.SvgCommands))
                    canvas.DrawImage(img, draw.X, draw.Y);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("An unknown exception occured trying to add drawing to the canvas:" + ex.Message, nameof(draw.SvgCommands));
            }

            if (persist)
            {
                bool saveDelay = draw.Delay;
                draw.Delay = true;
                Export("draw\t" + JsonSerializer.Serialize<IoTDisplayActionService.Draw>(draw));
                draw.Delay = saveDelay;
            }
            OnScreenChanged(draw.X, draw.Y, draw.Width, draw.Height, draw.Delay);
            return this;
        }

        private IIoTDisplayRenderService AddText(IoTDisplayActionService.Text text, bool bold = false, bool persist = true)
        {
            int width = 0;
            int height = 0;
            if (string.IsNullOrWhiteSpace(text.Value))
                return this;
            if (text.FontSize == 0)
                text.FontSize = 32;
            text.Value = text.Value.Replace("\r", " ").Replace("\n", "");
            SKPaint paint = null;
            try
            {
                int hoffset, voffset;
                (paint, width, height, hoffset, voffset) = GetPaint(text.X, text.Y, text.Value, text.HorizAlign, text.VertAlign,
                    text.Font, text.FontSize, text.FontWeight, text.FontWidth, text.HexColor, bold);
                canvas.DrawText(text.Value, text.X + hoffset, text.Y + voffset, paint);
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
                    paint.Dispose();
            }

            if (persist)
            {
                bool saveDelay = text.Delay;
                text.Delay = true;
                Export("text\t" + JsonSerializer.Serialize<IoTDisplayActionService.Text>(text));
                text.Delay = saveDelay;
            }
            OnScreenChanged(text.X, text.Y, width, height, text.Delay);
            return this;
        }

        private IIoTDisplayRenderService ClearClocks()
        {
            int clocksecond = DateTime.Now.Second;
            if (clocksecond > 48)
                Thread.Sleep((61 - clocksecond) * 1000);
            foreach (KeyValuePair<string, IoTDisplayClock> clock in clocks)
                DeleteClock(new IoTDisplayActionService.ClockDelete { Timezone = clock.Value.TimeZoneId });
            return this;
        }

        private IIoTDisplayRenderService AddClock(IoTDisplayActionService.Clock clock)
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
            if (clocks.ContainsKey(tzId))
            {
                clocks[tzId].Dispose();
                clocks.Remove(tzId);
            }

            clocks.Add(tzId, new IoTDisplayClock(this, tzId, BackgroundColor, ""));
            ExportClocks(false);
            return this;
        }

        private IIoTDisplayRenderService AddClock(IoTDisplayActionService.ClockImage clockImage)
        {
            string tzId;
            int width = 0;
            int height = 0;
            try
            {
                tzId = GetTimeZoneID(clockImage.Timezone, true);
                using (SKBitmap img = GetImage(clockImage.X, clockImage.Y, clockImage.Filename))
                {
                    width = img.Width;
                    height = img.Height;
                }
            }
            catch (ArgumentException)
            {
                throw;
            }

            clocks[tzId].AddImage(clockImage, width, height);
            ExportClock(tzId);
            return this;
        }

        private IIoTDisplayRenderService AddClock(IoTDisplayActionService.ClockDraw clockDraw)
        {
            string tzId;
            if (!string.IsNullOrEmpty(clockDraw.SvgCommands))
                clockDraw.SvgCommands = clockDraw.SvgCommands.Replace("\r", " ").Replace("\n", "");
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

            clocks[tzId].AddDraw(clockDraw);
            ExportClock(tzId);
            return this;
        }

        private IIoTDisplayRenderService AddClock(IoTDisplayActionService.ClockTime clockTime)
        {
            string tzId;
            int width = 0;
            int height = 0;
            if (clockTime.FontSize == 0)
                clockTime.FontSize = 32;
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
                clockTime.Formatstring = "t";
            else
                clockTime.Formatstring = clockTime.Formatstring.Replace("\r", " ").Replace("\n", "");
            try
            {
                testtime = new DateTime(2020, 12, 20, 23, 50, 50).ToString(clockTime.Formatstring);
            }
            catch
            {
                throw new ArgumentException("Invalid time format string", nameof(clockTime.Formatstring));
            }
            SKPaint paint = null;
            try
            {
                int hoffset, voffset;
                (paint, width, height, hoffset, voffset) = GetPaint(clockTime.X, clockTime.Y, testtime, clockTime.HorizAlign, clockTime.VertAlign,
                    clockTime.Font, clockTime.FontSize, clockTime.FontWeight, clockTime.FontWidth, clockTime.TextColor, true);
            }
            catch (ArgumentException)
            {
                throw;
            }
            finally
            {
                paint.Dispose();
            }

            clocks[tzId].AddTime(clockTime, width, height);
            ExportClock(tzId);
            return this;
        }

        private IIoTDisplayRenderService DeleteClock(IoTDisplayActionService.ClockDelete clockDelete)
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

            clocks[tzId].Dispose();
            clocks.Remove(tzId);
            string filepath = StateFolder + "IoTDisplayClock-" + CleanFileName(tzId) + ".json";
            if (File.Exists(filepath))
                File.Delete(filepath);
            ExportClocks(false);
            return this;
        }

        private string GetTimeZoneID(string timezone, bool verifyExists)
        {
            TimeZoneInfo tzi = null;
            if (string.IsNullOrWhiteSpace(timezone))
            {
                tzi = TimeZoneInfo.Local;
            }
            else
            {
                try
                {
                    tzi = TZConvert.GetTimeZoneInfo(timezone);
                    if (tzi == null)
                        throw new ArgumentException("Time zone could not be found", nameof(timezone));
                }
                catch
                {
                    throw new ArgumentException("Time zone could not be found", nameof(timezone));
                }
            }
            if (verifyExists && !clocks.ContainsKey(tzi.Id))
            {
                throw new ArgumentException("Clock not found for this time zone", nameof(timezone));
            }
            return tzi.Id;
        }

        private SKBitmap GetImage(int x, int y, string filename)
        {
            SKBitmap img = null;
            if (x < 0 || x > ScreenWidth)
                throw new ArgumentOutOfRangeException(nameof(x), x, "X coordinate is not within the screen");
            if (y < 0 || y > ScreenHeight)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Y coordinate is not within the screen");
            if (File.Exists(filename))
            {
                try
                {
                    img = SKBitmap.Decode(filename);
                    if (img == null)
                        throw new ArgumentException("Unable to decode image", nameof(filename));
                }
                catch (Exception ex)
                {
                    if (img != null)
                        img.Dispose();
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
            if (x < 0 || x >= ScreenWidth)
                throw new ArgumentOutOfRangeException(nameof(x), x, "X coordinate is not within the screen");
            if (y < 0 || y >= ScreenHeight)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Y coordinate is not within the screen");
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero");
            if (string.IsNullOrWhiteSpace(svgCommands))
                svgCommands = ForegroundColor;
            if (SKColor.TryParse(svgCommands, out SKColor tempcolor))
                svgCommands = "<rect width=\"" + width.ToString() + "\" height=\"" + height.ToString() + "\" fill=\"" + svgCommands + "\" />";
            byte[] fullSVG = Encoding.UTF8.GetBytes("<svg version=\"1.2\" baseProfile=\"full\" width=\"" + width.ToString() + "\" height=\"" + 
                height.ToString() + "\" " + "xmlns=\"http://www.w3.org/2000/svg\">" + svgCommands + "</svg>");
            SKImage img;
            try
            {
                SKSvg svg = new SKSvg();
                using (MemoryStream stream = new MemoryStream(fullSVG))
                using (SKPicture pict = svg.Load(stream))
                {
                    SKSizeI dimen = new SKSizeI(
                        (int)Math.Ceiling(pict.CullRect.Width),
                        (int)Math.Ceiling(pict.CullRect.Height)
                    );
                    img = SKImage.FromPicture(pict, dimen, SKMatrix.CreateScale(1, 1));
                    if (img == null)
                        throw new ArgumentException("Invalid SVG command", nameof(svgCommands));
                    stream.Close();
                }
            }
            catch (XmlException)
            {
                throw new ArgumentException("The SVG commands could not be parsed", nameof(svgCommands));
            }
            return img;
        }

        private (SKPaint, int, int, int, int) GetPaint(int x, int y, string text, int horizAlign, int vertAlign, string font, float fontSize,
            int fontWeight, int fontWidth, string hexColor, bool bold)
        {
            if (x < 0 || x >= ScreenWidth)
                throw new ArgumentOutOfRangeException(nameof(x), x, "X coordinate is not within the screen");
            if (y < 0 || y >= ScreenHeight)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Y coordinate is not within the screen");
            if (horizAlign < -1 || horizAlign > 1)
                throw new ArgumentOutOfRangeException(nameof(horizAlign), horizAlign, "Horizontal alignment must be -1, 0 or 1");
            if (vertAlign < -1 || vertAlign > 1)
                throw new ArgumentOutOfRangeException(nameof(vertAlign), vertAlign, "Vertical alignment must be -1, 0 or 1");
            if (fontSize <= 0 || fontSize > 9999)
                throw new ArgumentOutOfRangeException(nameof(fontSize), fontSize, "Font size must be greater than zero and less than 10000");
            if ((fontWeight < 100 && fontWeight != 0) || fontWeight > 900)
                throw new ArgumentOutOfRangeException(nameof(fontWeight), fontWeight, "Font weight must be between 100 and 900");
            if (fontWidth < 0 || fontWidth > 9)
                throw new ArgumentOutOfRangeException(nameof(fontWidth), fontWidth, "Font width must be between 1 and to 9");
            SKPaint paint = new SKPaint
            {
                TextSize = fontSize,
                IsAntialias = true,
            };
            if (!string.IsNullOrWhiteSpace(font))
            {
                using (SKTypeface typeface = GetTypeface(font, fontWeight, fontWidth))
                {
                    if (typeface == null)
                        throw new ArgumentException("Font not found", nameof(font));
                    else
                        paint.Typeface = typeface;
                }
            }
            if (string.IsNullOrWhiteSpace(hexColor))
            {
                paint.Color = new SKColor(0, 0, 0);
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
            SKRect bound = new SKRect();
            float width = paint.MeasureText(text, ref bound);
            float height = bound.Height;
            float hoffset;
            if (horizAlign == -1)
                hoffset = bound.Left - 1;
            else if (horizAlign == 1)
                hoffset = 1 - bound.Right;
            else
                hoffset = 1 - bound.MidX;
            float voffset;
            if (vertAlign == -1)
                voffset = 1 - bound.Top;
            else if (vertAlign == 1)
                voffset = bound.Bottom - 1;
            else
                voffset = 1 - bound.MidY;
            return (paint, (int)Math.Round(width), (int)Math.Round(height), (int)Math.Round(hoffset), (int)Math.Round(voffset));
        }

        private SKTypeface GetTypeface(string font, int weight, int width)
        {
            SKTypeface typeface = null;
            if (!string.IsNullOrWhiteSpace(font))
            {
                try
                {
                    if (File.Exists(font))
                        typeface = SKTypeface.FromFile(font);
                    else if (weight > 0 && width > 0)
                        typeface = SKTypeface.FromFamilyName(font, weight, width, SKFontStyleSlant.Upright);
                    else if (weight > 0)
                        typeface = SKTypeface.FromFamilyName(font, weight, 5, SKFontStyleSlant.Upright);
                    else if (width > 0)
                        typeface = SKTypeface.FromFamilyName(font, 400, width, SKFontStyleSlant.Upright);
                    else
                        typeface = SKTypeface.FromFamilyName(font);
                }
                catch
                {
                    typeface = null;
                }
            }
            return typeface;
        }

        private void ExportClock(string TimeZoneId)
        {
            string filepath = StateFolder + "IoTDisplayClock-" + CleanFileName(TimeZoneId) + ".json";

            using (FileStream fs = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Read))
                fs.Write(Encoding.UTF8.GetBytes(clocks[TimeZoneId].ToString()));
        }

        private void ExportClocks(bool clockState)
        {
            string filepath = StateFolder + "IoTDisplayClocks.txt";

            using (FileStream fs = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                foreach (KeyValuePair<string, IoTDisplayClock> clock in clocks)
                {
                    fs.Write(Encoding.UTF8.GetBytes(clock.Key + "\n"));
                    if (clockState)
                        ExportClock(clock.Key);
                }
            }
        }

        private void Export(string command)
        {
            lock (exportlock)
            {
                string screenpath = StateFolder + "IoTDisplayScreen.png";
                string commandpath = StateFolder + "IoTDisplayCommands.txt";

                if (!File.Exists(commandpath))
                {
                    using (StreamWriter sw = File.CreateText(commandpath))
                    {
                        sw.WriteLine(command.Replace("\r", " ").Replace("\n", ""));
                    }
                }
                else if (new FileInfo(commandpath).Length < 4096)
                {
                    using (StreamWriter sw = File.AppendText(commandpath))
                        sw.WriteLine(command.Replace("\r", " ").Replace("\n", ""));
                }
                else
                {
                    using (Bitmap currentScreen = GetScreen(false))
                        currentScreen.Save(screenpath, ImageFormat.Png);
                    File.Delete(commandpath);
                    ExportClocks(true);
                }
            }
        }

        private void Import(bool addClocks)
        {
            lock (exportlock)
            {
                string screenpath = StateFolder + "IoTDisplayScreen.png";
                string commandpath = StateFolder + "IoTDisplayCommands.txt";
                string clockpath = StateFolder + "IoTDisplayClocks.txt";
                bool updated = false;
                if (File.Exists(screenpath))
                {
                    AddImage(new IoTDisplayActionService.Image { X = 0, Y = 0, Filename = screenpath, Delay = true }, false);
                    Console.WriteLine("Previous screen restored");
                    updated = true;
                }
                if (File.Exists(commandpath))
                    using (StreamReader sr = File.OpenText(commandpath))
                    {
                        JsonSerializerOptions options = new JsonSerializerOptions { AllowTrailingCommas = true };
                        string command = "";
                        while ((command = sr.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(command))
                            {
                                string[] cmd = command.Split('\t');
                                switch (cmd[0])
                                {
                                    case "image":
                                        AddImage(JsonSerializer.Deserialize<IoTDisplayActionService.Image>(cmd[1], options), false);
                                        updated = true;
                                        break;
                                    case "draw":
                                        AddDraw(JsonSerializer.Deserialize<IoTDisplayActionService.Draw>(cmd[1], options), false);
                                        updated = true;
                                        break;
                                    case "text":
                                        AddText(JsonSerializer.Deserialize<IoTDisplayActionService.Text>(cmd[1], options), false, false);
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
                    using (StreamReader sr = File.OpenText(clockpath))
                    {
                        string clock = "";
                        while ((clock = sr.ReadLine()) != null)
                        {
                            string filepath = StateFolder + "IoTDisplayClock-" + CleanFileName(clock) + ".json";
                            string json = "";
                            if (File.Exists(filepath))
                                json = File.ReadAllText(filepath, Encoding.UTF8);
                            Console.WriteLine("Adding clock " + clock + " with state: " + json);
                            clocks.Add(clock, new IoTDisplayClock(this, clock, BackgroundColor, json));
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
                        execpath = execpath.Substring(5);
                    execpath = execpath.Trim('/').Trim('\\') + "/splash.png";
                    if (File.Exists(execpath))
                        AddImage(new IoTDisplayActionService.Image { X = 0, Y = 0, Filename = execpath, Delay = false }, false);
                }
            }
        }

        private static string CleanFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\/\\\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }

        #endregion Methods (Private)
    }
}
