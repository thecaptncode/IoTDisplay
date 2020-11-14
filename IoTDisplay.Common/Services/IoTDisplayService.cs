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
using System.Drawing;
using System.Timers;
using Waveshare;
using Waveshare.Devices;
using Waveshare.Interfaces;

#endregion Using

namespace IoTDisplay.Common.Services
{
    public class IoTDisplayService : IIoTDisplayService
    {
        #region Properties

        public IIoTDisplayRenderService Renderer { get; }

        public string DriverName { get; }

        public int ScreenWidth { get; }

        public int ScreenHeight { get; }

        public int ScreenRotation { get; }

        public TimeSpan RefreshTime { get; }

        public DateTime LastUpdated { get => lastUpdated; }

        #endregion Properties

        #region Methods (Public)


        #endregion Methods (Public)

        #region Fields

        private readonly IEPaperDisplay display;
        private readonly object updatelock = new();
        private static ClockTimer UpdateTimer;
        private static ClockTimer RefreshTimer;
        private DateTime lastUpdated;
        private bool updating = false;
        private bool delayed = false;

        #endregion Fields

        #region Constructor / Dispose / Finalizer

        public IoTDisplayService(EPaperDisplayType driver, IIoTDisplayRenderService renderer, int rotation,
            string statefolder, string background, string foreground, TimeSpan refreshtime)
        {
            DriverName = driver.ToString();
            display = EPaperDisplay.Create(driver);
            ScreenRotation = rotation;
            RefreshTime = refreshtime;
            if (display == null)
            {
                ScreenWidth = 800;
                ScreenHeight = 480;
            }
            else
            {
                ScreenWidth = display.Width;
                ScreenHeight = display.Height;
                display.Clear();
                display.PowerOff();
            }
            if (rotation == 90 || rotation == 270)
            {
                int tempwidth = ScreenWidth;
                ScreenWidth = ScreenHeight;
                ScreenHeight = tempwidth;
            }
            Renderer = renderer;
            renderer.ScreenChanged += Renderer_ScreenChanged;
            UpdateTimer = new()
            {
                TargetMillisecond = 300000,
                ToleranceMillisecond = 5000,
                Enabled = true
            };
            UpdateTimer.Elapsed += UpdateScreen;
            lastUpdated = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            renderer.Create(ScreenWidth, ScreenHeight, ScreenRotation, statefolder, background, foreground);
            if (display != null && refreshtime != default)
            {
                RefreshTimer = new()
                {
                    TargetTime = refreshtime,
                    ToleranceMillisecond = 180000,
                    Enabled = true
                };
                RefreshTimer.Elapsed += RefreshScreen;
            }
        }


        /// <summary>
        /// Finalizer
        /// </summary>
        ~IoTDisplayService()
        {
            if (display != null)
            {
                if (UpdateTimer != null)
                {
                    UpdateTimer.Elapsed -= UpdateScreen;
                    UpdateTimer.Enabled = false;
                    UpdateTimer.Dispose();
                }
                if (RefreshTimer != null)
                {
                    RefreshTimer.Elapsed -= RefreshScreen;
                    RefreshTimer.Enabled = false;
                    RefreshTimer.Dispose();
                }
                display.Sleep();
                display.Dispose();
            }
        }

        #endregion Constructor / Dispose / Finalizer

        #region Methods (Private)

        private DateTime GetLastUpdated()
        {
            return lastUpdated;
        }

        private void Renderer_ScreenChanged(object sender, EventArgs e)
        {
            if (display == null)
            {
                lastUpdated = DateTime.UtcNow;
            }
            else
            {
                ScreenChangedEventArgs args = (ScreenChangedEventArgs)e;
                lock (updatelock)
                {
                    if (!updating)
                    {
                        if (args.Delay)
                        {
                            delayed = true;
                        }
                        else
                        {
                            // Factor in rotation and accumulate ** TODO **
                            // Math.Min(args.Width, ScreenWidth - args.X), Math.Min(args.Height, ScreenHeight - args.Y) ** TODO **
                            updating = true;
                            UpdateTimer.Interval = 5000;
                        }
                    }
                }
            }
        }

        private void UpdateScreen(Object source, ElapsedEventArgs e)
        {
            if (delayed || updating)
                lock (display)
                {
                    Bitmap bitmap = null;
                    lock (updatelock)
                    {
                        bitmap = Renderer.Screen;
                        updating = false;
                        delayed = false;
                    }
                    // Handle if partial update ** TODO **
                    display.PowerOn();
                    display.DisplayImage(bitmap);
                    display.PowerOff();
                    bitmap.Dispose();
                    lastUpdated = DateTime.UtcNow;
                }
        }

        private void RefreshScreen(Object source, ElapsedEventArgs e)
        {
            // Screen flushing.  Cycle three times:
            //   Color: black, white, white, black, white, white
            //   Monochrome: black, white
            lock (display)
            {
                display.PowerOn();
                for (int i = 0; i < 6; i++)
                {
                    Console.WriteLine("Flushing screen");
                    display.ClearBlack();
                    display.Clear();
                }
                display.PowerOff();
                Console.WriteLine("Finished flushing screen");
                Renderer.Refresh();
            }
        }

        #endregion Methods (Private)
    }
}
