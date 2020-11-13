/**************************************************************************
 *
 * Name: IoT Display Application
 *
 * Copyright 2020 Greg Cannon
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 **************************************************************************/

public static String version() { return "v0.1.0" }

import groovy.transform.Field

definition(
		name: "IoT Display Application",
		namespace: "captncode",
		author: "Greg Cannon",
		description: "Send information to an IoT Display Device",
		category: "Misc",
		iconUrl: "",
		iconX2Url: "",
		iconX3Url: "",
		importUrl: "https://raw.githubusercontent.com/thecaptncode/hubitat-elkm1/master/Elk-M1-Application.groovy",
		documentationLink: "https://github.com/thecaptncode/hubitat-elkm1/wiki"
)

preferences {
	page(name: "mainPage", nextPage: "statusPage")
	page(name: "statusPage", nextPage: "mainPage")
}

Map mainPage() {
	//state["indoorTempValues"] = 0
	//updateDevices()
	dynamicPage(name: "mainPage", title: "", install: false, uninstall: true) {
		section("<h2>General Configuration</h2>") {
			input name: "displayDevice", type: "capability.healthCheck", title: "Choose your IoT Display Device", required: true
			input name: "weatherDevice", type: "capability.relativeHumidityMeasurement", title: "OpenWeatherMap device to keep updated"
			input name: "indoorTempDevice", type: "capability.temperatureMeasurement", title: "Indoor Temperature"
			input name: "indoorHumidDevice", type: "capability.relativeHumidityMeasurement", title: "Indoor Humidity"
			input name: "thermostatDevice", type: "capability.thermostat", title: "Thermostat"
			input name: "dbgEnable", type: "bool", title: "Enable debug logging", defaultValue: false, submitOnChange: true
		}
		section(hideable: true, hidden: true, "OpenWeatherMap Configuration") {
			input name: "owApiKey", type: "text", title: "OpenWeatherMap API key"
			input name: "owLatitude", type: "text", title: "Latitude", description: "-90..90"
			input name: "owLongitude", type: "text", title: "Longitude", description: "-180..180"
			input name: "owUnits", type: "enum", title: "Units", defaultValue: "imperial",
					options: [['standard': "Standard"], ['metric': "Metric"], ['imperial': "Imperial"]]
			input name: "owPolling", type: "enum", title: "Weather polling interval",
					options: [['': "Disabled"], ['05': "5 Minutes"], ['10': "10 Minutes"], ['15': "15 Minutes"], ['30': "30 minutes"]]
		}
		String imageURL = displayDevice?.getDataValue("fullPath")
		String linkURL = displayDevice?.getDataValue("host")
		if (imageURL && linkURL) {
			section("<h2>IoT Display Device</h2>") {
				href(name: "MyDevice", title: "My Display Device.",
						description: "Click to explore the display device APIs.",
						style: "external",
						image: imageURL,
						url: linkURL + "/swagger/index.html")
			}
		}
	}
}

