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
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Extensions.Options;

    #endregion Using

    public class RenderActions
    {
        #region Subclasses
#pragma warning disable IDE0051 // Remove unused private members
        /// <summary>
        /// Saves the current screen to a png file
        /// </summary>
        [Command("get", Description = "Saves the current screen to a png file")]
        public class Get
        {
            /// <summary>
            /// Filename to save the current screen to
            /// </summary>
            /// <example>/home/pi/current.png</example>
            [Required]
            [Option("-f|--filename", CommandOptionType.SingleValue, Description = "Filename to save the current screen to")]
            public string Filename { get; set; }

            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Get, options.Value, string.Empty, Array.Empty<byte>());
                if (response.ExitCode == 0)
                {
                    File.WriteAllBytes(Filename, response.Result);
                }
                else
                {
                    console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                }

                return response.ExitCode;
            }
        }

        /// <summary>
        /// Saves the current screen to a png file
        /// </summary>
        [Command("screenat", Description = "Saves an area of the screen to a png file")]
        public class GetAt : ScreenAt
        {
            /// <summary>
            /// Filename to save the current screen area to
            /// </summary>
            /// <example>/home/pi/current.png</example>
            [Required]
            [Option("-f|--filename", CommandOptionType.SingleValue, Description = "Filename to save the current screen area to")]
            public string Filename { get; set; }

            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Post, options.Value, "ScreenAt", JsonSerializer.SerializeToUtf8Bytes<ScreenAt>(this));
                if (response.ExitCode == 0)
                {
                    File.WriteAllBytes(Filename, response.Result);
                }
                else
                {
                    console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                }

                return response.ExitCode;
            }
        }

        /// <summary>
        /// Gets the last date and time the screen was updated
        /// </summary>
        [Command("lastupdated", Description = "Gets the last date and time the screen was updated")]
        public class LastUpdated
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "CommandLineUtils needs this to be non static")]
            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Get, options.Value, "LastUpdated", Array.Empty<byte>());
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Refreshes the screen
        /// </summary>
        [Command("refresh", Description = "Refreshes the screen")]
        public class Refresh
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "CommandLineUtils needs this to be non static")]
            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Get, options.Value, "Refresh", Array.Empty<byte>());
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Clears the screen of everything but clocks
        /// </summary>
        [Command("clear", Description = "Clears the screen of everything but clocks")]
        public class Clear
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "CommandLineUtils needs this to be non static")]
            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Get, options.Value, "Clear", Array.Empty<byte>());
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Gets an area of the screen
        /// </summary>
        public class ScreenAt
        {
            /// <summary>
            /// X coordinate to get
            /// </summary>
            /// <example>10</example>
            [Required]
            [Option("-x", CommandOptionType.SingleValue, Description = "X coordinate to get")]
            public int X { get; set; }

            /// <summary>
            /// Y coordinate to get
            /// </summary>
            /// <example>100</example>
            [Required]
            [Option("-y", CommandOptionType.SingleValue, Description = "Y coordinate to get")]
            public int Y { get; set; }

            /// <summary>
            /// Width of area to get
            /// </summary>
            /// <example>300</example>
            [Required]
            [Option("-w|--width", CommandOptionType.SingleValue, Description = "Width of area to get")]
            public int Width { get; set; }

            /// <summary>
            /// Height of area to get
            /// </summary>
            /// <example>200</example>
            [Required]
            [Option("-h|--height", CommandOptionType.SingleValue, Description = "Height of area to get")]
            public int Height { get; set; }

        }

        /// <summary>
        /// Loads an image from a file on the screen
        /// </summary>
        [Command("image", Description = "Loads an image from a file on the screen")]
        public class Image
        {
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
            /// <example>/home/pi/welcome.png</example>
            [Required]
            [Option("-f|--filename", CommandOptionType.SingleValue, Description = "Filename of the image to place on the screen")]
            public string Filename { get; set; }

            /// <summary>
            /// Delay screen update (optional)
            /// </summary>
            /// <example>false</example>
            [DefaultValue(false)]
            [Option("-d|--delay", CommandOptionType.SingleValue, Description = "Delay screen update (optional)")]
            public bool Delay { get; set; } = false;

            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Post, options.Value, "Image", JsonSerializer.SerializeToUtf8Bytes<Image>(this));
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Loads a graphical image on the screen
        /// </summary>
        public class Graphic
        {
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
            /// Data stream to decode
            /// </summary>
            [Required]
            public Stream Data { get; set; }

            /// <summary>
            /// Delay screen update (optional)
            /// </summary>
            /// <example>false</example>
            [DefaultValue(false)]
            [Option("-d|--delay", CommandOptionType.SingleValue, Description = "Delay screen update (optional)")]
            public bool Delay { get; set; } = false;
        }

        /// <summary>
        /// Draws an SVG object(s) on the screen
        /// </summary>
        [Command("draw", Description = "Draws an SVG object(s) on the screen")]
        public class Draw
        {
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
            /// and not percentage based and the X, Y coordinates are relative to the drawing area. Alternatively, a hexadecimal color string can be used
            /// for a solid fill of the rectangle, with or without a preceding '#' character formatted like: AARRGGB, RRGGBB, ARGB or RGB.
            /// </summary>
            /// <example><circle cx="150" cy="100" r="80" fill="green" /> <text x="150" y="120" font-size="60" text-anchor="middle" fill="white">SVG</text></example>
            [Option("-c|--svgcommands", CommandOptionType.SingleValue, Description = "SVG command(s) used to draw the image or hexColor of square (optional)")]
            public string SvgCommands { get; set; }

            /// <summary>
            /// Delay screen update (optional)
            /// </summary>
            /// <example>false</example>
            [DefaultValue(false)]
            [Option("-d|--delay", CommandOptionType.SingleValue, Description = "Delay screen update (optional)")]
            public bool Delay { get; set; } = false;

            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Post, options.Value, "Draw", JsonSerializer.SerializeToUtf8Bytes<Draw>(this));
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Places text on the screen
        /// </summary>
        [Command("text", Description = "Places text on the screen")]
        public class Text
        {
            /// <summary>
            /// X coordinate to place the text.
            /// </summary>
            /// <example>10</example>
            [Required]
            [Option("-x", CommandOptionType.SingleValue, Description = "X coordinate to place the text.")]
            public int X { get; set; }

            /// <summary>
            /// Y coordinate to place the text.
            /// </summary>
            /// <example>100</example>
            [Required]
            [Option("-y", CommandOptionType.SingleValue, Description = "Y coordinate to place the text.")]
            public int Y { get; set; }

            /// <summary>
            /// Text value to place
            /// </summary>
            /// <example>Welcome Home</example>
            [Required]
            [Option("-t|--value", CommandOptionType.SingleValue, Description = "Text value to place")]
            public string Value { get; set; }

            /// <summary>
            /// Text horizontal alignment (-1 = Left, 0 = Center, 1 = Right, optional)
            /// </summary>
            /// <example>0</example>
            [DefaultValue(0)]
            [Option("-h|--horizalign", CommandOptionType.SingleValue, Description = "Text horizontal alignment")]
            public int HorizAlign { get; set; } = 0;

            /// <summary>
            /// Text vertical alignment (-1 = Top, 0 = Middle, 1 = Bottom, optional)
            /// </summary>
            /// <example>0</example>
            [DefaultValue(0)]
            [Option("-v|--vertalign", CommandOptionType.SingleValue, Description = "Text vertical alignment")]
            public int VertAlign { get; set; } = 0;

            /// <summary>
            /// Filename or font family of the font to use (optional)
            /// </summary>
            /// <example>/home/pi/NotoSans-Black.ttf</example>
            [Option("-f|--font", CommandOptionType.SingleValue, Description = "Filename or font family of the font to use (optional)")]
            public string Font { get; set; }

            /// <summary>
            /// Font size of the text (optional)
            /// </summary>
            /// <example>60</example>
            [DefaultValue(32)]
            [Option("-s|--fontsize", CommandOptionType.SingleValue, Description = "Font size of the text (optional)")]
            public float FontSize { get; set; } = 32;

            /// <summary>
            /// Font weight of the text (100 - 900, optional)
            /// </summary>
            /// <example>400</example>
            [DefaultValue(400)]
            [Option("-fe|--fontweight", CommandOptionType.SingleValue, Description = "Font weight of the text (100 - 900, optional)")]
            public int FontWeight { get; set; } = 400;

            /// <summary>
            /// Font width of the text (1 - 9, optional)
            /// </summary>
            /// <example>5</example>
            [DefaultValue(5)]
            [Option("-fi|--fontwidth", CommandOptionType.SingleValue, Description = "Font width of the text (1 - 9, optional)")]
            public int FontWidth { get; set; } = 5;

            /// <summary>
            /// Hex color string representing the color of the text (optional)
            /// </summary>
            /// <example>#000000</example>
            [DefaultValue("#000000")]
            [Option("-c|--hexcolor", CommandOptionType.SingleValue, Description = "Hex color string representing the color of the text (optional)")]
            public string HexColor { get; set; } = "#000000";

            /// <summary>
            /// Delay screen update (optional)
            /// </summary>
            /// <example>false</example>
            [DefaultValue(false)]
            [Option("-d|--delay", CommandOptionType.SingleValue, Description = "Delay screen update (optional)")]
            public bool Delay { get; set; } = false;

            private async Task<int> OnExecuteAsync(IConsole console, IOptions<AppSettings.Console> options)
            {
                RenderTools.Response response = await RenderTools.RespondWith(HttpMethod.Post, options.Value, "Text", JsonSerializer.SerializeToUtf8Bytes<Text>(this));
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Render a raw command to the screen
        /// </summary>
        [Command("rendercommand", Description = "Render a raw command to the screen")]
        public class RenderCommand
        {
            /// <summary>
            /// Command to render
            /// </summary>
            /// <example>image</example>
            [Required]
            [Option("-c", CommandOptionType.SingleValue, Description = "Command to render")]
            public string CommandName { get; set; }

            /// <summary>
            /// Serialized action values
            /// </summary>
            /// <example>{ "x": 10, "y": 100, "filename": "/home/pi/welcome.png", "delay: true"}</example>
            [Option("-a", CommandOptionType.SingleValue, Description = "Serialized action values")]
            public string CommandValues { get; set; }
        }

#pragma warning restore IDE0051 // Remove unused private members
        #endregion Subclasses
    }
}
