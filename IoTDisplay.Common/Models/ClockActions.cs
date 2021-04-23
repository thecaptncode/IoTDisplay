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
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Extensions.Options;

    #endregion Using

    public class ClockActions
    {
        #region Subclasses
#pragma warning disable IDE0051 // Remove unused private members

        /// <summary>
        /// Clears the screen of all clocks
        /// </summary>
        [Command("clockclear", Description = "Clears the screen of all clocks")]
        public class ClockClear
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static",
                Justification = "CommandLineUtils needs this to be non static")]
            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Get, options.Value, "ClockClear", Array.Empty<byte>());
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Add a clock to the screen
        /// </summary>
        [Command("clock", Description = "Add a clock to the screen")]
        public class Clock
        {
            /// <summary>
            /// Time zone to use for the clock (blank for device default). This can be an IANA, Windows, or Rails time zone name.
            /// </summary>
            /// <example>America/New_York</example>
            [Option("-z|--timezone", CommandOptionType.SingleValue, Description = "Time zone to use for the clock (blank for device default)")]
            public string Timezone { get; set; }

            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Post, options.Value, "Clock",
                    JsonSerializer.SerializeToUtf8Bytes<Clock>(this));
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Add an image from a file to a clock
        /// </summary>
        [Command("clockimage", Description = "Add an image from a file to a clock")]
        public class ClockImage
        {
            /// <summary>
            /// Time zone of the clock (blank for device default). This must match an existing clock.
            /// </summary>
            /// <example>America/New_York</example>
            [Option("-z|--timezone", CommandOptionType.SingleValue, Description = "Time zone of the clock (blank for device default)")]
            public string Timezone { get; set; }

            /// <summary>
            /// X coordinate to place the image
            /// </summary>
            /// <example>10</example>
            [Required]
            [Option("-x", CommandOptionType.SingleValue, Description = "X coordinate to place the image")]
            public int X { get; set; }

            /// <summary>
            /// Y coordinate to place the image
            /// </summary>
            /// <example>100</example>
            [Required]
            [Option("-y", CommandOptionType.SingleValue, Description = "Y coordinate to place the image")]
            public int Y { get; set; }

            /// <summary>
            /// Filename of the image to place on the screen
            /// </summary>
            /// <example>/home/pi/nyc.png</example>
            [Required]
            [Option("-f|--filename", CommandOptionType.SingleValue, Description = "Filename of the image to place on the screen")]
            public string Filename { get; set; }

            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Post, options.Value, "ClockImage",
                    JsonSerializer.SerializeToUtf8Bytes<ClockImage>(this));
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Add a drawing to a clock
        /// </summary>
        [Command("clockdraw", Description = "Add a drawing to a clock")]
        public class ClockDraw
        {
            /// <summary>
            /// Time zone of the clock (blank for device default). This must match an existing clock.
            /// </summary>
            /// <example>America/New_York</example>
            [Option("-z|--timeZone", CommandOptionType.SingleValue, Description = "Time zone of the clock (blank for device default)")]
            public string Timezone { get; set; }

            /// <summary>
            /// X coordinate to place the drawing
            /// </summary>
            /// <example>10</example>
            [Required]
            [Option("-x", CommandOptionType.SingleValue, Description = "X coordinate to place the drawing")]
            public int X { get; set; }

            /// <summary>
            /// Y coordinate to place the drawing
            /// </summary>
            /// <example>100</example>
            [Required]
            [Option("-y", CommandOptionType.SingleValue, Description = "Y coordinate to place the drawing")]
            public int Y { get; set; }

            /// <summary>
            /// Drawing area width
            /// </summary>
            /// <example>300</example>
            [Required]
            [Option("-w|--width", CommandOptionType.SingleValue, Description = "Drawing area width")]
            public int Width { get; set; }

            /// <summary>
            /// Drawing area height
            /// </summary>
            /// <example>200</example>
            [Required]
            [Option("-h|--height", CommandOptionType.SingleValue, Description = "Drawing area height")]
            public int Height { get; set; }

            /// <summary>
            /// SVG command(s) used to draw the image or hexColor of square. You can supply one or more SVG drawing commands. Within the SVG, sizes must be actual
            /// and not percentage based and the X, Y coordinates are relative to the drawing area.  You can also embed a dotnet standard or custom DateTime format
            /// string in your SVG as a format string (example provided). Alternatively, a hexadecimal color string can be used for a solid fill of the rectangle,
            /// with or without a preceding '#' character formatted like: AARRGGB, RRGGBB, ARGB or RGB.
            /// </summary>
            /// <example><circle cx="150" cy="100" r="80" fill="green" />
            /// <text x="150" y="120" font-size="60" text-anchor="middle" fill="white">{0:ddd MM/dd/yy h:mm tt}</text></example>
            [Option("-c|--svgsommands", CommandOptionType.SingleValue, Description = "SVG command(s) used to draw the image or hexColor of square (optional)")]
            public string SvgCommands { get; set; }

            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Post, options.Value, "ClockDraw",
                    JsonSerializer.SerializeToUtf8Bytes<ClockDraw>(this));
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Add a time area to a clock
        /// </summary>
        [Command("clocktime", Description = "Add a time area to a clock")]
        public class ClockTime
        {
            /// <summary>
            /// Time zone of the clock (blank for device default). This must match an existing clock.
            /// </summary>
            /// <example>America/New_York</example>
            [Option("-z|--timezone", CommandOptionType.SingleValue, Description = "Time zone of the clock (blank for device default)")]
            public string Timezone { get; set; }

            /// <summary>
            /// X coordinate to place the time.
            /// </summary>
            /// <example>10</example>
            [Required]
            [Option("-x", CommandOptionType.SingleValue, Description = "X coordinate to place the time.")]
            public int X { get; set; }

            /// <summary>
            /// Y coordinate to place the bottom of the time.
            /// </summary>
            /// <example>100</example>
            [Required]
            [Option("-y", CommandOptionType.SingleValue, Description = "Y coordinate to place the time.")]
            public int Y { get; set; }

            /// <summary>
            /// Date/Time format string (optional). This is a dotnet standard or custom DateTime format string.
            /// </summary>
            /// <example>ddd MM/dd/yy h:mm tt</example>
            [DefaultValue("t")]
            [Option("-t|--formatstring", CommandOptionType.SingleValue, Description = "Date/Time format string (optional)")]
            public string Formatstring { get; set; } = "t";

            /// <summary>
            /// Time text horizontal alignment (-1 = Left, 0 = Center, 1 = Right, optional)
            /// </summary>
            /// <example>0</example>
            [DefaultValue(0)]
            [Option("-h|--horizalign", CommandOptionType.SingleValue, Description = "Time text horizontal alignment")]
            public int HorizAlign { get; set; } = 0;

            /// <summary>
            /// Time text vertical alignment (-1 = Top, 0 = Middle, 1 = Bottom, optional)
            /// </summary>
            /// <example>0</example>
            [DefaultValue(0)]
            [Option("-v|--vertalign", CommandOptionType.SingleValue, Description = "Time text vertical alignment")]
            public int VertAlign { get; set; } = 0;

            /// <summary>
            /// Filename or font family of the font to use (optional)
            /// </summary>
            /// <example>/home/pi/NotoSans-Black.ttf</example>
            [Option("-f|--font", CommandOptionType.SingleValue, Description = "Filename or font family of the font to use (optional)")]
            public string Font { get; set; }

            /// <summary>
            /// Font size of the time (optional)
            /// </summary>
            /// <example>60</example>
            [DefaultValue(32)]
            [Option("-s|--fontsize", CommandOptionType.SingleValue, Description = "Font size of the time (optional)")]
            public float FontSize { get; set; } = 32;

            /// <summary>
            /// Font weight of the time (100 - 900, optional)
            /// </summary>
            /// <example>400</example>
            [DefaultValue(400)]
            [Option("-fe|--fontweight", CommandOptionType.SingleValue, Description = "Font weight of the time (100 - 900, optional)")]
            public int FontWeight { get; set; } = 400;

            /// <summary>
            /// Font width of the time (1 - 9, optional)
            /// </summary>
            /// <example>5</example>
            [DefaultValue(5)]
            [Option("-fi|--fontwidth", CommandOptionType.SingleValue, Description = "Font width of the time (1 - 9, optional)")]
            public int FontWidth { get; set; } = 5;

            /// <summary>
            /// Hex color string representing the color of the time (optional)
            /// </summary>
            /// <example>#000000</example>
            [DefaultValue("#000000")]
            [Option("-tc|--textcolor", CommandOptionType.SingleValue, Description = "Hex color string representing the color of the time (optional)")]
            public string TextColor { get; set; } = "#000000";

            /// <summary>
            /// HexColor to use for the clock's background (optional). This is used to erase the previous time.
            /// </summary>
            /// <example>#ffffff</example>
            [Option("-bc|--backgroundcolor", CommandOptionType.SingleValue, Description = "HexColor to use for the clock's background (optional)")]
            public string BackgroundColor { get; set; }

            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Post, options.Value, "ClockTime",
                    JsonSerializer.SerializeToUtf8Bytes<ClockTime>(this));
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Delete a clock
        /// </summary>
        [Command("clockdelete", Description = "Delete a clock")]
        public class ClockDelete
        {
            /// <summary>
            /// Time zone of the clock (blank for device default)
            /// </summary>
            /// <example>America/New_York</example>
            [Option("-z|--timezone", CommandOptionType.SingleValue, Description = "Time zone of the clock (blank for device default)")]
            public string Timezone { get; set; }

            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Post, options.Value, "ClockDelete",
                    JsonSerializer.SerializeToUtf8Bytes<ClockDelete>(this));
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

#pragma warning restore IDE0051 // Remove unused private members
        #endregion Subclasses
    }
}