Map statusPage() {
	String message = ""
	if (displayDevice.hasCommand("addDrawing")) {
		unschedule(pollWeather)
		message = "${displayDevice} has been configured for display."

		if (!owUnits)
			owUnits = "metric"
		if (owApiKey && owLatitude && owLongitude) {
			if (owPolling == null || owPolling == "") {
				message += "<br />OpenWeatherMap has been configured but will not be polled."
			} else {
				message += "<br />OpenWeatherMap has been configured."
				pollWeather()
				schedule("50 4/" + owPolling + " * ? * * *", pollWeather)
				// https://www.freeformatter.com/cron-expression-generator-quartz.html
			}
		} else {
			message += "<br />Not polling for weather.  Not all needed information supplied."
		}

		if (weatherDevice && !weatherDevice.hasAttribute("weather")) {
			message += "<br />${weatherDevice} is not a usable weather device.  Please select another one on the next page."
			log.error "${app.name} error: ${weatherDevice} wrong weather device type."
			weatherDevice = null
		} else if (weatherDevice) {
			message += "<br />${weatherDevice} has been configured for update."
		}

		if (state["indoorTempDeviceId"] != indoorTempDevice?.deviceId || state["indoorHumidDeviceId"] != indoorHumidDevice?.deviceId ||
				state["thermostatDeviceId"] != thermostatDevice?.deviceId) {
			unsubscribe()
			Map options = null
			if (indoorTempDevice) {
				subscribe(indoorTempDevice, "temperature", subscriptionHandler)
				state["indoorTempValues"] = 0
				message += "<br />${indoorTempDevice} has been subscribed for indoor temperature."
			}
			if (indoorHumidDevice) {
				subscribe(indoorHumidDevice, "humidity", subscriptionHandler)
				state["indoorHumidValues"] = 0
				message += "<br />${indoorHumidDevice} has been subscribed for indoor humidity."
			}
			if (thermostatDevice) {
				thermAttributes.each {
					subscribe(thermostatDevice, it, subscriptionHandler)
				}
				state["thermostatValues"] = [:]
				message += "<br />${thermostatDevice} has been subscribed for a thermostat."
			}
			state["indoorTempDeviceId"] = indoorTempDevice?.deviceId
			state["indoorHumidDeviceId"] = indoorHumidDevice?.deviceId
			state["thermostatDeviceId"] = thermostatDevice?.deviceId
			updateDevices()
		}
	} else {
		message = "${displayDevice} is not a usable display device.  Please select another one on the next page."
		log.error "${app.name} error: ${displayDevice} wrong display device type."
		displayDevice = null
	}

	dynamicPage(name: "statusPage", title: "", install: true) {
		section("Current Configuration") {
			paragraph "<span style=\"color:#d86917;\">" + message + "</span>"
		}
	}
}

void pollWeather() {
	String fullPath = "https://api.openweathermap.org/data/2.5/onecall?lat=" + owLatitude + "&lon=" +
			owLongitude + "&exclude=minutely,hourly&mode=json&units=" + owUnits + "&appid=" + owApiKey
	Map params = [
			uri               : fullPath,
			requestContentType: 'application/json',
			contentType       : 'application/json',
			body              : body
	]
	asynchttpGet('readWeather', params, data)
}

void readWeather(hubitat.scheduling.AsyncResponse response, Map body) {
	if (dbgEnable) {
		response.properties.each {
			log.debug "${app.name} response: ${it}"
		}
	}
	if (response.status == 200 || response.status == 202) {
		Map weatherData = response.getJson()
		Date pollDate = (new Date(((weatherData.current.dt) as long) * 1000)).clearTime()
		Map current = parseDay(weatherData.current, true)
		Map info = [:]
		Map[] daily = new Map[6]
		int cnt = 0
		weatherData.daily.each {
			info = parseDay(it, false)
			if (pollDate == (new Date(((it.dt) as long) * 1000)).clearTime()) {
				current["min"] = info["min"]
				current["max"] = info["max"]
			} else if (cnt < 6) {
				daily[cnt++] = info
			}
		}
		String text = weatherData.alerts == null ? current.desc : weatherData.alerts
		if (dbgEnable) {
			log.debug "${app.name} current: ${current}"
			daily.each {
				log.debug "${app.name} daily: ${it}"
			}
		}
		updateWeather(current, daily, text)
	}
}

