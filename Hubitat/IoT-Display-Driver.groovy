/**************************************************************************
 *
 * Name: IoT Display Driver
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

metadata {
	definition(name: "IoT Display Driver", namespace: "captncode", author: "Greg Cannon", component: false) {
		capability "Actuator"
		capability "HealthCheck"
		capability "Refresh"
		attribute "lastChecked", "String"
		attribute "lastFailure", "String"
		attribute "lastRetry", "String"
		attribute "lastSuccess", "String"
		command "addClock", [[name: "timezone", description: "Clock time zone", type: "STRING"]]
		command "addClockDrawing", [[name: "timezone*", description: "Clock time zone", type: "STRING"],
									[name: "xPos*", description: "X pos", type: "NUMBER"],
									[name: "yPos*", description: "Y pos", type: "NUMBER"],
									[name: "width*", description: "Drawing width", type: "NUMBER"],
									[name: "height*", description: "Drawing height", type: "NUMBER"],
									[name: "svgCommands", description: "SVG commands", type: "STRING"]]
		command "addClockImage", [[name: "timezone*", description: "Clock time zone", type: "STRING"],
								  [name: "xPos*", description: "X pos", type: "NUMBER"],
								  [name: "yPos*", description: "Y pos", type: "NUMBER"],
								  [name: "filename*", description: "File name", type: "STRING"]]
		command "addClockTime", [[name: "timezone*", description: "Clock time zone", type: "STRING"],
								 [name: "xPos*", type: "NUMBER"],
								 [name: "yPos*", type: "NUMBER"],
								 [name: "timeFormat*", type: "STRING"],
								 [name: "horizAlign*", type: "NUMBER"],
								 [name: "vertAlign*", type: "NUMBER"],
								 [name: "font*", type: "STRING"],
								 [name: "fontSize*", type: "NUMBER"],
								 [name: "fontWeight*", type: "NUMBER"],
								 [name: "fontWidth*", type: "NUMBER"],
								 [name: "textColor*", type: "STRING"],
								 [name: "backgroundColor", type: "STRING"]]
		command "addDrawing", [[name: "xPos*", description: "X pos", type: "NUMBER"],
							   [name: "yPos*", description: "Y pos", type: "NUMBER"],
							   [name: "width*", description: "Drawing width", type: "NUMBER"],
							   [name: "height*", description: "Drawing height", type: "NUMBER"],
							   [name: "svgCommands*", description: "SVG commands", type: "STRING"],
							   [name: "delay", description: "Delay update until next cycle", type: "ENUM", constraints: ["no", "yes"]]]
		command "addImage", [[name: "xPos*", description: "X pos", type: "NUMBER"],
							 [name: "yPos*", description: "Y pos", type: "NUMBER"],
							 [name: "filename*", description: "File name", type: "STRING"],
							 [name: "delay", description: "Delay update until next cycle", type: "ENUM", constraints: ["no", "yes"]]]
		command "addText", [[name: "xPos*", type: "NUMBER"],
							[name: "yPos*", type: "NUMBER"],
							[name: "text*", type: "STRING"],
							[name: "horizAlign*", type: "NUMBER"],
							[name: "vertAlign*", type: "NUMBER"],
							[name: "font*", type: "STRING"],
							[name: "fontSize*", type: "NUMBER"],
							[name: "fontWeight*", type: "NUMBER"],
							[name: "fontWidth*", type: "NUMBER"],
							[name: "color*", description: "Hex color", type: "STRING"],
							[name: "delay", description: "Delay update until next cycle", type: "ENUM", constraints: ["no", "yes"]]]
		command "clearClocks"
		command "clearScreen"
		command "deleteClock", [[name: "timezone", description: "Clock time zone", type: "STRING"]]
		command "sendCmd", [[name: "command*", description: "Command", type: "ENUM", constraints:
				["Draw", "Image", "Text", "Clock", "ClockDraw",
				 "ClockImage", "ClockTime", "ClockDelete"]],
							[name: "json*", type: "JSON_OBJECT"]]
	}

	preferences {
		input name: "protocol", type: "enum", title: "Protocol", options: ["http", "https"], defaultValue: "http", required: true
		input name: "host", type: "text", title: "Host name or IP address", required: true
		input name: "port", type: "number", title: "Port", range: 1..65535, required: true, defaultValue: 5000
		input name: "path", type: "text", title: "API Url", required: true, defaultValue: "/api/IoTDisplay/"
		input name: "dbgEnable", type: "bool", title: "Enable debug logging", defaultValue: false
		input name: "txtEnable", type: "bool", title: "Enable descriptionText logging", defaultValue: true
	}
}

void updated() {
	initialize()
	log.info "${device.label} Updated..."
	log.warn "${device.label} description logging is: ${txtEnable}"
}

void installed() {
	log.info "${device.label} Installed..."
	device.updateSetting("protocol", [type: "text", value: "http"])
	device.updateSetting("port", [type: "number", value: 5000])
	device.updateSetting("path", [type: "text", value: "/api/IoTDisplay/"])
	device.updateSetting("dbgEnable", [type: "bool", value: false])
	device.updateSetting("txtEnable", [type: "bool", value: true])
	initialize()
}

void initialize() {
	String val = "${protocol}://${host}:${port}"
	if (!path)
		path = "/"
	else if (!path.startsWith("/"))
		path = "/" + path
	if (!path.endsWith("/"))
		path += "/"
	device.updateDataValue("host", val)
	device.updateDataValue("fullPath", val + path)
}

void addClock(String timezone = "") {
	Map json = ["timezone": timezone]
	sendCmd("POST", "Clock", json)
}

void addClockDrawing(String timezone, BigDecimal xPos, BigDecimal yPos, BigDecimal width, BigDecimal height,
					 String svgCommands = "") {
	Map json = ["timezone": timezone, "x": xPos, "y": yPos, "width": width, "height": height, "svgCommands": svgCommands]
	sendCmd("POST", "ClockDraw", json)
}

void addClockImage(String timezone, BigDecimal xPos, BigDecimal yPos, String filename = "") {
	Map json = ["timezone": timezone, "x": xPos, "y": yPos, "filename": filename]
	sendCmd("POST", "ClockImage", json)
}

void addClockTime(String timezone, BigDecimal xPos, BigDecimal yPos, String timeFormat, BigDecimal horizAlign, BigDecimal, vertAlign,
				  String font, BigDecimal fontSize, BigDecimal fontWeight, BigDecimal fontWidth, String textColor, String backgroundColor = "") {
	Map json = ["timezone"       : timezone, "x": xPos, "y": yPos, "formatstring": timeFormat, "horizAlign": horizAlign, "vertAlign": vertAlign,
				"font"           : font, "fontSize": fontSize, "fontWeight": fontWeight, "fontWidth": fontWidth, "textColor": textColor,
				"backgroundColor": backgroundColor]
	sendCmd("POST", "ClockTime", json)
}

void addDrawing(BigDecimal xPos, BigDecimal yPos, BigDecimal width, BigDecimal height, String svgCommands, String delay = "no") {
	Map json = ["x": xPos, "y": yPos, "width": width, "height": height, "svgCommands": svgCommands, "delay": delay == "yes"]
	sendCmd("POST", "Draw", json)
}

void addImage(BigDecimal xPos, BigDecimal yPos, String filename, String delay = "no") {
	Map json = ["x": xPos, "y": yPos, "filename": filename, "delay": delay == "yes"]
	sendCmd("POST", "Image", json)
}

void addText(BigDecimal xPos, BigDecimal yPos, String text, BigDecimal horizAlign, BigDecimal, vertAlign, String font, BigDecimal fontSize,
			 BigDecimal fontWeight, BigDecimal fontWidth, String color, String delay = "no") {
	Map json = ["x"         : xPos, "y": yPos, "value": text, "horizAlign": horizAlign, "vertAlign": vertAlign, "font": font, "fontSize": fontSize,
				"fontWeight": fontWeight, "fontWidth": fontWidth, "hexColor": color, "delay": delay == "yes"]
	sendCmd("POST", "Text", json)
}


void clearClocks() {
	sendCmd("GET", "ClockClear", null)
}

void clearScreen() {
	sendCmd("GET", "Clear", null)
}

void deleteClock(String timezone = "") {
	Map json = ["timezone": timezone]
	sendCmd("POST", "ClockDelete", json)
}

void ping() {
	Date oldest = new Date(0)
	sendEvent(name: "lastChecked", value: oldest.toLocaleString(), unit: "date",
			descriptionText: "${device.label} was pinged.")
	sendEvent(name: "checkInterval", value: -1, descriptionText: "${device.label} was pinged.")
	sendCmd("GET", "LastUpdated", null)
}

void refresh() {
	sendCmd("GET", "Refresh", null)
}

void parse(String description) { log.info "${device.label} parse(String description) received: ${description}" }

void parse(hubitat.scheduling.AsyncResponse response, Map body) {
	if (dbgEnable) {
		log.debug "${device.label} parse(${response.status}, ${body})"
		response.each {
			log.debug "${device.label} Response: ${it}"
		}
		response.properties.each {
			log.debug "${device.label} Property: ${it}"
		}
	}
	if (response.status == 200 || response.status == 202) {
		if (body["hubitatCommand"] != null && body["hubitatCommand"] == "LastUpdated") {
			Date current = new Date()
			String dt = response.properties.data.replaceAll(/"/, '')
			dt = dt.substring(0, dt.length() - 5) + "+00:00"
			BigDecimal diff = Math.round((current.getTime() - toDateTime(dt).getTime()) / 1000)
			if (diff > 99999)
				diff = -1
			sendEvent(name: "lastChecked", value: current.toLocaleString(), unit: "date",
					descriptionText: "${device.label} responded to ping.")
			sendEvent(name: "checkInterval", value: diff, unit: "seconds",
					descriptionText: "${device.label} was last updated ${diff} seconds ago.")
		}
	} else {
		retryFailed(body, "Request failed with status ${response.status} and message ${(response.errorData ?: response.errorMessage)} on ${body}")
	}
}

void retryFailed(Map data, String message) {
	if (data["hubitatRetry"] < 10) {
		data["hubitatRetry"] += 1
		log.warn "${message}. Retrying ${data["hubitatRetry"]} of 10 attempts in 30 seconds."
		if (state["retryList"] == null)
			state["retryList"] = []
		state["retryList"].add(data)
		runIn(30, 'resend')
	} else {
		sendEvent(name: "lastFailure", value: new Date().toLocaleString(), unit: "date",
				descriptionText: "${device.label} a command retry failed after 10 attempts.")
		log.error "${message}. Retry failed. Giving up after 10 attempts."
	}
}

void resend() {
	List<Map> retry = state["retryList"]
	state["retryList"] = []
	if (retry.size() > 0)
		sendEvent(name: "lastRetry", value: new Date().toLocaleString(), unit: "date",
				descriptionText: "${device.label} retried failed commands.")
	retry.each {
		sendCmd(it["hubitatMethod"], it["hubitatCommand"], it)
	}
}

void sendCmd(String command, String json) {
	Map data = new groovy.json.JsonSlurper().parseText(json)
	sendCmd("POST", command, data)
}

void sendCmd(String method, String command, Map data) {
	String fullPath = device.getDataValue("fullPath")
	if (!fullPath) {
		log.error "Unable to use device settings.  Please update preferences."
		return
	}
	Map body = null
	if (data == null) {
		data = [:]
	} else {
		body = [:]
		data.each {
			if (it.key != "hubitatRetry" && it.key != "hubitatMethod" && it.key != "hubitatCommand")
				body[it.key] = it.value
		}
	}
	if (body != null && body.size() == 0)
		body = null;
	Map params = [
			uri               : fullPath + command,
			requestContentType: 'application/json',
			contentType       : 'application/json',
			body              : body
	]

	if (data["hubitatRetry"] == null)
		data["hubitatRetry"] = 0
	data["hubitatMethod"] = method
	data["hubitatCommand"] = command

	try {
		if (method == "GET")
			asynchttpGet('parse', params, data)
		else
			asynchttpPost('parse', params, data)
	}
	catch (e) {
		retryFailed(data, "${device.label} had an error in sendCmd. Exception ${e} on ${params}")
	}
}

/***********************************************************************************************************************
 *
 * Release Notes
 *
 * 0.1.0
 * New IoT Display Driver
 *
 ***********************************************************************************************************************/
