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
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml;
    using SkiaSharp;
    using Svg.Skia;

    #endregion Using

    /// <summary>
    /// Tools used for rendering
    /// </summary>
    public static class RenderTools
    {
        #region Methods (Public)

        /// <summary>
        /// Sent HTTP post
        /// </summary>
        /// <param name="method">HTTP Method to use</param>
        /// <param name="settings">Application Settings</param>
        /// <param name="uri">URI to send to</param>
        /// <param name="document">Document to send</param>
        /// <returns>Byte array of response</returns>
        public static async Task<Response> RespondWith(HttpMethod method, AppSettings.Console settings, string uri, byte[] document)
        {
            byte[] responseBytes = null;
            int exitCode = 0;
            byte[] reasonBytes = null;
            if (string.IsNullOrWhiteSpace(settings?.BaseUrl))
            {
                return new Response { Result = Encoding.UTF8.GetBytes("BaseUrl not found in configuration file" + Environment.NewLine), ExitCode = 3 };
            }

            try
            {
                using HttpClient httpClient = new ();
                using HttpRequestMessage request = new (method, settings.BaseUrl + uri);
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
                byte[] errorBytes = Encoding.UTF8.GetBytes(ex.Message + Environment.NewLine);
                int len = errorBytes.Length;
                if (reasonBytes != null)
                {
                    len += reasonBytes.Length;
                }

                if (responseBytes != null)
                {
                    len += responseBytes.Length;
                }

                byte[] bytes = new byte[len];
                len = 0;
                if (reasonBytes != null)
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
                responseBytes = Encoding.UTF8.GetBytes(ex.Message + Environment.NewLine);
                exitCode = 5;
            }

            if (responseBytes.Length == 0 && exitCode == 0 && reasonBytes != null)
            {
                responseBytes = reasonBytes;
            }

            return new Response { Result = responseBytes, ExitCode = exitCode };
        }

        /// <summary>
        /// Clean up a file name of unsupported characters
        /// </summary>
        /// <param name="name">File name to clean</param>
        /// <returns>Returns a cleaned up file name</returns>
        public static string CleanFileName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\/\\\.+$)|([{0}]+)", invalidChars);

            return Regex.Replace(name, invalidRegStr, "_");
        }

        /// <summary>
        /// Get a TypeFace for a font
        /// </summary>
        /// <param name="font">Font for the TypeFace</param>
        /// <param name="weight">Weight of the font</param>
        /// <param name="width">Width of the font</param>
        /// <returns>Returns a TypeFace</returns>
        public static SKTypeface GetTypeface(string font, int weight, int width)
        {
            SKTypeface typeface = null;
            if (!string.IsNullOrWhiteSpace(font))
            {
                try
                {
                    if (File.Exists(font))
                    {
                        typeface = SKTypeface.FromFile(font);
                    }
                    else if (weight > 0 && width > 0)
                    {
                        typeface = SKTypeface.FromFamilyName(font, weight, width, SKFontStyleSlant.Upright);
                    }
                    else if (weight > 0)
                    {
                        typeface = SKTypeface.FromFamilyName(font, weight, 5, SKFontStyleSlant.Upright);
                    }
                    else if (width > 0)
                    {
                        typeface = SKTypeface.FromFamilyName(font, 400, width, SKFontStyleSlant.Upright);
                    }
                    else
                    {
                        typeface = SKTypeface.FromFamilyName(font);
                    }
                }
                catch
                {
                    typeface = null;
                }
            }

            return typeface;
        }

        /// <summary>
        /// Get an image
        /// </summary>
        /// <param name="settings">Render settings to use</param>
        /// <param name="x">X coordinate to use</param>
        /// <param name="y">Y coordinate to use</param>
        /// <param name="filename">File name to get</param>
        /// <returns>Returns a bitmap of the image</returns>
        public static SKBitmap GetImage(RenderSettings settings, int x, int y, string filename)
        {
            SKBitmap img = null;
            if (x < 0 || x >= settings.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "X coordinate is not within the screen");
            }

            if (y < 0 || y >= settings.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "Y coordinate is not within the screen");
            }

            if (File.Exists(filename))
            {
                try
                {
                    img = SKBitmap.Decode(filename) ?? throw new ArgumentException("Unable to decode image", nameof(filename));
                }
                catch (Exception ex)
                {
                    if (img != null)
                    {
                        img.Dispose();
                    }

                    throw new ArgumentException("An exception occurred trying to load image: " + ex.Message, nameof(filename), ex);
                }
            }
            else
            {
                throw new ArgumentException("File not found", nameof(filename));
            }

            return img;
        }

        public static SKImage GetPicture(RenderSettings settings, int x, int y, int width, int height, string svgCommands)
        {
            if (x < 0 || x >= settings.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "X coordinate is not within the screen");
            }

            if (y < 0 || y >= settings.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "Y coordinate is not within the screen");
            }

            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero");
            }

            if (string.IsNullOrWhiteSpace(svgCommands))
            {
                svgCommands = settings.Foreground.ToString();
            }

            if (SKColor.TryParse(svgCommands, out _))
            {
                svgCommands = "<rect width=\"" + width.ToString() + "\" height=\"" + height.ToString() + "\" fill=\"" + svgCommands + "\" />";
            }

            byte[] fullSVG = Encoding.UTF8.GetBytes("<svg version=\"1.2\" baseProfile=\"full\" width=\"" + width.ToString() + "\" height=\"" +
                height.ToString() + "\" " + "xmlns=\"http://www.w3.org/2000/svg\">" + svgCommands + "</svg>");
            SKImage img;
            try
            {
                SKSvg svg = new ();
                using MemoryStream stream = new (fullSVG);
                using SKPicture pict = svg.Load(stream);
                SKSizeI dimen = new (
                    (int)Math.Ceiling(pict.CullRect.Width),
                    (int)Math.Ceiling(pict.CullRect.Height));
                img = SKImage.FromPicture(pict, dimen, SKMatrix.CreateScale(1, 1)) ?? throw new ArgumentException("Invalid SVG command", nameof(svgCommands));
                stream.Close();
            }
            catch (XmlException ex)
            {
                throw new ArgumentException("The SVG commands could not be parsed", nameof(svgCommands), ex);
            }

            return img;
        }

        /// <summary>
        /// Get a paint object for text
        /// </summary>
        /// <param name="settings">Settings to use</param>
        /// <param name="x">X coordinate to use</param>
        /// <param name="y">Y coordinate to use</param>
        /// <param name="text">Text to paint</param>
        /// <param name="horizAlign">Horizontal text alignment</param>
        /// <param name="vertAlign">Vertical text alignment</param>
        /// <param name="font">Font to use</param>
        /// <param name="fontSize">Font size</param>
        /// <param name="fontWeight">Font weight</param>
        /// <param name="fontWidth">Font width</param>
        /// <param name="hexColor">Font hex color</param>
        /// <param name="bold">Bold setting</param>
        /// <returns>Returns a paint object for the text</returns>
        public static (SKPaint paint, int width, int height, int hoffset, int voffset, int left, int top) GetPaint(RenderSettings settings, int x, int y,
            string text, int horizAlign, int vertAlign, string font, float fontSize, int fontWeight, int fontWidth, string hexColor, bool bold)
        {
            if (x < 0 || x >= settings.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "X coordinate is not within the screen");
            }

            if (y < 0 || y >= settings.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "Y coordinate is not within the screen");
            }

            if (horizAlign < -1 || horizAlign > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(horizAlign), horizAlign, "Horizontal alignment must be -1, 0 or 1");
            }

            if (vertAlign < -1 || vertAlign > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(vertAlign), vertAlign, "Vertical alignment must be -1, 0 or 1");
            }

            if (fontSize <= 0 || fontSize > 9999)
            {
                throw new ArgumentOutOfRangeException(nameof(fontSize), fontSize, "Font size must be greater than zero and less than 10000");
            }

            if ((fontWeight < 100 && fontWeight != 0) || fontWeight > 900)
            {
                throw new ArgumentOutOfRangeException(nameof(fontWeight), fontWeight, "Font weight must be between 100 and 900");
            }

            if (fontWidth < 0 || fontWidth > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(fontWidth), fontWidth, "Font width must be between 1 and to 9");
            }

            SKPaint paint = new ()
            {
                TextSize = fontSize,
                IsAntialias = true,
            };
            if (!string.IsNullOrWhiteSpace(font))
            {
                paint.Typeface = GetTypeface(font, fontWeight, fontWidth) ?? throw new ArgumentException("Font not found", nameof(font));
            }

            if (string.IsNullOrWhiteSpace(hexColor))
            {
                paint.Color = new (0, 0, 0);
            }
            else
            {
                try
                {
                    paint.Color = SKColor.Parse(hexColor);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException("Invalid hexColor", nameof(hexColor), ex);
                }
            }

            paint.FakeBoldText = bold;
            paint.IsStroke = false;
            SKRect bound = new ();
            float width = paint.MeasureText(text, ref bound);
            float height = bound.Height;
            float hoffset;
            float voffset;
            float left;
            if (horizAlign == -1) // Left
            {
                hoffset = 0;
                left = x + bound.Left;
            }
            else if (horizAlign == 1) // Right
            {
                hoffset = 1 - width - bound.Left;
                left = x - width + 1;
            }
            else
            {
                hoffset = 0 - bound.MidX;
                left = x - bound.MidX + bound.Left;
            }

            float top;
            if (vertAlign == -1) // Top
            {
                voffset = 0 - bound.Top;
                top = y;
            }
            else if (vertAlign == 1) // Bottom
            {
                voffset = 0;
                top = y + bound.Top;
            }
            else
            {
                voffset = 1 - bound.MidY;
                top = y - bound.MidY + bound.Top + 1;
            }

            return (paint, (int)Math.Round(width), (int)Math.Round(height), (int)Math.Round(hoffset), (int)Math.Round(voffset),
                (int)Math.Round(left), (int)Math.Round(top));
        }

        #endregion Methods (Public)

        #region Subclasses

        /// <summary>
        /// Response from HTTP attempt
        /// </summary>
        public class Response
        {
            public byte[] Result { get; set; }

            public int ExitCode { get; set; }
        }

        #endregion Subclasses
    }
}