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

using System;
using System.Collections.Generic;
using System.Text.Json;

#endregion Using

namespace IoTDisplay.Common.Services
{
    class IoTDisplayClock : IDisposable
    {
        #region Properties

        public string TimeZoneId { get; }

        #endregion Properties

        #region Methods (Public)

        public void AddImage(IoTDisplayActionService.ClockImage clockImage, int width, int height)
        {
            RenderCommand cmd = new()
            {
                Type = 'I',
                X = clockImage.X,
                Y = clockImage.Y,
                Width = width,
                Height = height,
                Format = clockImage.Filename
            };
            commandlist.Add(cmd);
        }

        public void AddDraw(IoTDisplayActionService.ClockDraw clockDraw)
        {
            RenderCommand cmd = new()
            {
                Type = 'D',
                X = clockDraw.X,
                Y = clockDraw.Y,
                Width = clockDraw.Width,
                Height = clockDraw.Height,
                HexColor = clockDraw.SvgCommands
            };
            commandlist.Add(cmd);
        }

        public void AddTime(IoTDisplayActionService.ClockTime clockTime, int width, int height)
        {
            RenderCommand cmd = new()
            {
                Type = 'T',
                X = clockTime.X,
                Y = clockTime.Y,
                Format = clockTime.Formatstring,
                HorizAlign = clockTime.HorizAlign,
                VertAlign = clockTime.VertAlign,
                Font = clockTime.Font,
                FontSize = clockTime.FontSize,
                FontWeight = clockTime.FontWeight,
                FontWidth = clockTime.FontWidth,
                HexColor = clockTime.TextColor,
                BackgroundColor = clockTime.BackgroundColor,
                Width = width,
                Height = height
            };
            commandlist.Add(cmd);
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize<List<RenderCommand>>(commandlist);
        }

        #endregion Methods (Public)

        #region Fields
        private bool _disposed = false;
        private bool hasDrawn = false;
        private static ClockTimer TickTimer;
        private readonly IIoTDisplayRenderService renderer;
        private readonly List<RenderCommand> commandlist;
        private readonly string ScreenBackgroundColor;

        #endregion Fields

        #region Constructor / Dispose / Finalizer
        public IoTDisplayClock(IIoTDisplayRenderService renderer, string timezoneID, string screenbackground, string commands)
        {
            this.renderer = renderer;
            this.TimeZoneId = timezoneID;
            this.ScreenBackgroundColor = screenbackground;
            if (string.IsNullOrWhiteSpace(commands))
            {
                commandlist = new();
            }
            else
            {
                JsonSerializerOptions options = new()
                {
                    AllowTrailingCommas = true
                };
                commandlist = JsonSerializer.Deserialize<List<RenderCommand>>(commands, options);
            }
            TickTimer = new()
            {
                TargetMillisecond = 55000,
                ToleranceMillisecond = 5000,
                Enabled = true
            };
            TickTimer.Elapsed += UpdateClock;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                TickTimer.Elapsed -= UpdateClock;
                TickTimer.Enabled = false;
                TickTimer.Dispose();
                Clear();
            }

            commandlist.Clear();
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~IoTDisplayClock() => Dispose(false);

        #endregion Constructor / Dispose / Finalizer

        #region Methods (Private)
        private void UpdateClock(Object source, System.Timers.ElapsedEventArgs e)
        {
            // Get the time using the given time zone
            DateTime time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));

            // If the time is within 10 seconds of a new minute, use the next minute on the clock
            if (time.Second > 50)
                time = time.AddSeconds(60 - time.Second).AddMilliseconds(0 - time.Millisecond);

