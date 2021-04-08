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
    using IoTDisplay.Common.Services;
    using Microsoft.Extensions.Configuration;
    using SkiaSharp;
    using Waveshare.Devices;

    #endregion Using

    public static class IoTDisplayServiceHelper
    {
        #region Methods (Public)

        public static IIoTDisplayService GetService(IConfiguration Configuration)
        {
            // Configuration settings are obtained from appsettings.json)
            if (!int.TryParse(Configuration["Rotation"], out int rotation) ||
                (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270))
            {
                throw new ArgumentException("Rotation must be 0, 90, 180 or 270", nameof(Configuration));
            }

            string stateFolder = Configuration["StateFolder"];
            if (string.IsNullOrWhiteSpace(stateFolder))
            {
                stateFolder = Path.GetTempPath();
            }

            if (!stateFolder.EndsWith(Path.DirectorySeparatorChar))
            {
                stateFolder += Path.DirectorySeparatorChar;
            }

            if (!Directory.Exists(stateFolder))
            {
                throw new ArgumentException("StateFolder does not point to an existing folder", nameof(Configuration));
            }

            string backgroundColor = Configuration["BackgroundColor"];
            SKColor background = SKColors.White;
            SKColor foreground = SKColors.Black;
            string foregroundColor = Configuration["ForegroundColor"];
            if (!string.IsNullOrWhiteSpace(backgroundColor) && !SKColor.TryParse(backgroundColor, out background))
            {
                throw new ArgumentException("Unable to parse BackgroundColor", nameof(Configuration));
            }

            if (!string.IsNullOrWhiteSpace(foregroundColor) && !SKColor.TryParse(foregroundColor, out foreground))
            {
                throw new ArgumentException("Unable to parse ForegroundColor", nameof(Configuration));
            }

            TimeSpan refreshTime = default;
            if (!string.IsNullOrWhiteSpace(Configuration["RefreshTime"]) && !TimeSpan.TryParse(Configuration["RefreshTime"], out refreshTime))
            {
                throw new ArgumentException("Unable to parse RefreshTime", nameof(Configuration));
            }

            if (refreshTime.Days != 0)
            {
                throw new ArgumentException("RefreshTime must not have a day portion", nameof(Configuration));
            }

            string driver = string.IsNullOrWhiteSpace(Configuration["Driver"]) ? "none" : Configuration["Driver"];

            EPaperDisplayType screenDriver = (EPaperDisplayType)System.Enum.Parse(typeof(EPaperDisplayType), driver, true);

            IIoTDisplayRenderService renderer = new IoTDisplayRenderService();

            IoTDisplayRenderSettings settings = new ()
            {
                Rotation = rotation,
                Statefolder = stateFolder,
                Background = background,
                Foreground = foreground
            };

            return new IoTDisplayService(screenDriver, renderer, settings, refreshTime);
        }

        #endregion Methods (Public)
    }
}
