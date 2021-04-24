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

namespace IoTDisplay.Common.Helpers
{
    #region Using

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using IoTDisplay.Common.Models;
    using IoTDisplay.Common.Services;
    using Waveshare.Devices;

    #endregion Using

    public static class DisplayServiceHelper
    {
        #region Methods (Public)

        public static IRenderService GetService(AppSettings.Api Configuration)
        {
            // Configuration settings are obtained from appsettings.json)
            if (Configuration.Rotation != 0 && Configuration.Rotation != 90 && Configuration.Rotation != 180 && Configuration.Rotation != 270)
            {
                throw new ArgumentException("Rotation must be 0, 90, 180 or 270", nameof(Configuration));
            }

            if (string.IsNullOrWhiteSpace(Configuration.StateFolder))
            {
                Configuration.StateFolder = Path.GetTempPath();
            }

            if (!Configuration.StateFolder.EndsWith(Path.DirectorySeparatorChar))
            {
                Configuration.StateFolder += Path.DirectorySeparatorChar;
            }

            if (!Directory.Exists(Configuration.StateFolder))
            {
                throw new ArgumentException("StateFolder does not point to an existing folder", nameof(Configuration));
            }

            RenderSettings settings = new ()
            {
                Width = Configuration.Width,
                Height = Configuration.Height,
                Rotation = Configuration.Rotation,
                Statefolder = Configuration.StateFolder,
                Background = Configuration.BackgroundColor,
                Foreground = Configuration.ForegroundColor,
                IncludeCommand = false
            };

            IClockManagerService clocks = new ClockManagerService();
            List<IDisplayService> displays = new ();
            foreach (AppSettings.Api.DriverDetails driver in Configuration.Drivers)
            {
                if (driver.DriverType.Equals("eXoCooLd.Waveshare.EPaperDisplay", StringComparison.OrdinalIgnoreCase))
                {
                    if (driver.RefreshTime.Days != 0)
                    {
                        throw new ArgumentException("RefreshTime must not have a day portion", nameof(Configuration));
                    }

                    EPaperDisplayType screenDriver;
                    try
                    {
                        screenDriver = (EPaperDisplayType)Enum.Parse(typeof(EPaperDisplayType), driver.Driver, true);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException("Unable to find eXoCooLd.Waveshare.EPaperDisplay driver", nameof(Configuration), ex);
                    }

                    if (screenDriver != EPaperDisplayType.None)
                    {
                        displays.Add(new WsEPaperDisplayService(screenDriver, driver.RefreshTime));
                    }
                }
                else if (driver.DriverType.Equals("IPCSocket", StringComparison.OrdinalIgnoreCase))
                {
                    Socket screenDriver = new (AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    try
                    {
                        if (File.Exists(driver.Driver))
                        {
                            File.Delete(driver.Driver);
                        }

                        screenDriver.Bind(new UnixDomainSocketEndPoint(driver.Driver));
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException("Unable to establish IPCSocket end point", nameof(Configuration), ex);
                    }

                    displays.Add(new SocketDisplayService(screenDriver));
                }
            }

            return new RenderService(settings, clocks, displays);
        }

        #endregion Methods (Public)
    }
}
