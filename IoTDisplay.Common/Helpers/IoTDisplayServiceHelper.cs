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

using IoTDisplay.Common.Services;
using System;
using Waveshare.Devices;

#endregion Using

namespace IoTDisplay.Common.Helpers
{
    public static class IoTDisplayServiceHelper
    {
        #region Methods (Public)

        public static IIoTDisplayService GetService(string DisplayType = "none", int Rotation = 0,
            string StateFolder = "", string BackgroundColor = "", string ForegroundColor = "", TimeSpan RefreshTime = default)
        {
            EPaperDisplayType ScreenDriver = (EPaperDisplayType)System.Enum.Parse(typeof(EPaperDisplayType), DisplayType, true);

            IIoTDisplayRenderService Renderer = new IoTDisplayRenderService();

            return new IoTDisplayService(ScreenDriver, Renderer, Rotation, StateFolder, BackgroundColor, ForegroundColor, RefreshTime);
        }

        #endregion Methods (Public)
    }
}
