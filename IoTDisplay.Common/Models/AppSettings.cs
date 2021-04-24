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

namespace IoTDisplay.Common.Models
{
    #region Using

    using System;
    using System.Collections.Generic;
    using SkiaSharp;

    #endregion Using

    public class AppSettings
    {
        public class Api
        {
            #region Properties (subclass)

            public List<DriverDetails> Drivers { get; set; }
            public int Width { get; set; } = 800;
            public int Height { get; set; } = 480;
            public int Rotation { get; set; }
            public string StateFolder { get; set; }
            public SKColor BackgroundColor { get; set; } = SKColors.White;
            public SKColor ForegroundColor { get; set; } = SKColors.Black;

            #endregion Properties (subclass)

            public class DriverDetails
            {
                #region Properties (subclass)

                public string DriverType { get; set; }
                public string Driver { get; set; }
                public TimeSpan RefreshTime { get; set; } = default;

                #endregion Properties (subclass)
            }
        }

        public class Console
        {
            #region Properties (subclass)

            public string BaseUrl { get; set; } = "http://localhost:5000/api/Action/";

            #endregion Properties (subclass)
        }
    }
}
