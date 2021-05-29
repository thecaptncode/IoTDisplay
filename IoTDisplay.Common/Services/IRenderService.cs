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

    using System;
    using System.Collections.Generic;
    using System.IO;
    using IoTDisplay.Common.Models;

    #endregion Using

    public interface IRenderService
    {
        #region Properties and Events

        public RenderSettings Settings { get; }

        public List<IDisplayService> Displays { get; }

        public IClockManagerService Clocks { get; }

        public Stream Screen { get; }

        public event EventHandler ScreenChanged;

        #endregion Properties and Events

        #region Methods (Public)

        public IRenderService Clear();

        public IRenderService Refresh();

        public Stream ScreenAt(RenderActions.ScreenAt area);

        public IRenderService Image(RenderActions.Image image, bool persist = true);

        public IRenderService Draw(RenderActions.Draw draw, bool persist = true);

        public IRenderService Graphic(RenderActions.Graphic graphic, bool persist = true);

        public IRenderService Text(RenderActions.Text text, bool bold = false, bool persist = true);

        public bool RenderCommand(RenderActions.RenderCommand command);

        public void Dispose();

        #endregion Methods (Public)
    }

    #region EventArgs (Public)

    public class ScreenChangedEventArgs : EventArgs
    {
        public int X { get; init; }

        public int Y { get; init; }

        public int Width { get; init; }

        public int Height { get; init; }

        public bool Delay { get; init; }

        public RenderActions.RenderCommand Command { get; init; }
    }

    #endregion EventArgs (Public)
}
