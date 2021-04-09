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

namespace IoTDisplay.Common
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

    #endregion Using

    public class IoTDisplayActionService
    {
        private const string BaseURL = "http://localhost:5000/api/IoTDisplay/";

        #region Methods (Private)

        /// <summary>
        /// Sent HTTP post
        /// </summary>
        /// <param name="method">HTTP Method to use</param>
        /// <param name="uri">URI to send to</param>
        /// <param name="document">Document to send</param>
        /// <returns>Byte array of response</returns>
        private static async Task<Response> RespondWith(HttpMethod method, string uri, byte[] document)
        {
            byte[] responseBytes = null;
            int exitCode = 0;
            byte[] reasonBytes = null;
            try
            {
                using HttpClient httpClient = new ();
                using HttpRequestMessage request = new (method, BaseURL + uri);
                request.Content = new ByteArrayContent(document);
                request.Content.Headers.ContentType = new ("application/json");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                responseBytes = await response.Content.ReadAsByteArrayAsync();
                if (response.ReasonPhrase.Length > 0)
                {
                    reasonBytes = Encoding.UTF8.GetBytes(response.ReasonPhrase + Environment.NewLine);
                }

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                byte[] errorBytes = Encoding.UTF8.GetBytes(ex.ToString() + Environment.NewLine);
                int len = errorBytes.Length;
                if (responseBytes != null)
                {
                    len += responseBytes.Length;
                }

                if (reasonBytes != null)
                {
                    len += reasonBytes.Length;
                }

                byte[] bytes = new byte[len];
                len = 0;
                if (responseBytes != null)
                {
                    Buffer.BlockCopy(reasonBytes, 0, bytes, len, reasonBytes.Length);
                    len += reasonBytes.Length;
                }

                if (responseBytes != null)
                {
                    Buffer.BlockCopy(responseBytes, 0, bytes, len, responseBytes.Length);
                    len += responseBytes.Length;
                }

                Buffer.BlockCopy(errorBytes, 0, bytes, len, errorBytes.Length);
                responseBytes = bytes;
                exitCode = 4;
            }
            catch (Exception ex)
            {
                responseBytes = Encoding.UTF8.GetBytes(ex.ToString() + Environment.NewLine);
                exitCode = 5;
            }

            if (responseBytes.Length == 0 && exitCode == 0 && reasonBytes != null)
            {
                responseBytes = reasonBytes;
            }

            return new Response { Result = responseBytes, ExitCode = exitCode };
        }
        #endregion Methods (Private)

        #region Subclasses
#pragma warning disable IDE0051 // Remove unused private members

        /// <summary>
        /// Response from HTTP attempt
        /// </summary>
        public class Response
        {
            public byte[] Result { get; set; }

            public int ExitCode { get; set; }
        }

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

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Get, string.Empty, Array.Empty<byte>());
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

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Post, "ScreenAt", JsonSerializer.SerializeToUtf8Bytes<ScreenAt>(this));
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
            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Get, "LastUpdated", Array.Empty<byte>());
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
            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Get, "Refresh", Array.Empty<byte>());
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
            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Get, "Clear", Array.Empty<byte>());
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

        /// <summary>
        /// Clears the screen of all clocks
        /// </summary>
        [Command("clockclear", Description = "Clears the screen of all clocks")]
        public class ClockClear
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "CommandLineUtils needs this to be non static")]
            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Get, "ClockClear", Array.Empty<byte>());
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

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Post, "Image", JsonSerializer.SerializeToUtf8Bytes<Image>(this));
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
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

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Post, "Draw", JsonSerializer.SerializeToUtf8Bytes<Draw>(this));
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

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Post, "Text", JsonSerializer.SerializeToUtf8Bytes<Text>(this));
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

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Post, "Clock", JsonSerializer.SerializeToUtf8Bytes<Clock>(this));
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

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Post, "ClockImage", JsonSerializer.SerializeToUtf8Bytes<ClockImage>(this));
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

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Post, "ClockDraw", JsonSerializer.SerializeToUtf8Bytes<ClockDraw>(this));
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

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Post, "ClockTime", JsonSerializer.SerializeToUtf8Bytes<ClockTime>(this));
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

            private async Task<int> OnExecuteAsync(IConsole console)
            {
                Response response = await RespondWith(HttpMethod.Post, "ClockDelete", JsonSerializer.SerializeToUtf8Bytes<ClockDelete>(this));
                console.Write(Encoding.UTF8.GetChars(response.Result, 0, response.Result.Length));
                return response.ExitCode;
            }
        }

#pragma warning restore IDE0051 // Remove unused private members
        #endregion Subclasses
    }
}