Map parseDay(Map day, boolean isCurrent) {
	Map rtn = [:]
	String dayNight = (!isCurrent || (day.dt >= day.sunrise && day.dt <= day.sunset)) ? "D" : "N"
	rtn["date"] = (new Date((day.dt as long) * 1000)).clearTime()
	rtn["temp"] = isCurrent ? Math.round(day.temp) : 0
	rtn["min"] = isCurrent ? 0 : Math.round(day.temp["min"])
	rtn["max"] = isCurrent ? 0 : Math.round(day.temp["max"])
	rtn["humid"] = day.humidity
	BigDecimal speed = day.wind_speed
	if (speed >= 0 && speed < 5)
		rtn["speed"] = "F0B7"
	else if (speed >= 5 && speed < 10)
		rtn["speed"] = "F0B8"
	else if (speed >= 10 && speed < 15)
		rtn["speed"] = "F0B9"
	else if (speed >= 15 && speed < 20)
		rtn["speed"] = "F0BA"
	else if (speed >= 20 && speed < 25)
		rtn["speed"] = "F0BB"
	else if (speed >= 25 && speed < 30)
		rtn["speed"] = "F0BC"
	else if (speed >= 30 && speed < 35)
		rtn["speed"] = "F0BD"
	else if (speed >= 35 && speed < 40)
		rtn["speed"] = "F0BE"
	else if (speed >= 40 && speed < 45)
		rtn["speed"] = "F0BF"
	else if (speed >= 45 && speed < 50)
		rtn["speed"] = "F0C0"
	else if (speed >= 50 && speed < 55)
		rtn["speed"] = "F0C1"
	else if (speed >= 55 && speed < 60)
		rtn["speed"] = "F0C2"
	else if (speed >= 60 && speed < 65)
		rtn["speed"] = "F0C3"
	else if (speed >= 65)
		rtn["speed"] = "F050"
	int dir = day.wind_deg
	if (dir >= 338 || dir <= 22)
		rtn["direction"] = "F05C"
	else if (dir >= 23 && dir <= 67)
		rtn["direction"] = "F05A"
	else if (dir >= 68 && dir <= 112)
		rtn["direction"] = "F059"
	else if (dir >= 113 && dir <= 157)
		rtn["direction"] = "F05D"
	else if (dir >= 158 && dir <= 202)
		rtn["direction"] = "F060"
	else if (dir >= 203 && dir <= 247)
		rtn["direction"] = "F05E"
	else if (dir >= 248 && dir <= 292)
		rtn["direction"] = "F061"
	else if (dir >= 293 && dir <= 337)
		rtn["direction"] = "F05B"
	rtn["pressure"] = day.pressure
	rtn["uvi"] = day.uvi == null ? 0 : Math.round(day.uvi)
	String icon = ""
	String desc = ""
	int lastPri = -1
	String owWeather = ""
	String OwIcon = ""
	day.weather.each {
		desc += ", " + it.description.capitalize()
		int thisPri = weatherPriority[it.id.toString().substring(0, 1)].toInteger()
		if (thisPri > lastPri) {
			icon = weatherID[it.id.toString() + dayNight]
			owWeather = day.description
			owIcon = day.icon
		}
	}
	if (icon == null)
		rtn["icon"] = "F07B"
	else
		rtn["icon"] = icon
	if (desc.length() > 2)
		rtn["desc"] = desc.substring(2)
	else
		rtn["desc"] = ""
	if (isCurrent && weatherDevice) {
		if (weatherDevice.currentState("cloudiness")?.value != day.clouds)
			weatherDevice.sendEvent(name: "cloudiness", value: day.clouds, unit: "%")
		if (weatherDevice.currentState("humidity")?.value != day.humidity)
			weatherDevice.sendEvent(name: "humidity", value: day.humidity, unit: "%")
		if (weatherDevice.currentState("pressure")?.value != day.pressure)
			weatherDevice.sendEvent(name: "pressure", value: day.pressure, unit: "hPa")
		if (weatherDevice.currentState("temperature")?.value != rtn["temp"])
			weatherDevice.sendEvent(name: "temperature", value: rtn["temp"], unit: "˚F")
		if (weatherDevice.currentState("windDirection")?.value != day.wind_deg)
			weatherDevice.sendEvent(name: "windDirection", value: day.wind_deg, unit: "degrees")
		if (weatherDevice.currentState("windSpeed")?.value != day.wind_speed)
			weatherDevice.sendEvent(name: "windSpeed", value: day.wind_speed, unit: "MPH")
		if (weatherDevice.currentState("weather")?.value != owWeather)
			weatherDevice.sendEvent(name: "weather", value: owWeather)
		if (weatherDevice.currentState("weatherIcons")?.value != owIcon)
			weatherDevice.sendEvent(name: "weatherIcons", value: owIcon)
	}
	return rtn
}

