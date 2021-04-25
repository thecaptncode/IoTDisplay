REM This file automatically deploys the assemblies to my Raspberry Pi.
REM You'll need to set up SSH key authentication and make changes
REM specific to your environment.
REM

echo Deploying...
dotnet publish -r linux-arm -c Release
ssh pi@iotdisplay sudo systemctl stop iotdisplay
scp pi@iotdisplay:/etc/iotdisplay/appsettings.json ./
ssh pi@iotdisplay sudo rm /etc/iotdisplay/* /home/pi/epaper/*
scp ./IoTDisplay.Console/bin/Release/net5.0/linux-arm/publish/* pi@iotdisplay:/home/pi/epaper
scp ./IoTDisplay.Api/bin/Release/net5.0/linux-arm/publish/* pi@iotdisplay:/home/pi/epaper
scp ./appsettings.json pi@iotdisplay:/home/pi/epaper/
del appsettings.json
ssh pi@iotdisplay sudo chmod 755 /home/pi/epaper/IoTDisplay.Console /home/pi/epaper/IoTDisplay.Api
ssh pi@iotdisplay sudo cp /home/pi/epaper/* /etc/iotdisplay
ssh pi@iotdisplay sudo systemctl start iotdisplay
pause
