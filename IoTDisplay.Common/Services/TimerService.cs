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
    using System.Timers;

    #endregion Using

    public class TimerService : Timer
    {
        #region Properties

        public TimeSpan TargetTime { get; set; }

        public int TargetMillisecond { get; set; }

        public int ToleranceMillisecond { get; set; }

        public new bool Enabled
        {
            get
            {
                return base.Enabled;
            }
            set
            {
                if (value)
                {
                    SetInterval();
                }

                base.Enabled = value;
            }
        }

        public double TimeLeft => (_dueTime - DateTime.Now).TotalMilliseconds;

        #endregion Properties

        #region Methods (Public)

        public new void Start()
        {
            SetInterval();
            base.Start();
        }

        #endregion Methods (Public)

        #region Fields

        private DateTime _dueTime;

        #endregion Fields

        #region Constructor / Dispose / Finalizer

        public TimerService()
            : base()
        {
            AutoReset = true;
            TargetTime = default;
            TargetMillisecond = 0;
            ToleranceMillisecond = 0;
            Elapsed += ElapsedAction;
        }

        protected new void Dispose()
        {
            Elapsed -= ElapsedAction;
            base.Dispose();
            Enabled = true;
        }

        #endregion Constructor / Dispose / Finalizer

        #region Methods (Private)

        private void SetInterval()
        {
            DateTime time = DateTime.Now;
            int next;
            // Calculate how long to wait for the next interval, shooting for the target
            // millisecond mark but not less than tolerance millisecond due to display update time.
            if (TargetTime == default)
            {
                int targetminute = 0;
                int targetsecond = TargetMillisecond;
                if (TargetMillisecond > 59999)
                {
                    targetminute = TargetMillisecond / 60000;
                    targetsecond = TargetMillisecond % 60000;
                }

                next = targetsecond - (time.Second * 1000 + time.Millisecond);
                if (next <= ToleranceMillisecond)
                {
                    next += 60000;
                }

                if (targetminute > 0)
                {
                    next += (targetminute - (time.AddMilliseconds(next).Minute % targetminute)) * 60000;
                }
            }

            // Calculate how long to wait for the next interval, shooting for the reset time
            // but not less than tolerance millisecond due to display update time.
            else
            {
                next = (int)((TargetTime - time.TimeOfDay).TotalMilliseconds);
                if (next <= ToleranceMillisecond)
                {
                    next += 24 * 60 * 60000;
                }
            }

            // Update the interval to prevent clock drift
            Interval = next;
            _dueTime = time.AddMilliseconds(next);
        }

        private void ElapsedAction(object sender, ElapsedEventArgs e)
        {
            if (AutoReset)
            {
                SetInterval();
            }
        }
        #endregion Methods (Private)
    }
}
