### Description
This is a .NET 5 Project for network enabling an E-Paper screen attached to a Raspberry Pi. It allows
any device on the network to draw or write on the screen using the http post of json documents.  It is
OpenAPI compliant and has a web UI listing all commands with examples and json schema details. There is 
also a console app allowing any command to be sent to the screen via the command line.

The web app accepts drawing commands, image file names and text info and via OpenAPI for placement on 
the screen.  There are also commands to place one or more built in digital clocks on the screen. The
clocks are kept up to date without additional commands.

This project was started to give the Hubitat Elevation home automation hub a display of device status
and other information but it can be used by any device on the network. Lots of us have small displays on our
nightstands or end tables showing the time and basic weather information. I wanted a small device like that
but more versatile and customizable to my needs.

Included is a Hubitat driver that can communicate with the web app and an example Hubitat application that
gets OpenWeatherMap data, thermostat settings, indoor temperature and humidity sensors.  It passes that
information to the Hubitat driver to be sent to the screen.

Here is picture of that in action.

![Image of display](https://raw.githubusercontent.com/thecaptncode/IoTDisplay/master/Hardware1.jpg)

Currently, this is a work in progress.  Additional details are soon to come.

### Installation

Set up a Raspberry Pi 2 or later.  This has been tested on a Raspberry Pi 3B+ running 
Raspberry Pi OS Lite (32-bit).  

Verify it is on the network.

Log in and configure the system:

```
sudo raspi-config
Choose Interfacing Options -> SPI -> Yes  to enable SPI interface
Choose System Options -> Password  to change the default password for user pi
Choose System Options -> Hostname  to change the system hostname
Choose Localisation Options -> Locale  to set up your locale
Choose Localisation Options -> Timezone  to set up your timezone
```

Shut down the system:

```
sudo shutdown -h now
```

Remove power from the system and install a Waveshare E-Paper display with HAT.  The currently supported 
versions can be found here: 
https://github.com/eXoCooLd/Waveshare.EPaperDisplay

Power the system up and log back in.

Install libgdiplus and fontconfig packages:
```
sudo apt update
sudo apt-get libgdiplus
sudo apt-get fontconfig
```

The tzdata package is also required but automatically installed with Raspberry PI OS Lite. 

Update/Upgrade to the lastest versions of all packages:
```
sudo apt upgrade
```
Copy any desired TrueType or OpenType fonts to /usr/local/share/fonts.  

Update the font cache after installing any font:
```
sudo fc-cache
```
You can verify font installation using `fc-list`.  If the iotdisplay service is running, 
it will need to be stopped and restarted (or the system rebooted) in order to use any new font.

Deploy the project to the Raspberry Pi into its own folder such as /usr/local/iotdisplay.  If the 
Console App is desired, it can be depolyed to the same folder as the Web App.  
See https://docs.microsoft.com/en-us/dotnet/iot/deployment

Create a folder for the service to store its state files such as /home/iotdisplay.  /tmp will not 
work as the folder is flushed after reboot.

Update the appsettings.json file with the desired settings including state folder and display type 
driver name.  The display types can be found here 
https://github.com/eXoCooLd/Waveshare.EPaperDisplay/blob/master/Waveshare/Devices/EPaperDisplayType.cs

Test the web app by running:
```
sudo /usr/local/iotdisplay/IoTDisplay.WebApp
```
Your E-Paper screen should flash black and white while it is starting.

See if it is running by opening a browser to http://[host name]:5000/swagger/index.html

Back on the Raspberry Pi, stop the web app with Ctrl-C

Set up WebApp to run as a service:

Create the following as a file in /etc/systemd/system/multi-user.target.wants  named  iotdisplay.service
```
[Unit]
Description=IoT Display service

[Service]
ExecStart=/usr/local/iotdisplay/IoTDisplay.WebApp
WorkingDirectory=/usr/local/iotdisplay/
User=root
Group=root
Restart=on-failure
SyslogIdentifier=iotdisplay

[Install]
WantedBy=multi-user.target
```

Make sure the paths above match the path the project was deployed to.

Set up to start automatically:
```
sudo systemctl enable iotdisplay
```
Start the web app:
```
sudo systemctl start iotdisplay
```
Watch the E-Paper screen to make sure it flashes black and white.  Test the web app one more time in a 
browser.

Hopefully success!
