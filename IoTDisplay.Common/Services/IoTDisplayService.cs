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
    using System.IO;
    using System.Timers;
    using Waveshare;
    using Waveshare.Devices;
    using Waveshare.Interfaces;

    #endregion Using

    public class IoTDisplayService : IIoTDisplayService
    {
        #region Properties

        public IIoTDisplayRenderService Renderer { get; init; }

        public IoTDisplayRenderSettings Settings { get; init; }

        public TimeSpan RefreshTime { get; init; }

        public string DriverName { get; }

        public DateTime LastUpdated { get => _lastUpdated; }

        #endregion Properties

        #region Methods (Public)

        #endregion Methods (Public)

        #region Fields

        private static ClockTimer _updateTimer;
        private static ClockTimer _refreshTimer;
        private readonly IEPaperDisplay _display;
        private readonly object _updatelock = new ();
        private DateTime _lastUpdated;
        private bool _updating = false;
        private bool _delayed = false;

        #endregion Fields

        #region Constructor / Dispose / Finalizer

        public IoTDisplayService(EPaperDisplayType driver, IIoTDisplayRenderService renderer, IoTDisplayRenderSettings settings, TimeSpan refreshtime)
        {
            DriverName = driver.ToString();
            _display = EPaperDisplay.Create(driver);
            RefreshTime = refreshtime;
            if (_display == null)
            {
                Settings = settings;
            }
            else
            {
                Settings = new IoTDisplayRenderSettings()
                {
                    Width = _display.Width,
                    Height = _display.Height,
                    Rotation = settings.Rotation,
                    Statefolder = settings.Statefolder,
                    Background = settings.Background,
                    Foreground = settings.Foreground
                };
                _display.Clear();
                _display.PowerOff();
            }

            Renderer = renderer;
            renderer.ScreenChanged += Renderer_ScreenChanged;
            _updateTimer = new ()
            {
                TargetMillisecond = 300000,
                ToleranceMillisecond = 5000,
                Enabled = true
            };
            _updateTimer.Elapsed += UpdateScreen;
            _lastUpdated = new (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            renderer.Create(Settings);
            if (_display != null && refreshtime != default)
            {
                _refreshTimer = new ()
                {
                    TargetTime = refreshtime,
                    ToleranceMillisecond = 180000,
                    Enabled = true
                };
                _refreshTimer.Elapsed += RefreshScreen;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~IoTDisplayService()
        {
            if (_display != null)
            {
                if (_updateTimer != null)
                {
                    _updateTimer.Elapsed -= UpdateScreen;
                    _updateTimer.Enabled = false;
                    _updateTimer.Dispose();
                }

                if (_refreshTimer != null)
                {
                    _refreshTimer.Elapsed -= RefreshScreen;
                    _refreshTimer.Enabled = false;
                    _refreshTimer.Dispose();
                }

                _display.Sleep();
                _display.Dispose();
            }
        }

        #endregion Constructor / Dispose / Finalizer

        #region Methods (Private)

        private void Renderer_ScreenChanged(object sender, EventArgs e)
        {
            if (_display == null)
            {
                _lastUpdated = DateTime.UtcNow;
            }
            else
            {
                ScreenChangedEventArgs args = (ScreenChangedEventArgs)e;
                lock (_updatelock)
                {
                    if (!_updating)
                    {
                        if (args.Delay)
                        {
                            _delayed = true;
                        }
                        else
                        {
                            // Factor in rotation, isportrait and accumulate ** TODO **
                            // Math.Min(args.Width, ScreenWidth - args.X), Math.Min(args.Height, ScreenHeight - args.Y) ** TODO **
                            _updating = true;
                            _updateTimer.Interval = 5000;
                        }
                    }
                }
            }
        }

        private void UpdateScreen(Object source, ElapsedEventArgs e)
        {
            if (_delayed || _updating)
            {
                lock (_display)
                {
                    Stream memStream = null;
                    lock (_updatelock)
                    {
                        memStream = Renderer.Screen;
                        _updating = false;
                        _delayed = false;
                    }

                    _display.PowerOn();
                    _display.DisplayImage(new (memStream));
                    _display.PowerOff();
                    memStream.Close();
                    memStream.Dispose();
                    _lastUpdated = DateTime.UtcNow;
                }
            }
        }

        private void RefreshScreen(Object source, ElapsedEventArgs e)
        {
            // Screen flushing.  Cycle three times:
            //   Color: black, white, white, black, white, white
            //   Monochrome: black, white
            lock (_display)
            {
                _display.PowerOn();
                for (int i = 0; i < 6; i++)
                {
                    Console.WriteLine("Flushing screen");
                    _display.ClearBlack();
                    _display.Clear();
                }

                _display.PowerOff();
                Console.WriteLine("Finished flushing screen");
                Renderer.Refresh();
            }
        }

        #endregion Methods (Private)
    }
}
