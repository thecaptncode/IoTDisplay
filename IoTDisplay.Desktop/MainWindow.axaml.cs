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

namespace IoTDisplay.Desktop
{
    #region Using

    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Interactivity;
    using Avalonia.Markup.Xaml;
    using Avalonia.Threading;
    using Avalonia.VisualTree;

    #endregion Using

    public class MainWindow : Window
    {
        #region Constructor

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
#if DEBUG
            this.AttachDevTools();
#endif
        }

        #endregion Constructor

        #region Methods (Private)

        private void MainWindow_Opened(object sender, EventArgs e)
        {
            StatusChange();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            GraphicsClient iotDisplay = this.FindControl<GraphicsClient>("iotDisplay");
            iotDisplay.Disconnect();
        }

        private void BtnReconnect_Click(object sender, RoutedEventArgs e)
        {
            GraphicsClient iotDisplay = this.FindControl<GraphicsClient>("iotDisplay");
            iotDisplay.Reconnect();
        }

        private void IotDisplay_ConnectionChanged(object sender, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(StatusChange, DispatcherPriority.Background);
        }

        private void IotDisplay_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            Vector change = e.Delta;
            Point pos = e.GetPosition((IVisual)sender);
            double chg;
            string dir;
            if (change.X < 0)
            {
                chg = change.X;
                dir = "left";
            }
            else if (change.X > 0)
            {
                chg = change.X;
                dir = "right";
            }
            else if (change.Y < 0)
            {
                chg = change.Y;
                dir = "up";
            }
            else if (change.Y > 0)
            {
                chg = change.Y;
                dir = "down";
            }
            else
            {
                chg = 0;
                dir = "stationary";
            }

            Display($"Scrolled {dir} at speed {Math.Round(chg, 3)} and position {Math.Round(pos.X, 0)}, {Math.Round(pos.Y)}");
        }

        private void IotDisplay_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            PointerPointProperties prop = e.GetCurrentPoint((IVisual)sender).Properties;
            Point pos = e.GetPosition((IVisual)sender);
            Display($"{prop.PointerUpdateKind} " +
                $"{(e.Pointer.Type == PointerType.Touch ? "touched" : "clicked")} at position {Math.Round(pos.X, 0)}, {Math.Round(pos.Y)}");
        }

        private void Display(string evt)
        {
            TextBox box = this.FindControl<TextBox>("txtEvent");
            if (box.Text == null)
            {
                box.Text = evt;
            }
            else
            {
                if (box.Text.Length > 2000)
                {
                    box.Text = box.Text[1000..];
                }

                box.Text += Environment.NewLine + evt;
                box.CaretIndex = int.MaxValue;
            }
        }

        private void StatusChange()
        {
            TextBox box = this.FindControl<TextBox>("txtEvent");
            GraphicsClient iotDisplay = this.FindControl<GraphicsClient>("iotDisplay");
            box.Text = iotDisplay.IsConnected ? "Connected to server." : "Disconnected from server.";
            if (!string.IsNullOrEmpty(iotDisplay.ConnectionMessage))
            {
                box.Text += Environment.NewLine + iotDisplay.ConnectionMessage;
            }

            iotDisplay.IsVisible = iotDisplay.IsConnected;
        }
        #endregion Methods (Private)
    }
}
