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
    using System.IO;
    using IoTDisplay.Common.Models;
    using IoTDisplay.Common.Services;
    using Waveshare.Devices;

    #endregion Using

    public static class DisplayServiceHelper
    {
        #region Methods (Public)

        public static IDisplayService GetService(AppSettings.Api Configuration)
        {
            // Configuration settings are obtained from appsettings.json)
            if (Configuration.Rotation != 0 && Configuration.Rotation != 90 && Configuration.Rotation != 180 && Configuration.Rotation != 270)
            {
                throw new ArgumentException("Rotation must be 0, 90, 180 or 270", nameof(Configuration));
            }

            if (Configuration.RefreshTime.Days != 0)
            {
                throw new ArgumentException("RefreshTime must not have a day portion", nameof(Configuration));
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

            IRenderService renderer = new RenderService();

            RenderSettings settings = new ()
            {
                Rotation = Configuration.Rotation,
                Statefolder = Configuration.StateFolder,
                Background = Configuration.BackgroundColor,
                Foreground = Configuration.ForegroundColor
            };

            EPaperDisplayType screenDriver = (EPaperDisplayType)System.Enum.Parse(typeof(EPaperDisplayType), Configuration.Driver, true);

            return new WsEPaperDisplayService(screenDriver, renderer, settings, Configuration.RefreshTime);
        }

        #endregion Methods (Public)
    }
}
