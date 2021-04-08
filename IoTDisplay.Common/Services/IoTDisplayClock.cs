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
    using System.Text.Json;

    #endregion Using

    public class IoTDisplayClock : IDisposable
    {
        #region Properties

        public string TimeZoneId { get; }

        #endregion Properties

        #region Methods (Public)

        public void AddImage(IoTDisplayActionService.ClockImage clockImage, int width, int height)
        {
            RenderCommand cmd = new ()
            {
                Type = 'I',
                X = clockImage.X,
                Y = clockImage.Y,
                Width = width,
                Height = height,
                Format = clockImage.Filename
            };
            _commandlist.Add(cmd);
        }

        public void AddDraw(IoTDisplayActionService.ClockDraw clockDraw)
        {
            RenderCommand cmd = new ()
            {
                Type = 'D',
                X = clockDraw.X,
                Y = clockDraw.Y,
                Width = clockDraw.Width,
                Height = clockDraw.Height,
                HexColor = clockDraw.SvgCommands
            };
            _commandlist.Add(cmd);
        }

        public void AddTime(IoTDisplayActionService.ClockTime clockTime, int width, int height)
        {
            RenderCommand cmd = new ()
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
            _commandlist.Add(cmd);
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize<List<RenderCommand>>(_commandlist);
        }

        #endregion Methods (Public)

        #region Fields
        private static ClockTimer _tickTimer;
        private readonly IIoTDisplayRenderService _renderer;
        private readonly List<RenderCommand> _commandlist;
        private readonly string _screenBackgroundColor;
        private bool _disposed = false;
        private bool _hasDrawn = false;

        #endregion Fields

        #region Constructor / Dispose / Finalizer
        public IoTDisplayClock(IIoTDisplayRenderService renderer, string timezoneID, string screenbackground, string commands)
        {
            _renderer = renderer;
            TimeZoneId = timezoneID;
            _screenBackgroundColor = screenbackground;
            if (string.IsNullOrWhiteSpace(commands))
            {
                _commandlist = new ();
            }
            else
            {
                JsonSerializerOptions options = new ()
                {
                    AllowTrailingCommas = true
                };
                _commandlist = JsonSerializer.Deserialize<List<RenderCommand>>(commands, options);
            }

            _tickTimer = new ()
            {
                TargetMillisecond = 55000,
                ToleranceMillisecond = 5000,
                Enabled = true
            };
            _tickTimer.Elapsed += UpdateClock;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _tickTimer.Elapsed -= UpdateClock;
                _tickTimer.Enabled = false;
                _tickTimer.Dispose();
                Clear();
            }

            _commandlist.Clear();
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
            {
                time = time.AddSeconds(60 - time.Second).AddMilliseconds(0 - time.Millisecond);
            }

            try
            {
                foreach (RenderCommand item in _commandlist)
                {
                    if (item.Type == 'T' && item.LastText != null && item.LastText != time.ToString(item.Format))
                    {
                        _renderer.Text(new ()
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
                    }
                }

                foreach (RenderCommand item in _commandlist)
                {
                    switch (item.Type)
                    {
                        case 'I':
                            _renderer.Image(new () { X = item.X, Y = item.Y, Filename = item.Format, Delay = _hasDrawn }, false);
                            break;
                        case 'D':
                            string cmd = string.Format(item.HexColor, time);
                            bool hasEmbeded = (cmd != item.HexColor);
                            if (!hasEmbeded || item.LastText == null || item.LastText != cmd)
                            {
                                if (hasEmbeded)
                                {
                                    item.LastText = cmd;
                                }

                                _renderer.Draw(new ()
                                {
                                    X = item.X,
                                    Y = item.Y,
                                    Width = item.Width,
                                    Height = item.Height,
                                    SvgCommands = cmd,
                                    Delay = _hasDrawn
                                }, false);
                            }

                            _hasDrawn = true;
                            break;
                        case 'T':
                            if (item.LastText == null || item.LastText != time.ToString(item.Format))
                            {
                                item.LastText = time.ToString(item.Format);
                                _renderer.Text(new ()
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
                                    Delay = _hasDrawn
                                }, false, false);
                            }

                            _hasDrawn = true;
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
            foreach (RenderCommand item in _commandlist)
            {
                switch (item.Type)
                {
                    case 'T':
                        if (item.LastText != null)
                        {
                            _renderer.Text(new ()
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
                                HexColor = _screenBackgroundColor,
                                Delay = false
                            }, true, false);
                        }

                        break;
                    case 'I':
                    case 'D':
                        _renderer.Draw(new ()
                        {
                            X = item.X,
                            Y = item.Y,
                            Width = item.Width,
                            Height = item.Height,
                            SvgCommands = _screenBackgroundColor,
                            Delay = false
                        }, false);
                        _hasDrawn = true;
                        break;
                }
            }
        }

        #endregion Methods (Private)

        #region Subclasses (Protected Internal)

        protected internal class RenderCommand
        {
            public RenderCommand()
            {
            }

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
