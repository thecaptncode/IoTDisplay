### Description
This is a .NET 5 Project for network enabling an E-Paper screen attached to a Raspberry Pi. It allows
any device on the network to draw or write on the screen using the http post of json documents.  It is
OpenAPI compliant and has a web UI listing all commands with examples and json schema details. There is 
also a console app allowing any command to be sent to the screen via the command line.

The web api accepts drawing commands, image file names and text info and via OpenAPI for placement on 
the screen.  There are also commands to place one or more built in digital clocks on the screen. The
clocks are kept up to date without additional commands.

This project was started to give the Hubitat Elevation home automation hub a display of device status
and other information but it can be used by any device on the network. Lots of us have small displays on our
nightstands or end tables showing the time and basic weather information. I wanted a small device like that
but more versatile and customizable to my needs.

Included is a Hubitat driver that can communicate with the web api and an example Hubitat application that
gets OpenWeatherMap data, thermostat settings, indoor temperature and humidity sensors.  It passes that
information to the Hubitat driver to be sent to the screen.

Here is picture of that in action.

![Image of display](https://raw.githubusercontent.com/thecaptncode/IoTDisplay/master/Hardware1.jpg)

The full documentation can be found in the [project Wiki.](https://github.com/thecaptncode/IoTDisplay/wiki)

### Installation

The installation information can be found [here.](https://github.com/thecaptncode/IoTDisplay/wiki/Installation).