            try
            {
                foreach (RenderCommand item in commandlist)
                    if (item.Type == 'T' && item.LastText != null && item.LastText != time.ToString(item.Format))
                        renderer.Text(new()
                        {
                            X = item.X,
                            Y = item.Y,
                            Value = item.LastText,
                            HorizAlign = item.HorizAlign,
                            VertAlign = item.VertAlign,
                            Font = item.Font,
                            FontSize = item.FontSize,
                            FontWeight = item.FontWeight,
                            FontWidth = item.FontWidth,
                            HexColor = item.BackgroundColor,
                            Delay = true
                        }, true, false);
                foreach (RenderCommand item in commandlist)
                {
                    switch (item.Type)
                    {
                        case 'I':
                            renderer.Image(new() { X = item.X, Y = item.Y, Filename = item.Format, Delay = hasDrawn }, false);
                            break;
                        case 'D':
                            string cmd = string.Format(item.HexColor, time);
                            bool hasEmbeded = (cmd != item.HexColor);
                            if (!hasEmbeded || item.LastText == null || item.LastText != cmd)
                            {
                                if (hasEmbeded)
                                    item.LastText = cmd;
                                renderer.Draw(new()
                                {
                                    X = item.X,
                                    Y = item.Y,
                                    Width = item.Width,
                                    Height = item.Height,
                                    SvgCommands = cmd,
                                    Delay = hasDrawn
                                }, false);
                            }
                            hasDrawn = true;
                            break;
                        case 'T':
                            if (item.LastText == null || item.LastText != time.ToString(item.Format))
                            {
                                item.LastText = time.ToString(item.Format);
                                renderer.Text(new()
                                {
                                    X = item.X,
                                    Y = item.Y,
                                    Value = time.ToString(item.Format),
                                    HorizAlign = item.HorizAlign,
                                    VertAlign = item.VertAlign,
                                    Font = item.Font,
                                    FontSize = item.FontSize,
                                    FontWeight = item.FontWeight,
                                    FontWidth = item.FontWidth,
                                    HexColor = item.HexColor,
                                    Delay = hasDrawn
                                }, false, false);
                            }
                            hasDrawn = true;
                            break;
                        default:
                            Console.WriteLine("Unknown clock command found in clock " + TimeZoneId + ": " + item.Type.ToString());
                            break;
                    }
                }
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("An arguement exception occured in clock " + TimeZoneId + " on parameter " + ex.ParamName + " with message: " + ex.Message);
            }
            catch
            {
                throw;
            }
        }

        private void Clear()
        {
            foreach (RenderCommand item in commandlist)
            {
                switch (item.Type)
                {
                    case 'T':
                        if (item.LastText != null)
                            renderer.Text(new()
                            {
                                X = item.X,
                                Y = item.Y,
                                Value = item.LastText,
                                HorizAlign = item.HorizAlign,
                                VertAlign = item.VertAlign,
                                Font = item.Font,
                                FontSize = item.FontSize,
                                FontWeight = item.FontWeight,
                                FontWidth = item.FontWidth,
                                HexColor = ScreenBackgroundColor,
                                Delay = false
                            }, true, false);
                        break;
                    case 'I':
                    case 'D':
                        renderer.Draw(new()
                        {
                            X = item.X,
                            Y = item.Y,
                            Width = item.Width,
                            Height = item.Height,
                            SvgCommands = ScreenBackgroundColor,
                            Delay = false
                        }, false);
                        hasDrawn = true;
                        break;
                }
            }
        }

        #endregion Methods (Private)

        #region Subclasses (Protected Internal)

        protected internal class RenderCommand
        {
            public RenderCommand() { }
            public char Type { get; init; }
            public int X { get; init; }
            public int Y { get; init; }
            public int Width { get; init; }
            public int Height { get; init; }
            public string Font { get; init; }
            public float FontSize { get; init; }
            public int FontWeight { get; init; }
            public int FontWidth { get; init; }
            public string HexColor { get; init; }
            public string BackgroundColor { get; init; }
            public string Format { get; init; }
            public int HorizAlign { get; init; }
            public int VertAlign { get; init; }
            public string LastText { get; set; }

        }

        #endregion Subclasses (Protected Internal)
    }
}
