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
    using System.Threading;
    using System.Timers;
    using IoTDisplay.Common.Models;
    using Waveshare;
    using Waveshare.Devices;
    using Waveshare.Interfaces;

    #endregion Using

    public class WsEPaperDisplayService : IDisplayService, IDisposable
    {
        #region Properties

        public string DriverName => _driverName;

        public DateTime LastUpdated => _lastUpdated;

        #endregion Properties

        #region Methods (Public)

        public void Configure(IRenderService renderer, RenderSettings setting) => Create(renderer, setting);

        #endregion Methods (Public)

        #region Fields

        private static TimerService _updateTimer;
        private static TimerService _refreshTimer;
        private readonly IEPaperDisplay _display;
        private readonly int _displayLockTimeout = 60000;
        private readonly object _updatelock = new ();
        private readonly int _updateLockTimeout = 60000;
        private int _sectionX1 = int.MaxValue;
        private int _sectionY1 = int.MaxValue;
        private int _sectionX2 = int.MinValue;
        private int _sectionY2 = int.MinValue;
        private DateTime _lastUpdated;
        public string _driverName;
        private readonly TimeSpan _refreshTime;
        private IRenderService _renderer;
        private bool _updating = false;
        private bool _delayed = false;
        private bool _disposed = false;

        #endregion Fields

        #region Constructor / Dispose / Finalizer

        public WsEPaperDisplayService(EPaperDisplayType driver, TimeSpan refreshtime)
        {
            _driverName = driver.ToString();
            _display = EPaperDisplay.Create(driver);
            _refreshTime = refreshtime;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
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

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~WsEPaperDisplayService() => Dispose(false);

        #endregion Constructor / Dispose / Finalizer

        #region Methods (Private)

        private void Create(IRenderService renderer, RenderSettings settings)
        {
            Console.WriteLine($"Starting eXoCooLd.Waveshare.EPaperDisplay driver: {_driverName}");
            if (_display != null)
            {
                settings.Resize(_display.Width, _display.Height);
                _display.Clear();
                _display.PowerOff();
            }

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
            if (_display != null && _refreshTime != default)
            {
                _refreshTimer = new ()
                {
                    TargetTime = _refreshTime,
                    ToleranceMillisecond = 180000,
                    Enabled = true
                };
                _refreshTimer.Elapsed += RefreshScreen;
            }
        }

        private void Renderer_ScreenChanged(object sender, EventArgs e)
        {
            if (_display == null)
            {
                _lastUpdated = DateTime.UtcNow;
            }
            else
            {
                ScreenChangedEventArgs args = (ScreenChangedEventArgs)e;
                bool lockSuccess = false;
                try
                {
                    Monitor.TryEnter(_updatelock, _updateLockTimeout, ref lockSuccess);
                    if (!lockSuccess)
                    {
                        throw new TimeoutException("A wait for update lock timed out.");
                    }

                    if (!_updating)
                    {
                        if (args.Delay)
                        {
                            _delayed = true;
                        }
                        else
                        {
                            int x2 = args.Width + args.X - 1;
                            int y2 = args.Height + args.Y - 1;
                            _sectionX1 = Math.Min(args.X, _sectionX1);
                            _sectionY1 = Math.Min(args.Y, _sectionY1);
                            _sectionX2 = Math.Max(x2, _sectionX2);
                            _sectionY2 = Math.Max(y2, _sectionY2);
                            _updating = true;
                            _updateTimer.Interval = 5000;
                        }
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
        }

        private void UpdateScreen(Object source, ElapsedEventArgs e)
        {
            if (_delayed || _updating)
            {
                bool lockSuccess = false;
                try
                {
                    Monitor.TryEnter(_display, _displayLockTimeout, ref lockSuccess);
                    if (!lockSuccess)
                    {
                        throw new TimeoutException("A wait for display lock timed out.");
                    }

                    Stream memStream = null;
                    try
                    {
                        lockSuccess = false;
                        Monitor.TryEnter(_updatelock, _updateLockTimeout, ref lockSuccess);
                        if (!lockSuccess)
                        {
                            throw new TimeoutException("A wait for update lock timed out.");
                        }

                        memStream = _renderer.Screen;
                        _updating = false;
                        _delayed = false;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("An exception occurred trying to get screen to update. " + ex.Message);
                    }
                    finally
                    {
                        if (lockSuccess)
                        {
                            Monitor.Exit(_updatelock);
                        }
                    }

                    // Implement partial update and factor in isportrait ** TODO **
                    _display.PowerOn();
                    _display.DisplayImage(new (memStream));
                    _display.PowerOff();
                    memStream.Close();
                    memStream.Dispose();
                    _lastUpdated = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An exception occurred trying to update display. " + ex.Message);
                }
                finally
                {
                    if (lockSuccess)
                    {
                        Monitor.Exit(_display);
                    }
                }
            }
        }

        private void RefreshScreen(Object source, ElapsedEventArgs e)
        {
            // Screen flushing.  Cycle three times:
            //   Color: black, white, white, black, white, white
            //   Monochrome: black, white
            bool lockSuccess = false;
            try
            {
                Monitor.TryEnter(_display, _displayLockTimeout, ref lockSuccess);
                if (!lockSuccess)
                {
                    throw new TimeoutException("A wait for display lock timed out.");
                }

                _display.PowerOn();
                for (int i = 0; i < 6; i++)
                {
                    Console.WriteLine("Flushing display");
                    _display.ClearBlack();
                    _display.Clear();
                }

                _display.PowerOff();
                Console.WriteLine("Finished flushing display");
                _renderer.Refresh();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception occurred flushing display. " + ex.Message);
            }
            finally
            {
                if (lockSuccess)
                {
                    Monitor.Exit(_display);
                }
            }
        }

        #endregion Methods (Private)
    }
}
