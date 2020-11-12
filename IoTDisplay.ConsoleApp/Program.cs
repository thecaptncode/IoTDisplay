﻿#region Copyright

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

using IoTDisplay.Common;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;
using System.Threading.Tasks;

#endregion

namespace IoTDisplay.ConsoleApp
{
    [Command(Name = "IoTDisplay.exe",
        Description = "Run IoTDisplay Commands")]
    [HelpOption]
    [VersionOptionFromMember(MemberName = "GetVersion")]
    [Subcommand(
        typeof(IoTDisplayActionService.Clear),
        typeof(IoTDisplayActionService.Clock),
        typeof(IoTDisplayActionService.ClockClear),
        typeof(IoTDisplayActionService.ClockDelete),
        typeof(IoTDisplayActionService.ClockDraw),
        typeof(IoTDisplayActionService.ClockImage),
        typeof(IoTDisplayActionService.ClockTime),
        typeof(IoTDisplayActionService.Draw),
        typeof(IoTDisplayActionService.Get),
        typeof(IoTDisplayActionService.Image),
        typeof(IoTDisplayActionService.LastUpdated),
        typeof(IoTDisplayActionService.Refresh),
        typeof(IoTDisplayActionService.Text))
    ]
    class Program
    {
        #region Main
        private static async Task Main(string[] args)
        {
            try
            {
                await Host.CreateDefaultBuilder()
                    .RunCommandLineApplicationAsync<Program>(args);
            }
            catch (CommandParsingException ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }
        #endregion Main

        #region Methods (Private)
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify a command");
            app.ShowHelp();
            return 1;
        }

        private string GetVersion()
        {
            return typeof(Program).Assembly
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
        }

        #endregion Methods (Private)
    }
}