void updateWeather(Map current, Map[] daily, String text) {
	String fontCurrent = "font-family=\"Poiret One\" font-weight=\"900\" font-size=\"36\""
	String fontDaily = "font-family=\"Poiret One\" font-weight=\"900\" font-size=\"26\""
	if (!displayDevice?.hasCommand("addDrawing"))
		return
	String start
	int x = 0
	int y = 0
	def duration = groovy.time.TimeCategory.minus(
			new Date(),
			Date.parse("yyyy-MM-dd", "2001-01-01")
	);
	double days = duration.days + ((duration.hours / 24) as double) + ((duration.minutes / 1440) as double) +
			((duration.seconds / 86400) as double)
	days = ((days * 0.03386319269) as double) + 0.20439731
	String moon = moonPhases[Math.round(((days % 1) as double) * 28).toString()]
	if (dbgEnable)
		log.debug "Moon phase: " + moon
	if (moon) {
		start = "<rect width=\"60\" height=\"60\" fill=\"white\" />" +
				"<text x=\"5\" y=\"60\" font-size=\"70\" font-family=\"Weather Icons\" fill=\"black\">&#x" +
				moon + ";</text>"
		displayDevice.addDrawing(645, 180, 60, 60, start, "yes")
	}
	// Draw current weather
	start = "<rect width=\"180\" height=\"190\" fill=\"white\" />"
	x = 100
	y = 50
	start += "<text x=\"$x\" y=\"$y\" " + fontCurrent + " text-anchor=\"middle\" fill=\"black\">" +
			current.temp + "°&#160;" + current.humid + "%</text>"
	y = 115
	start += "<text x=\"$x\" y=\"$y\" font-size=\"50\" font-family=\"Weather Icons\" text-anchor=\"middle\">&#x" +
			current.icon + "; &#x" + current.speed + ";</text>"
	y = 165
	start += "<text x=\"$x\" y=\"$y\" " + fontCurrent + " text-anchor=\"middle\">" +
			current.max + "° / " + current.min + "°</text>"
	displayDevice.addDrawing(0, 85, 180, 190, start, "yes")

	// Draw forecast, high temperature graph and weather text
	int mintemp = current.min
	int maxtemp = current.max
	daily.each {
		if (it.min < mintemp)
			mintemp = it.min
		if (it.max > maxtemp)
			maxtemp = it.max
	}
	if (mintemp < maxtemp) {
		float scale = maxtemp - mintemp
		if (scale <= 22.5)
			scale = 2
		else if (scale <= 45)
			scale = 1
		else
			scale = 45 / scale
		int cnt = 0
		x = -0.5 * 800 / 5
		y = Math.round(160 - (current.max - mintemp) * scale)
		int y2 = Math.round(160 - (current.min - mintemp) * scale)
		String drawH = "<path d=\"M $x $y "
		String drawL = "<path d=\"M $x $y2 "
		start = "<rect width=\"550\" height=\"32\" fill=\"white\" />" +
				"<text x=\"0\" y=\"30\" " + fontDaily + " fill=\"black\">" +
				text + "</text>"
		displayDevice.addDrawing(10, 10, 550, 32, start, "yes")
		String forecast = "<rect width=\"800\" height=\"170\" fill=\"white\" />"
		daily.each {
			x = Math.round((cnt++ + 0.5) * 800 / 5)
			y = Math.round(160 - (it.max - mintemp) * scale)
			y2 = Math.round(160 - (it.min - mintemp) * scale)
			drawH += "L $x $y "
			drawL += "L $x $y2 "
			if (cnt < 6) {
				y = 30
				forecast += "<text x=\"$x\" y=\"$y\" " + fontDaily + " text-anchor=\"middle\" fill=\"black\">" +
						it.date.format("EEE") + "</text>"
				y = 75
				forecast += "<text x=\"$x\" y=\"$y\" font-size=\"30\" font-family=\"Weather Icons\" text-anchor=\"middle\">&#x" +
						it.icon + "; &#x" + it.speed + ";</text>"
				y = 115
				forecast += "<text x=\"$x\" y=\"$y\" " + fontDaily + " text-anchor=\"middle\">" +
						it.max + "° / " + it.min + "°</text>"
			}
		}
		drawH += "\" fill=\"white\" stroke=\"black\"/>"
		drawL += "\" fill=\"white\" stroke=\"black\" stroke-dasharray=\"2\"/>"
		displayDevice.addDrawing(0, 310, 800, 170, forecast + drawH + drawL, "yes")
	}
}

