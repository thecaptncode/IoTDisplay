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
    using System.Text;
    using System.Threading;
    using IoTDisplay.Common.Models;
    using SkiaSharp;
    using TimeZoneConverter;

    #endregion Using

    public class ClockManagerService : IClockManagerService, IDisposable
    {
        #region Properties

        public void Configure(IRenderService renderer, RenderSettings settings) => Create(renderer, settings);

        public IClockManagerService Clock(ClockActions.Clock clock) => AddClock(clock);

        public IClockManagerService ClockClear() => ClearClocks();

        public IClockManagerService ClockImage(ClockActions.ClockImage clockImage) => AddImage(clockImage);

        public IClockManagerService ClockDraw(ClockActions.ClockDraw clockDraw) => AddDraw(clockDraw);

        public IClockManagerService ClockTime(ClockActions.ClockTime clockTime) => AddTime(clockTime);

        public IClockManagerService ClockDelete(ClockActions.ClockDelete clockDelete) => DeleteClock(clockDelete);

        public void Import() => ImportClocks();

        public void Export() => ExportClocks(true);

        #endregion Properties

        #region Fields

        private readonly IDictionary<string, ClockService> _clocks = new Dictionary<string, ClockService>();
        private IRenderService _renderer;
        private RenderSettings _settings;
        private bool _disposed = false;

        #endregion Fields

        #region Constructor / Dispose / Finalizer

        public ClockManagerService()
        {
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (KeyValuePair<string, ClockService> clock in _clocks)
                {
                    clock.Value.Dispose();
                }

                _clocks.Clear();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ClockManagerService() => Dispose(false);

        #endregion Constructor / Dispose / Finalizer

        private void Create(IRenderService renderer, RenderSettings settings)
        {
            _renderer = renderer;
            _settings = settings;
        }

        private IClockManagerService ClearClocks()
        {
            int clocksecond = DateTime.Now.Second;
            if (clocksecond > 48)
            {
                Thread.Sleep((61 - clocksecond) * 1000);
            }

            foreach (KeyValuePair<string, ClockService> clock in _clocks)
            {
                DeleteClock(new () { Timezone = clock.Value.TimeZoneId });
            }

            return this;
        }

        private IClockManagerService AddClock(ClockActions.Clock clock)
        {
            string tzId;
            try
            {
                tzId = GetTimeZoneID(clock.Timezone, false);
            }
            catch
            {
                throw;
            }

            if (_clocks.ContainsKey(tzId))
            {
                _clocks[tzId].Dispose();
                _clocks.Remove(tzId);
            }

            _clocks.Add(tzId, new (_renderer, tzId, _settings.Background.ToString(), string.Empty));
            ExportClocks(false);
            return this;
        }

        private IClockManagerService AddImage(ClockActions.ClockImage clockImage)
        {
            string tzId;
            int width = 0;
            int height = 0;
            try
            {
                tzId = GetTimeZoneID(clockImage.Timezone, true);
                using SKBitmap img = RenderTools.GetImage(_settings, clockImage.X, clockImage.Y, clockImage.Filename);
                width = img.Width;
                height = img.Height;
            }
            catch (ArgumentException)
            {
                throw;
            }

            _clocks[tzId].AddImage(clockImage, width, height);
            ExportClock(tzId);
            return this;
        }

        private IClockManagerService AddDraw(ClockActions.ClockDraw clockDraw)
        {
            string tzId;
            if (!string.IsNullOrEmpty(clockDraw.SvgCommands))
            {
                clockDraw.SvgCommands = clockDraw.SvgCommands.Replace("\r", " ").Replace("\n", string.Empty);
            }

            try
            {
                tzId = GetTimeZoneID(clockDraw.Timezone, true);
                using SKImage img = RenderTools.GetPicture(_settings, clockDraw.X, clockDraw.Y, clockDraw.Width, clockDraw.Height,
                    clockDraw.SvgCommands);
            }
            catch
            {
                throw;
            }

            _clocks[tzId].AddDraw(clockDraw);
            ExportClock(tzId);
            return this;
        }

        private IClockManagerService AddTime(ClockActions.ClockTime clockTime)
        {
            string tzId;
            int width;
            int height;
            if (clockTime.FontSize == 0)
            {
                clockTime.FontSize = 32;
            }

            try
            {
                tzId = GetTimeZoneID(clockTime.Timezone, true);
            }
            catch
            {
                throw;
            }

            string testtime;
            if (string.IsNullOrWhiteSpace(clockTime.Formatstring))
            {
                clockTime.Formatstring = "t";
            }
            else
            {
                clockTime.Formatstring = clockTime.Formatstring.Replace("\r", " ").Replace("\n", string.Empty);
            }

            try
            {
                testtime = new DateTime(2000, 10, 20, 20, 50, 50).ToString(clockTime.Formatstring);
            }
            catch
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentException("Invalid time format string", nameof(clockTime.Formatstring));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            try
            {
                (_, width, height, _, _, _, _) = RenderTools.GetPaint(_settings, clockTime.X, clockTime.Y, testtime, clockTime.HorizAlign, clockTime.VertAlign,
                    clockTime.Font, clockTime.FontSize, clockTime.FontWeight, clockTime.FontWidth, clockTime.TextColor, true);
            }
            catch (ArgumentException)
            {
                throw;
            }

            _clocks[tzId].AddTime(clockTime, width, height);
            ExportClock(tzId);
            return this;
        }

        private IClockManagerService DeleteClock(ClockActions.ClockDelete clockDelete)
        {
            string tzId;
            try
            {
                tzId = GetTimeZoneID(clockDelete.Timezone, true);
            }
            catch
            {
                throw;
            }

            _clocks[tzId].Dispose();
            _clocks.Remove(tzId);
            string filepath = _settings.Statefolder + "IoTDisplayClock-" + RenderTools.CleanFileName(tzId) + ".json";
            if (File.Exists(filepath))
            {
                File.Delete(filepath);
            }

            ExportClocks(false);
            return this;
        }

        private string GetTimeZoneID(string timezone, bool verifyExists)
        {
            TimeZoneInfo tzi;
            if (string.IsNullOrWhiteSpace(timezone))
            {
                tzi = TimeZoneInfo.Local;
            }
            else
            {
                try
                {
                    tzi = TZConvert.GetTimeZoneInfo(timezone) ?? throw new ArgumentException("Time zone could not be found", nameof(timezone));
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("An exception occurred finding time zone: " + ex.Message, nameof(timezone), ex);
                }
            }

            if (verifyExists && !_clocks.ContainsKey(tzi.Id))
            {
                throw new ArgumentException("Clock not found for this time zone", nameof(timezone));
            }

            return tzi.Id;
        }

        private void ExportClock(string TimeZoneId)
        {
            string filepath = _settings.Statefolder + "IoTDisplayClock-" + RenderTools.CleanFileName(TimeZoneId) + ".json";

            using FileStream fs = new (filepath, FileMode.Create, FileAccess.Write, FileShare.Read);
            fs.Write(Encoding.UTF8.GetBytes(_clocks[TimeZoneId].ToString()));
        }

        private void ExportClocks(bool clockState)
        {
            string filepath = _settings.Statefolder + "IoTDisplayClocks.txt";

            using FileStream fs = new (filepath, FileMode.Create, FileAccess.Write, FileShare.Read);
            foreach (KeyValuePair<string, ClockService> clock in _clocks)
            {
                fs.Write(Encoding.UTF8.GetBytes(clock.Key + "\n"));
                if (clockState)
                {
                    ExportClock(clock.Key);
                }
            }
        }

        private void ImportClocks()
        {
            string clockpath = _settings.Statefolder + "IoTDisplayClocks.txt";
            if (File.Exists(clockpath))
            {
                using StreamReader sr = File.OpenText(clockpath);
                string clock = string.Empty;
                while ((clock = sr.ReadLine()) != null)
                {
                    string filepath = _settings.Statefolder + "IoTDisplayClock-" + RenderTools.CleanFileName(clock) + ".json";
                    string json = string.Empty;
                    if (File.Exists(filepath))
                    {
                        json = File.ReadAllText(filepath, Encoding.UTF8);
                    }

                    Console.WriteLine("Adding clock " + clock + " with state: " + json);
                    _clocks.Add(clock, new (_renderer, clock, _settings.Background.ToString(), json));
                }
            }
        }
    }
}
