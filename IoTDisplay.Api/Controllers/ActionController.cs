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

namespace IoTDisplay.Api.Controllers
{
    #region Using

    using System;
    using System.IO;
    using System.Net;
    using IoTDisplay.Common.Models;
    using IoTDisplay.Common.Services;
    using Microsoft.AspNetCore.Mvc;

    #endregion Using

    /// <summary>
    /// API to control an Internet of Things E-Paper screen
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ActionController : ControllerBase
    {
        #region Fields

        private readonly IRenderService _renderer;

        #endregion Fields

        #region Constructor

        public ActionController(IRenderService renderer)
        {
            _renderer = renderer;
        }

        #endregion Constructor

        #region GET

        /// <summary>
        /// Returns the current screen as a png image
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /
        ///
        /// </remarks>
        /// <returns>Screen as a PNG</returns>
        /// <response code="200">The screen was returned</response>
        /// <response code="400">The screen could not be returned</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpGet]
        [Produces("image/png")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Screen()
        {
            string result = null;
            Stream area = null;
            try
            {
                area = _renderer.Screen;
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return File(area, "image/png");
            }
            else
            {
                return BadRequest(result);
            }

        }

        /// <summary>
        /// Gets the last date and time the screen was updated
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /LastUpdated
        ///
        /// </remarks>
        /// <returns>The last screen update Date/Time</returns>
        [HttpGet]
        [Route("LastUpdated")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public DateTime LastUpdated()
        {
            DateTime lastUpdated = DateTime.MinValue;
            foreach (IDisplayService display in _renderer.Displays)
            {
                if (display.LastUpdated > lastUpdated)
                {
                    lastUpdated = display.LastUpdated;
                }
            }

            return lastUpdated;
        }

        /// <summary>
        /// Refreshes the screen
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /Refresh
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The screen will be refreshed</response>
        /// <response code="400">The screen was not refreshed due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpGet]
        [Route("Refresh")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Refresh()
        {
            string result = null;
            try
            {
                _renderer.Refresh();
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Clears the screen of everything but clocks.  Screen state files are also deleted.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /Clear
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The screen will be cleared</response>
        /// <response code="400">The screen was not cleared due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpGet]
        [Route("Clear")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Clear()
        {
            string result = null;
            try
            {
                _renderer.Clear();
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Clears the screen of all clocks.   Clock state files are also deleted.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /ClockClear
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The clock will be cleared from the screen</response>
        /// <response code="400">The clock was not added due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpGet]
        [Route("ClockClear")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult ClockClear()
        {
            string result = null;
            try
            {
                _renderer.Clocks.ClockClear();
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        #endregion GET

        #region POST

        /// <summary>
        /// Gets an area of the screen
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /ScreenAt
        ///     {
        ///         "x": 10,
        ///         "y": 100,
        ///         "width": 300,
        ///         "height": 200
        ///     }
        ///
        /// </remarks>
        /// <returns>Screen area as a PNG</returns>
        /// <response code="200">The screen area was returned</response>
        /// <response code="400">The screen area could not be returned</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpPost]
        [Route("ScreenAt")]
        [Produces("image/png")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult ScreenAt(RenderActions.ScreenAt screenat)
        {
            string result = null;
            Stream area = null;
            try
            {
                area = _renderer.ScreenAt(screenat);
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return File(area, "image/png");
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Loads an image from a file on the screen
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /Image
        ///     {
        ///         "x": 10,
        ///         "y": 100,
        ///         "filename": "/home/pi/welcome.png",
        ///         "delay: true"
        ///     }
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The image will be added to the screen</response>
        /// <response code="400">The image was not added due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpPost]
        [Route("Image")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Image(RenderActions.Image image)
        {
            string result = null;
            try
            {
                _renderer.Image(image);
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Draws an SVG object(s) on the screen
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /Draw
        ///     {
        ///         "x": 10,
        ///         "y": 100,
        ///         "width": 300,
        ///         "height": 200,
        ///         "svgCommands": '<circle cx="150" cy="100" r="80" fill="green" /><text x="150" y="120" font-size="60" text-anchor="middle" fill="white">SVG</text>',
        ///         "delay: true"
        ///     }
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The drawing will be added to the screen</response>
        /// <response code="400">The drawing was not added due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpPost]
        [Route("Draw")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Draw(RenderActions.Draw draw)
        {
            string result = null;
            try
            {
                _renderer.Draw(draw);
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Places text on the screen
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /Text
        ///     {
        ///         "x": 10,
        ///         "y": 100,
        ///         "value": "Welcome Home",
        ///         "horizAlign": 0,
        ///         "vertAlign": 0,
        ///         "font": "/home/pi/NotoSans-Black.ttf",
        ///         "fontSize": 60,
        ///         "fontWeight": 400,
        ///         "fontWidth": 5,
        ///         "hexColor": "#000000",
        ///         "delay: true"
        ///     }
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The text will be added to the screen</response>
        /// <response code="400">The text was not added due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpPost]
        [Route("Text")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Text(RenderActions.Text text)
        {
            string result = null;
            try
            {
                _renderer.Text(text);
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Add a clock to the screen
        /// </summary>
        /// <remarks>
        /// This sets up a clock to be used on the screen.  Subsequent commands are required to describe the clock.
        ///
        /// Sample request:
        ///
        ///     POST /Clock
        ///     {
        ///         "timezone": "America/New_York"
        ///     }
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The clock will be added to the screen</response>
        /// <response code="400">The clock was not added due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpPost]
        [Route("Clock")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Clock(ClockActions.Clock clock)
        {
            string result = null;
            try
            {
                _renderer.Clocks.Clock(clock);
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Add an image from a file to a clock
        /// </summary>
        /// <remarks>
        /// This is added to the screen every time the clock updates and is intended to be used as a clock background.
        /// ClockImage areas should be added before adding the ClockTime area they relate to so to ensure the image is beneath the time.
        ///
        /// Sample request:
        ///
        ///     POST /ClockImage
        ///     {
        ///         "timezone": "America/New_York",
        ///         "x": 10,
        ///         "y": 100,
        ///         "filename": "/home/pi/nyc.png"
        ///     }
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The image will be added to the clock</response>
        /// <response code="400">The image was not added due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpPost]
        [Route("ClockImage")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult ClockImage(ClockActions.ClockImage clockImage)
        {
            string result = null;
            try
            {
                _renderer.Clocks.ClockImage(clockImage);
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Add a drawing to a clock.
        /// </summary>
        /// <remarks>
        /// This is written to the screen every time the clock updates and is intended to be used as a clock background.
        /// ClockDraw areas should be added before adding the ClockTime area they relate to so to ensure the drawing is beneath the time.
        ///
        /// Sample request:
        ///
        ///     POST /ClockDraw
        ///     {
        ///         "timezone": "America/New_York",
        ///         "x": 10,
        ///         "y": 100,
        ///         "width": 300,
        ///         "height": 200,
        ///         "svgCommands": '<circle cx="150" cy="100" r="80" fill="green" /><text x="150" y="120" font-size="60" text-anchor="middle" fill="white">{0:ddd MM/dd/yy h:mm tt}</text>'
        ///     }
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The drawing will be added to the clock</response>
        /// <response code="400">The drawing was not added due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpPost]
        [Route("ClockDraw")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult ClockDraw(ClockActions.ClockDraw clockDraw)
        {
            string result = null;
            try
            {
                _renderer.Clocks.ClockDraw(clockDraw);
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Add a time area to a clock
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /ClockTime
        ///     {
        ///         "timezone": "America/New_York",
        ///         "x": 10,
        ///         "y": 100,
        ///         "formatstring": "ddd MM/dd/yy h:mm tt",
        ///         "horizAlign": 0,
        ///         "vertAlign": 0,
        ///         "font": "/home/pi/NotoSans-Black.ttf",
        ///         "fontSize": 60,
        ///         "fontWeight": 400,
        ///         "fontWidth": 5,
        ///         "textColor": "#000000",
        ///         "backgroundColor": "#ffffff"
        ///     }
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The time area will be added to the clock</response>
        /// <response code="400">The time area was not added due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpPost]
        [Route("ClockTime")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult ClockTime(ClockActions.ClockTime clockTime)
        {
            string result = null;
            try
            {
                _renderer.Clocks.ClockTime(clockTime);
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Delete a clock
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /ClockDelete
        ///     {
        ///         "timezone": "America/New_York"
        ///     }
        ///
        /// </remarks>
        /// <returns>Success or failure as a response code</returns>
        /// <response code="202">The clock will be deleted</response>
        /// <response code="400">The clock was not deleted due to an error in the request</response>
        /// <response code="500">An unknown error occurred processing the request</response>
        [HttpPost]
        [Route("ClockDelete")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult ClockDelete(ClockActions.ClockDelete clockDelete)
        {
            string result = null;
            try
            {
                _renderer.Clocks.ClockDelete(clockDelete);
            }
            catch (ArgumentException ex)
            {
                result = ex.Message;
            }

            if (result == null)
            {
                return Accepted();
            }
            else
            {
                return BadRequest(result);
            }
        }

        #endregion POST
    }
}