void updateDevices() {
	if (!displayDevice?.hasCommand("addDrawing"))
		return
	int indoorTemp = 0
	int indoorHumid = 0
	Map thermostat = [:]
	boolean changed = true
	if (indoorTempDevice) {
		indoorTemp = Math.round(indoorTempDevice.currentState("temperature").getNumberValue())
		if (state["indoorTempValues"] != indoorTemp)
			changed = true
	}
	if (indoorHumidDevice) {
		indoorHumid = Math.round(indoorHumidDevice.currentState("humidity").getNumberValue())
		if (state["indoorHumidValues"] != indoorHumid)
			changed = true
	}
	if (thermostatDevice) {
		thermAttributes.each {
			thermostat.put(it, thermostatDevice.currentState(it).value)
		}
		if (!state["thermostatValues"].equals(thermostat))
			changed = true
	}
	if (changed) {
		String font = "font-family=\"Poiret One\" font-weight=\"900\" font-size=\"32\""
		String font2 = "font-family=\"Poiret One\" font-weight=\"900\" font-size=\"36\""
		int x = 0
		int y = 0
		String mode
		switch (thermostat.thermostatMode) {
			case "auto":
				mode = " «» "
				break
			case "cool":
				mode = " « "
				break
			case "emergency heat":
			case "heat":
				mode = " » "
				break
			case "default":
				mode = " × "
				break
		}
		String start = "<path d=\"M 90 1 L 179 88 L 179 189 L 1 189 L 1 88 Z\" fill=\"white\" stroke=\"black\" stroke-width=\"2\" />"
		x = 90
		y = 70
		start += "<text x=\"$x\" y=\"$y\" " + font2 + " text-anchor=\"middle\" fill=\"black\">" +
				(indoorTemp ?: "- -") + "°</text>"
		x = 90
		y = 120
		start += "<text x=\"$x\" y=\"$y\" " + font + " text-anchor=\"middle\">" +
				"<tspan dx=\"-70\" dy=\"0\" font-size=\"28\" font-family=\"Weather Icons\">&#xF053; </tspan>" +
				(thermostat.coolingSetpoint ?: "- -") + "°" + mode + (thermostat.heatingSetpoint ?: "- -") +
				"°<tspan dx=\"70\" dy=\"0\" font-size=\"28\" font-family=\"Weather Icons\"> &#xF055;</tspan></text>"
		x = 90
		y = 175
		start += "<text x=\"$x\" y=\"$y\" " + font2 + " text-anchor=\"middle\">" +
				(thermostat.temperature ?: "- -") + "°&#160;" + (indoorHumid ?: "- -") + "%</text>"
		displayDevice.addDrawing(310, 80, 180, 190, start, "no")

		state["indoorTempValues"] = indoorTemp
		state["indoorHumidValues"] = indoorHumid
		state["thermostatValues"] = thermostat
	}
}

void subscriptionHandler(com.hubitat.hub.domain.Event evt) {
	// evt?.properties?.each { item -> log.debug "$item.key = $item.value" }
	if ((evt.deviceId == indoorTempDevice?.deviceId && evt.name == "temperature") ||
			(evt.deviceId == indoorHumidDevice?.deviceId && evt.name == "humidity") ||
			(evt.deviceId == thermostatDevice?.deviceId && thermAttributes.indexOf(evt.name) >= 0))
		runIn(2, updateDevices)
}

void installed() {
	initialize()
}

void updated() {
	log.info app.name + " updated"
	initialize()
}

void initialize() {
	log.info app.name + " initialize"
}

void uninstalled() {
	log.info app.name + " uninstalled"
}

