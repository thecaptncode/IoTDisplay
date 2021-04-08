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

namespace IoTDisplay.WebApp.Controllers
{
    #region Using

    using System;
    using System.Net;
    using IoTDisplay.Common;
    using IoTDisplay.Common.Services;
    using Microsoft.AspNetCore.Mvc;

    #endregion Using

    /// <summary>
    /// API to control an Internet of Things E-Paper screen
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class IoTDisplayController : ControllerBase
    {
        #region Fields

        private readonly IIoTDisplayService _ioTDisplayService;

        #endregion Fields

        #region Constructor

        public IoTDisplayController(IIoTDisplayService ioTDisplayService)
        {
            _ioTDisplayService = ioTDisplayService;
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
        /// <response code="500">An unknown error occured processing the request</response>
        [HttpGet]
        [Produces("image/png")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Screen()
        {
            return File(_ioTDisplayService.Renderer.Screen, "image/png");
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
            return _ioTDisplayService.LastUpdated;
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
        /// <response code="500">An unknown error occured processing the request</response>
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
                _ioTDisplayService.Renderer.Refresh();
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
        /// <response code="500">An unknown error occured processing the request</response>
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
                _ioTDisplayService.Renderer.Clear();
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
        /// <response code="500">An unknown error occured processing the request</response>
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
                _ioTDisplayService.Renderer.ClockClear();
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
        /// <response code="500">An unknown error occured processing the request</response>
        [HttpPost]
        [Route("Image")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Image(IoTDisplayActionService.Image image)
        {
            string result = null;
            try
            {
                _ioTDisplayService.Renderer.Image(image);
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
        /// <response code="500">An unknown error occured processing the request</response>
        [HttpPost]
        [Route("Draw")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Draw(IoTDisplayActionService.Draw draw)
        {
            string result = null;
            try
            {
                _ioTDisplayService.Renderer.Draw(draw);
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
        /// <response code="500">An unknown error occured processing the request</response>
        [HttpPost]
        [Route("Text")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Text(IoTDisplayActionService.Text text)
        {
            string result = null;
            try
            {
                _ioTDisplayService.Renderer.Text(text);
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
        /// <response code="500">An unknown error occured processing the request</response>
        [HttpPost]
        [Route("Clock")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult Clock(IoTDisplayActionService.Clock clock)
        {
            string result = null;
            try
            {
                _ioTDisplayService.Renderer.Clock(clock);
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
        /// <response code="500">An unknown error occured processing the request</response>
        [HttpPost]
        [Route("ClockImage")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult ClockImage(IoTDisplayActionService.ClockImage clockImage)
        {
            string result = null;
            try
            {
                _ioTDisplayService.Renderer.ClockImage(clockImage);
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
        /// <response code="500">An unknown error occured processing the request</response>
        [HttpPost]
        [Route("ClockDraw")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult ClockDraw(IoTDisplayActionService.ClockDraw clockDraw)
        {
            string result = null;
            try
            {
                _ioTDisplayService.Renderer.ClockDraw(clockDraw);
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
        /// <response code="500">An unknown error occured processing the request</response>
        [HttpPost]
        [Route("ClockTime")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult ClockTime(IoTDisplayActionService.ClockTime clockTime)
        {
            string result = null;
            try
            {
                _ioTDisplayService.Renderer.ClockTime(clockTime);
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
        /// <response code="500">An unknown error occured processing the request</response>
        [HttpPost]
        [Route("ClockDelete")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public ActionResult ClockDelete(IoTDisplayActionService.ClockDelete clockDelete)
        {
            string result = null;
            try
            {
                _ioTDisplayService.Renderer.ClockDelete(clockDelete);
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
