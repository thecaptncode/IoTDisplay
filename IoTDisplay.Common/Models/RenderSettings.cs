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
    using SkiaSharp;

    #endregion Using

    public class RenderSettings
    {
        #region Properties

        public int Width { get; init; } = 800;
        public int Height { get; init; } = 480;
        public int Rotation { get; init; }
        public bool IsPortrait { get; }
        public string Statefolder { get; init; }
        public SKColor Background { get; init; }
        public SKColor Foreground { get; init; }

        #endregion Properties

        #region Constructor

        public RenderSettings()
        {
            if (Width < 1 || Width > 9999)
            {
                throw new ArgumentException("Width must be greater than 0 and less than 10000", nameof(Width));
            }

            if (Height < 1 || Height > 9999)
            {
                throw new ArgumentException("Height must be greater than 0 and less than 10000", nameof(Height));
            }

            if (Rotation == 0 || Rotation == 180)
            {
                IsPortrait = false;
            }
            else if (Rotation == 90 || Rotation == 270)
            {
                IsPortrait = true;
                int width = Width;
                Width = Height;
                Height = width;
            }
            else
            {
                throw new ArgumentException("Rotation must be 0, 90, 180 or 270", nameof(Rotation));
            }
        }

        #endregion Constructor
    }
}