@Field final List<String> thermAttributes = ["temperature", "thermostatMode", "coolingSetpoint", "heatingSetpoint"]
@Field final Map moonPhases = [
		"0" : "F0EB",
		"1" : "F0D0",
		"2" : "F0D1",
		"3" : "F0D2",
		"4" : "F0D3",
		"5" : "F0D4",
		"6" : "F0D5",
		"7" : "F0D6",
		"8" : "F0D7",
		"9" : "F0D8",
		"10": "F0D9",
		"11": "F0DA",
		"12": "F0DB",
		"13": "F0DC",
		"14": "F0DD",
		"15": "F0DE",
		"16": "F0DF",
		"17": "F0E0",
		"18": "F0E1",
		"19": "F0E2",
		"20": "F0E3",
		"21": "F0E4",
		"22": "F0E5",
		"23": "F0E6",
		"24": "F0E7",
		"25": "F0E8",
		"26": "F0E9",
		"27": "F0EA"
]
@Field final Map weatherPriority = ["7": 0, "8": 1, "3": 2, "5": 3, "2": 4, "6": 5]
@Field final Map weatherID = [
		"200D": "F00E",
		"200N": "F02C",
		"201D": "F010",
		"201N": "F02D",
		"202D": "F010",
		"202N": "F02D",
		"210D": "F005",
		"210N": "F025",
		"211D": "F005",
		"211N": "F025",
		"212D": "F005",
		"212N": "F025",
		"221D": "F005",
		"221N": "F025",
		"230D": "F06B",
		"230N": "F06D",
		"231D": "F068",
		"231N": "F06A",
		"232D": "F00E",
		"232D": "F068",
		"232N": "F02C",
		"232N": "F06A",
		"300D": "F065",
		"300N": "F067",
		"301D": "F065",
		"301N": "F067",
		"302D": "F0B2",
		"302N": "F0B4",
		"310D": "F0B2",
		"310N": "F0B4",
		"311D": "F009",
		"311N": "F029",
		"312D": "F009",
		"312N": "F029",
		"313D": "F006",
		"313N": "F026",
		"314D": "F006",
		"314N": "F026",
		"321D": "F006",
		"321N": "F026",
		"500D": "F00B",
		"500N": "F02B",
		"501D": "F00B",
		"501N": "F02B",
		"502D": "F004",
		"502N": "F024",
		"503D": "F004",
		"503N": "F024",
		"504D": "F007",
		"504N": "F027",
		"511D": "F007",
		"511D": "F00A",
		"511N": "F027",
		"511N": "F02A",
		"520D": "F007",
		"520N": "F027",
		"521D": "F008",
		"521N": "F028",
		"522D": "F008",
		"522N": "F028",
		"531D": "F008",
		"531N": "F028",
		"600D": "F077",
		"600N": "F077",
		"601D": "F077",
		"601N": "F077",
		"602D": "F076",
		"602N": "F076",
		"611D": "F076",
		"611N": "F076",
		"612D": "F076",
		"612N": "F076",
		"614D": "F076",
		"614N": "F076",
		"615D": "F077",
		"615N": "F077",
		"616D": "F077",
		"616N": "F077",
		"620D": "F077",
		"620N": "F077",
		"621D": "F076",
		"621N": "F076",
		"622D": "F076",
		"622N": "F076",
		"701D": "F063",
		"701N": "F063",
		"711D": "F0C7",
		"711N": "F0C7",
		"721D": "F021",
		"721N": "F021",
		"731D": "F03E",
		"731N": "F03E",
		"741D": "F021",
		"741N": "F021",
		"751D": "F063",
		"751N": "F063",
		"761D": "F063",
		"761N": "F063",
		"762D": "F0C8",
		"762N": "F0C8",
		"771D": "F03D",
		"771N": "F03D",
		"781D": "F056",
		"781N": "F056",
		"800D": "F00D",
		"800N": "F02E",
		"801D": "F00C",
		"801N": "F081",
		"802D": "F002",
		"802N": "F086",
		"803D": "F001",
		"803N": "F023",
		"804D": "F003",
		"804N": "F04A"
]

/* Test clock
{
  "timezone": "America/New_York",
  "x": 550,
  "y": 0,
  "width": 250,
  "height": 110,
  "svgCommands": '<rect width="250" height="110" fill="white" /><text x="225" y="60" font-family="Poiret One" font-weight="900" font-size="48" text-anchor="end" fill="black">{0:t}</text><text x="225" y="110" font-family="Poiret One" font-weight="900" font-size="38" text-anchor="end" fill="black">{0:ddd MMM d}</text>'
}
*/