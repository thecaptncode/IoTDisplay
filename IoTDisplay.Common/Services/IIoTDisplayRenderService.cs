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
using System.Drawing;

#endregion Using

namespace IoTDisplay.Common.Services
{
    public interface IIoTDisplayRenderService
    {
        #region Properties and Events

        public Bitmap Screen { get; }

        public event EventHandler ScreenChanged;

        #endregion Properties and Events

        #region Methods (Public)

        public void Create(int width, int height, int rotation, string statefolder, string background, string foreground);

        public IIoTDisplayRenderService Clear();

        public IIoTDisplayRenderService Refresh();

        public IIoTDisplayRenderService Image(IoTDisplayActionService.Image image, bool persist = true);

        public IIoTDisplayRenderService Draw(IoTDisplayActionService.Draw draw, bool persist = true);

        public IIoTDisplayRenderService Text(IoTDisplayActionService.Text text, bool bold = false, bool persist = true);

        public IIoTDisplayRenderService Clock(IoTDisplayActionService.Clock clock);

        public IIoTDisplayRenderService ClockClear();

        public IIoTDisplayRenderService ClockImage(IoTDisplayActionService.ClockImage clockImage);

        public IIoTDisplayRenderService ClockDraw(IoTDisplayActionService.ClockDraw clockDraw);

        public IIoTDisplayRenderService ClockTime(IoTDisplayActionService.ClockTime clockTime);

        public IIoTDisplayRenderService ClockDelete(IoTDisplayActionService.ClockDelete clockDelete);

        #endregion Methods (Public)
    }

    #region EventArgs (Public)

    public class ScreenChangedEventArgs : System.EventArgs
    {
        public int X { get; }

        public int Y { get; }

        public int Width { get; }

        public int Height { get; }

        public bool Delay { get; }

        public ScreenChangedEventArgs(int x, int y, int width, int height, bool delay)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Delay = delay;
        }

        #endregion EventArgs (Public)
    }

}
