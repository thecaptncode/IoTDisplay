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

#endregion Using

namespace IoTDisplay.Common.Services
{
    public interface IIoTDisplayService
    {
        #region Properties

        public IIoTDisplayRenderService Renderer { get; }

        public string DriverName { get; }

        public int ScreenWidth { get; }

        public int ScreenHeight { get; }

        public int ScreenRotation { get; }

        public TimeSpan RefreshTime { get; }

        public DateTime LastUpdated { get; }

        #endregion Properties
    }
}
