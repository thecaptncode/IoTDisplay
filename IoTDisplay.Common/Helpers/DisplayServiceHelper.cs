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
    using System.Net;
    using System.Net.Sockets;
    using IoTDisplay.Common.Models;
    using IoTDisplay.Common.Services;
    using Waveshare.Devices;

    #endregion Using

    public static class DisplayServiceHelper
    {
        #region Methods (Public)

        public static IRenderService GetService(AppSettings.Api configuration)
        {
            // Configuration settings are obtained from appsettings.json)
            if (configuration.Rotation != 0 && configuration.Rotation != 90 && configuration.Rotation != 180 && configuration.Rotation != 270)
            {
                throw new ArgumentException("Rotation must be 0, 90, 180 or 270", nameof(configuration));
            }

            if (string.IsNullOrWhiteSpace(configuration.StateFolder))
            {
                configuration.StateFolder = Path.GetTempPath();
            }

            if (!configuration.StateFolder.EndsWith(Path.DirectorySeparatorChar))
            {
                configuration.StateFolder += Path.DirectorySeparatorChar;
            }

            if (!Directory.Exists(configuration.StateFolder))
            {
                throw new ArgumentException("StateFolder does not point to an existing folder", nameof(configuration));
            }

            RenderSettings settings = new ()
            {
                Rotation = configuration.Rotation,
                Statefolder = configuration.StateFolder,
                Background = configuration.BackgroundColor,
                Foreground = configuration.ForegroundColor,
                IncludeCommand = false,
            };
            settings.Resize(configuration.Width, configuration.Height);

            IClockManagerService clocks = new ClockManagerService();
            List<IDisplayService> displays = new ();
            foreach (AppSettings.Api.DriverDetails driver in configuration.Drivers)
            {
                if (driver.DriverType.Equals("eXoCooLd.Waveshare.EPaperDisplay", StringComparison.OrdinalIgnoreCase))
                {
                    if (driver.RefreshTime.Days != 0)
                    {
                        throw new ArgumentException("RefreshTime must not have a day portion", nameof(configuration));
                    }

                    EPaperDisplayType screenDriver;
                    try
                    {
                        screenDriver = (EPaperDisplayType)Enum.Parse(typeof(EPaperDisplayType), driver.Driver, true);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException("Unable to find eXoCooLd.Waveshare.EPaperDisplay driver", nameof(configuration), ex);
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
                        throw new ArgumentException("Unable to establish IPCSocket end point", nameof(configuration), ex);
                    }

                    displays.Add(new SocketDisplayService(screenDriver));
                }
                else if (driver.DriverType.Equals("TCPSocket", StringComparison.OrdinalIgnoreCase))
                {
                    Uri listener;
                    try
                    {
                        listener = new ("net.tcp://" + (string.IsNullOrWhiteSpace(driver.Driver) ? Dns.GetHostName() : driver.Driver));
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException($"Exception occurred trying to resolve listener {driver.Driver}", nameof(configuration), ex);
                    }

                    Socket screenDriver;
                    try
                    {
                        IPAddress ipAddress;
                        if (listener.HostNameType == UriHostNameType.Dns)
                        {
                            ipAddress = Dns.GetHostEntry(listener.DnsSafeHost).AddressList[0];
                        }
                        else
                        {
                            ipAddress = IPAddress.Parse(listener.DnsSafeHost);
                        }

                        screenDriver = new (ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        screenDriver.Bind(new IPEndPoint(ipAddress, listener.Port < 0 || listener.Port == 808 ? 11000 : listener.Port));
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException("Unable to establish TCPSocket end point", nameof(configuration), ex);
                    }

                    displays.Add(new SocketDisplayService(screenDriver));
                }
            }

            return new RenderService(settings, clocks, displays);
        }

        #endregion Methods (Public)
    }
}
