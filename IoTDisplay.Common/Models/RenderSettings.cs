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

        public int Width => _width;
        public int Height => _height;
        public int Rotation { get; init; }
        public bool IsPortrait => _isPortrait;
        public string Statefolder { get; init; }
        public SKColor Background { get; init; }
        public SKColor Foreground { get; init; }
        public bool IncludeCommand { get; set; }

        #endregion Properties

        #region Methods (Public)

        public void Resize(int width, int height) => ChangeScreen(width, height);

        #endregion Methods (Public)

        #region Fields

        private int _width;
        private int _height;
        private bool _isPortrait;

        #endregion Fields

        #region Constructor

        public RenderSettings()
        {
        }

        #endregion Constructor

        #region Methods (Private)

        private void ChangeScreen(int width, int height)
        {
            if (width < 1 || width > 9999)
            {
                // throw new ArgumentException("Width must be greater than 0 and less than 10000", nameof(width));
            }

            if (height < 1 || height > 9999)
            {
                // throw new ArgumentException("Height must be greater than 0 and less than 10000", nameof(height));
            }

            if (Rotation == 0 || Rotation == 180)
            {
                _width = width;
                _height = height;
                _isPortrait = false;
            }
            else if (Rotation == 90 || Rotation == 270)
            {
                _width = height;
                _height = width;
                _isPortrait = true;
            }
            else
            {
                throw new ArgumentException("Rotation must be 0, 90, 180 or 270");
            }
        }

        #endregion Methods (Private)
    }
}
