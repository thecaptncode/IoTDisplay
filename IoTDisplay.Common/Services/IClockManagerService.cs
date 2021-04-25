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

    using IoTDisplay.Common.Models;

    #endregion Using

    public interface IClockManagerService
    {
        #region Methods (Public)

        public void Configure(IRenderService renderer, RenderSettings settings);

        public IClockManagerService Clock(ClockActions.Clock clock);

        public IClockManagerService ClockClear();

        public IClockManagerService ClockImage(ClockActions.ClockImage clockImage);

        public IClockManagerService ClockDraw(ClockActions.ClockDraw clockDraw);

        public IClockManagerService ClockTime(ClockActions.ClockTime clockTime);

        public IClockManagerService ClockDelete(ClockActions.ClockDelete clockDelete);

        public void Import();

        public void Export();

        public void Dispose();

        #endregion Methods (Public)
    }
}
