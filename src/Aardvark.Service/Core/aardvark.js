
//gamepad:
(function () { 'use strict'; var _requestAnimationFrame, _cancelAnimationFrame, hasGamepadSupport = window.navigator.getGamepads !== undefined; if (String(typeof window) !== 'undefined') { ['webkit', 'moz'].forEach(function (key) { _requestAnimationFrame = _requestAnimationFrame || window.requestAnimationFrame || window[key + 'RequestAnimationFrame'] || null; _cancelAnimationFrame = _cancelAnimationFrame || window.cancelAnimationFrame || window[key + 'CancelAnimationFrame'] || null }) } function findKeyMapping(index, mapping) { var results = []; Object.keys(mapping).forEach(function (key) { if (mapping[key] === index) { results.push(key) } else if (Array.isArray(mapping[key]) && mapping[key].indexOf(index) !== -1) { results.push(key) } }); return results } function Gamepad() { this._events = { gamepad: [], axes: [], keyboard: {} }; this._handlers = { gamepad: { connect: null, disconnect: null } }; this._keyMapping = { gamepad: { 'button_1': 0, 'button_2': 1, 'button_3': 2, 'button_4': 3, 'shoulder_top_left': 4, 'shoulder_top_right': 5, 'shoulder_bottom_left': 6, 'shoulder_bottom_right': 7, 'select': 8, 'start': 9, 'stick_button_left': 10, 'stick_button_right': 11, 'd_pad_up': 12, 'd_pad_down': 13, 'd_pad_left': 14, 'd_pad_right': 15, 'vendor': 16 }, axes: { 'stick_axis_left': [0, 2], 'stick_axis_right': [2, 4] }, keyboard: { 'button_1': 32, 'start': 27, 'd_pad_up': [38, 87], 'd_pad_down': [40, 83], 'd_pad_left': [37, 65], 'd_pad_right': [39, 68] } }; this._threshold = 0.3; this._listeners = []; this._handleKeyboardEventListener = this._handleKeyboardEventListener.bind(this); this.resume() } Gamepad.prototype._handleGamepadConnected = function (index) { if (this._handlers.gamepad.connect) { this._handlers.gamepad.connect({ index: index }) } }; Gamepad.prototype._handleGamepadDisconnected = function (index) { if (this._handlers.gamepad.disconnect) { this._handlers.gamepad.disconnect({ index: index }) } }; Gamepad.prototype._handleGamepadEventListener = function (controller) { var self = this; if (controller && controller.connected) { controller.buttons.forEach(function (button, index) { var keys = findKeyMapping(index, self._keyMapping.gamepad); if (keys) { keys.forEach(function (key) { if (button.pressed) { if (!self._events.gamepad[controller.index][key]) { self._events.gamepad[controller.index][key] = { pressed: true, hold: false, released: false, player: controller.index } } self._events.gamepad[controller.index][key].value = button.value } else if (!button.pressed && self._events.gamepad[controller.index][key]) { self._events.gamepad[controller.index][key].released = true; self._events.gamepad[controller.index][key].hold = false } }) } }) } }; Gamepad.prototype._handleGamepadAxisEventListener = function (controller) { var self = this; if (controller && controller.connected) { Object.keys(self._keyMapping.axes).forEach(function (key) { var axes = Array.prototype.slice.apply(controller.axes, self._keyMapping.axes[key]); if (Math.abs(axes[0]) > self._threshold || Math.abs(axes[1]) > self._threshold) { self._events.axes[controller.index][key] = { pressed: self._events.axes[controller.index][key] ? false : true, hold: self._events.axes[controller.index][key] ? true : false, released: false, value: axes } } else if (self._events.axes[controller.index][key]) { self._events.axes[controller.index][key] = { pressed: false, hold: false, released: true, value: axes } } }) } }; Gamepad.prototype._handleKeyboardEventListener = function (e) { var self = this, keys = findKeyMapping(e.keyCode, self._keyMapping.keyboard); if (keys) { keys.forEach(function (key) { if (e.type === 'keydown' && !self._events.keyboard[key]) { self._events.keyboard[key] = { pressed: true, hold: false, released: false } } else if (e.type === 'keyup' && self._events.keyboard[key]) { self._events.keyboard[key].released = true; self._events.keyboard[key].hold = false } }) } }; Gamepad.prototype._handleEvent = function (key, events, player) { if (events[key].pressed) { this.trigger('press', key, events[key].value, player); events[key].pressed = false; events[key].hold = true } else if (events[key].hold) { this.trigger('hold', key, events[key].value, player) } else if (events[key].released) { this.trigger('release', key, events[key].value, player); delete events[key] } }; Gamepad.prototype._loop = function () { var self = this, gamepads = hasGamepadSupport ? window.navigator.getGamepads() : false, length = 4, i; if (gamepads) { for (i = 0; i < length; i = i + 1) { if (gamepads[i]) { if (!self._events.gamepad[i]) { self._handleGamepadConnected(i); self._events.gamepad[i] = {}; self._events.axes[i] = {} } self._handleGamepadEventListener(gamepads[i]); self._handleGamepadAxisEventListener(gamepads[i]) } else if (self._events.gamepad[i]) { self._handleGamepadDisconnected(i); self._events.gamepad[i] = null; self._events.axes[i] = null } } self._events.gamepad.forEach(function (gamepad, player) { if (gamepad) { Object.keys(gamepad).forEach(function (key) { self._handleEvent(key, gamepad, player) }) } }); self._events.axes.forEach(function (gamepad, player) { if (gamepad) { Object.keys(gamepad).forEach(function (key) { self._handleEvent(key, gamepad, player) }) } }) } Object.keys(self._events.keyboard).forEach(function (key) { self._handleEvent(key, self._events.keyboard, 'keyboard') }); if (self._requestAnimation) { self._requestAnimation = _requestAnimationFrame(self._loop.bind(self)) } }; Gamepad.prototype.on = function (type, button, callback, options) { var self = this; if (Object.keys(this._handlers.gamepad).indexOf(type) !== -1 && typeof button === 'function') { this._handlers.gamepad[type] = button; this._events.gamepad = [] } else { if (typeof type === "string" && type.match(/\s+/)) { type = type.split(/\s+/g) } if (typeof button === "string" && button.match(/\s+/)) { button = button.split(/\s+/g) } if (Array.isArray(type)) { type.forEach(function (type) { self.on(type, button, callback, options) }) } else if (Array.isArray(button)) { button.forEach(function (button) { self.on(type, button, callback, options) }) } else { this._listeners.push({ type: type, button: button, callback: callback, options: options }) } } }; Gamepad.prototype.off = function (type, button) { var self = this; if (typeof type === "string" && type.match(/\s+/)) { type = type.split(/\s+/g) } if (typeof button === "string" && button.match(/\s+/)) { button = button.split(/\s+/g) } if (Array.isArray(type)) { type.forEach(function (type) { self.off(type, button) }) } else if (Array.isArray(button)) { button.forEach(function (button) { self.off(type, button) }) } else { this._listeners = this._listeners.filter(function (listener) { return listener.type !== type && listener.button !== button }) } }; Gamepad.prototype.setCustomMapping = function (device, config) { if (this._keyMapping[device] !== undefined) { this._keyMapping[device] = config } else { throw new Error('The device "' + device + '" is not supported through gamepad.js') } }; Gamepad.prototype.setGlobalThreshold = function (num) { this._threshold = parseFloat(num) }; Gamepad.prototype.trigger = function (type, button, value, player) { if (this._listeners) { this._listeners.forEach(function (listener) { if (listener.type === type && listener.button === button) { listener.callback({ type: listener.type, button: listener.button, value: value, player: player, event: listener, timestamp: Date.now() }) } }) } }; Gamepad.prototype.pause = function () { _cancelAnimationFrame(this._requestAnimation); this._requestAnimation = null; document.removeEventListener('keydown', this._handleKeyboardEventListener); document.removeEventListener('keyup', this._handleKeyboardEventListener) }; Gamepad.prototype.resume = function () { this._requestAnimation = _requestAnimationFrame(this._loop.bind(this)); document.addEventListener('keydown', this._handleKeyboardEventListener); document.addEventListener('keyup', this._handleKeyboardEventListener) }; Gamepad.prototype.destroy = function () { this.pause(); delete this._listeners }; if (typeof define === 'function' && define.amd !== undefined) { define([], function () { return Gamepad }) } else if (typeof module === 'object' && module.exports !== undefined) { module.exports = Gamepad } else { window.Gamepad = Gamepad } }());

var urlCreator = window.URL;

var aardvark = document.aardvark;
if (!aardvark) {
    console.debug("[Aardvark] creating aardvark-value");
    aardvark = {};
    document.aardvark = aardvark;
    //top.aardvark = aardvark;
    //window.aardvark = aardvark;
}

// until new aardium version available
if (aardvark.electron) {



    aardvark.openFileDialog = function (config, callback) {
        if (!callback) callback = config;
        aardvark.electron.remote.dialog.showOpenDialog({ properties: ['openFile', 'multiSelections'] }).then(e => callback(e.filePaths));
    };

}

if (!aardvark.promise)
{
    aardvark.promise = new Promise(function (succ, fail) { succ(); });
}


if (!aardvark.newguid) {
    aardvark.newguid = function() {
        /// <summary>
        ///    Creates a unique id for identification purposes.
        /// </summary>
        /// <param name="separator" type="String" optional="true">
        /// The optional separator for grouping the generated segmants: default "-".    
        /// </param>

        var delim = "-";

        function S4() {
            return (((1 + Math.random()) * 0x10000) | 0).toString(16).substring(1);
        }

        return (S4() + S4() + delim + S4() + delim + S4() + delim + S4() + delim + S4() + S4() + S4());
    };

}

if (!sessionStorage.aardvarkId) {
    sessionStorage.aardvarkId = aardvark.newguid();
}

if (!aardvark.guid) {
    aardvark.guid = sessionStorage.aardvarkId;
}


if (!aardvark.channels) {
    console.debug("[Aardvark] creating aardvark-channels");
    aardvark.channels = {};
}

if (!aardvark.referencedScripts) {
    console.debug("[Aardvark] creating aardvark-script-references");
    aardvark.referencedScripts = {};
}

if (!aardvark.referencedStyles) {
    console.debug("[Aardvark] creating aardvark-stylesheet-references");
    aardvark.referencedStyles = {};
}

aardvark.referencedScripts["jquery"] = true;

if (!aardvark.processEvent) {
    console.debug("[Aardvark] creating aardvark-event-processor");
    aardvark.processEvent = function () {
        console.warn("[Aardvark] cannot process events yet (websocket not opened)");
    };
}

if (!aardvark.localhost) {
    aardvark.localhost = location.hostname === "localhost" || location.hostname === "127.0.0.1";
}

if (!aardvark.getRelativeUrl) {
    var splitPath = function (path) {
        var dirPart, filePart;
        path.replace(/^(.*\/)?([^/]*)$/, function (_, dir, file) {
            dirPart = dir; filePart = file;
        });
        return { dirPart: dirPart, filePart: filePart };
    };

    var scripts = document.head.getElementsByTagName("script");
    var selfScript = undefined;
    for (var i = 0; i < scripts.length; i++) {
        var t = scripts[i];
        var comp = splitPath(t.src);
        if (comp.filePart === "aardvark.js") {
            selfScript = comp.dirPart;
            break;
        }
    }

    if (selfScript) {
        console.debug("[Aardvark] self-url: " + selfScript);

        aardvark.getScriptRelativeUrl = function (protocol, relativePath) {

            if (relativePath.startsWith("/")) relativePath = relativePath.substring(1);

            return selfScript.replace("https://", protocol + "://").replace("http://", protocol + "://") + relativePath;
        };

    }
    
    aardvark.getRelativeUrl = function (protocol, relativePath) {
        var location = window.location;
        var path = splitPath(location.pathname);
        var dir = path.dirPart;

        if (relativePath.startsWith("/")) relativePath = relativePath.substring(1);
        if (!dir.startsWith("/")) dir = "/" + dir;
        if (!dir.endsWith("/")) dir = dir + "/";

        path = protocol + "://" + window.location.host + path.dirPart + relativePath;
        console.log(path);

        return path;
    }
}

class Renderer {

    constructor(id) {
        this.id = id;
        this.div = document.getElementById(id);

        var scene = this.div.getAttribute("data-scene");
        if (!scene) scene = id;
        this.scene = scene;

        var samples = this.div.getAttribute("data-samples");
        if (!samples) samples = 1;
        this.samples = samples;
        
        var quality = this.div.getAttribute("data-quality");
        if (!quality) samples = 80;
        this.quality = quality;

		var showFPS = this.div.getAttribute("showFPS");
		if (showFPS === "true") showFPS = true; else showFPS = false;
		this.showFPS = showFPS;

		var showLoader = this.div.getAttribute("showLoader");
		if (showLoader === "false") showLoader = false; else showLoader = true;
		this.showLoader = showLoader;

		var useMapping = this.div.getAttribute("useMapping");
		if (useMapping === "false") useMapping = false; else useMapping = true;
		this.useMapping = useMapping;

		//var onRendered = this.div.getAttribute("onRendered");
  //      if (onRendered) this.onRendered = onRendered;

        this.customLoaderImg = this.div.getAttribute("data-customLoaderImg");
        this.customLoaderImgSize = this.div.getAttribute("data-customLoaderSize");

        this.buffer = [];
        this.isOpen = false;
        this.isClosed = false;
        this.loading = true;

        this.depthCallbacks = [];

        var renderAlways = this.div.getAttribute("data-renderalways");
        if (renderAlways) renderAlways = true;
        else renderAlways = false;
        this.renderAlways = renderAlways;

        this.init();
    }

    createLoader() {
        if (!this.loader) {
            var loader = document.createElement("div");
            this.div.appendChild(loader);
            loader.setAttribute("class", "loader");

            let aardvarkHtml = 
                "<div style='color: white; margin-top: 30px; text-align: center;>" +
                "<center style='text-align: center;'>Powered by the Aardvark Platform</center>" +
                "</div >";


            let loaderHtml =
                "<div class='fountainG_0'>" +
                "<div class='fountainG_1 fountainG'></div>" +
                "<div class='fountainG_2 fountainG'></div>" +
                "<div class='fountainG_3 fountainG'></div>" +
                "<div class='fountainG_4 fountainG'></div>" +
                "<div class='fountainG_5 fountainG'></div>" +
                "<div class='fountainG_6 fountainG'></div>" +
                "<div class='fountainG_7 fountainG'></div>" +
                "<div class='fountainG_8 fountainG'></div>" +
                (this.customLoaderImg ? aardvarkHtml : "") +
                "</div>"; 

            $(loader).html(loaderHtml);

            if (this.customLoaderImg) {
                $(loader).css("background-image", this.customLoaderImg);
            }
            if (this.customLoaderImgSize) {
                $(loader).css("background-size", this.customLoaderImgSize);
            }


            this.loader = loader;
        }
        return this.loader;
    }

    destroyLoader() {
        if (this.loader) {
            var loader = this.loader;
            delete this.loader;
            this.div.removeChild(loader);
        }
    }

    init() {
        var connect = null;

        

		if (aardvark.localhost && top.aardvark.openMapping && this.useMapping) {
            var canvas = document.createElement("canvas");
            this.div.appendChild(canvas);
            canvas.setAttribute("class", "rendercontrol");

            if (this.showLoader) this.createLoader();

            this.canvas = canvas;
            this.ctx = canvas.getContext("2d");
            this.img = canvas;

            var overlay = document.createElement("span");
            if (!this.showFPS) overlay.style = "display:none;";
            this.div.appendChild(overlay);
            overlay.className = "fps";
            overlay.innerText = "";
            this.overlay = overlay;
            this.frameCount = 0;
            this.div.tabIndex = 1;
			canvas.style.cursor = "default";

			var mappedRequest = "&mapped=true";
			if (!this.useMapping) mappedRequest = "&mapped=false";

			var url = aardvark.getScriptRelativeUrl("ws", "render/" + this.id + "?session=" + aardvark.guid + "&scene=" + this.scene + "&samples=" + this.samples + mappedRequest);
            
            var self = this;


            var onGlobalClick = function (event) {
                if (event.target == self.div) self.div.focus();
            };
            document.addEventListener("click", onGlobalClick, false);


            //if (this.div.onclick) {
            //    var old = this.div.onclick;
            //    this.div.onclick = function () { console.warn("focus"); self.div.focus(); old(); };
            //}
            //else {
            //    this.div.onclick = function () { console.warn("focus"); self.div.focus(); };
            //}
            connect = function () {
                var socket = new WebSocket(url);
                socket.binaryType = "blob";
                self.socket = socket;

                //var doPing = function () {
                //    if (socket.readyState <= 1) {
                //        socket.send("#ping");
                //        setTimeout(doPing, 1000);
                //    }
                //};

                socket.onopen = function () {
                    for (var i = 0; i < self.buffer.length; i++) {
                        socket.send(self.buffer[i]);
                    }
                    self.isClosed = false;
                    self.isOpen = true;
                    self.buffer = [];

                    self.render();

                    //doPing();

                };

                socket.onmessage = function (msg) {
                    self.received(msg);
                };

                socket.onclose = function () {
                    self.isOpen = false;
                    self.isClosed = true;
                    self.fadeOut();
                    //setTimeout(connect, 500);
                };

                socket.onerror = function (err) {
                    console.warn(err);
                    self.isClosed = true;
                    self.fadeOut();
                    //setTimeout(connect, 500);
                };
            };
        }
        else {
            var img = document.createElement("img");
            this.div.appendChild(img);
            img.setAttribute("class", "rendercontrol");

            if (this.showLoader) this.createLoader();

            this.img = img;

            var overlay = document.createElement("span");
            if (!this.showFPS) overlay.style = "display:none;";
            this.div.appendChild(overlay);
            overlay.className = "fps";
            overlay.innerText = "";
            this.overlay = overlay;
            this.frameCount = 0;
            this.div.tabIndex = 1;
            //this.img.contentEditable = true;

            img.style.cursor = "default";

            var url = aardvark.getScriptRelativeUrl("ws", "render/" + this.id + "?session=" + aardvark.guid + "&scene=" + this.scene + "&samples=" + this.samples + "&quality=" + this.quality);

            var self = this;

            var onGlobalClick = function (event) {
                if (event.target == self.div) self.div.focus();
            };
            document.addEventListener("click", onGlobalClick, false);

            connect = function () {
                var socket = new WebSocket(url);
                socket.binaryType = "blob";
                self.socket = socket;

                //var doPing = function () {
                //    if (socket.readyState <= 1) {
                //        socket.send("#ping");
                //        setTimeout(doPing, 50);
                //    }
                //};

                socket.onopen = function () {
                    for (var i = 0; i < self.buffer.length; i++) {
                        socket.send(self.buffer[i]);
                    }
                    self.isClosed = false;
                    self.isOpen = true;
                    self.buffer = [];

                    self.render();

                    //doPing();

                };

                socket.onmessage = function (msg) {
                    self.received(msg);
                };

                socket.onclose = function () {
                    self.isOpen = false;
                    self.isClosed = true;
                    self.fadeOut();
                    //setTimeout(connect, 500);
                };

                socket.onerror = function (err) {
                    console.warn(err);
                    self.isClosed = true;
                    self.fadeOut();
                    //setTimeout(connect, 500);
                };
            };
        }

		var downloadDirect = function (dataurl, filename) {
			var a = document.createElement("a");
			a.href = dataurl;
			a.setAttribute("download", filename);
			var b = document.createEvent("MouseEvents");
			b.initEvent("click", false, true);
			a.dispatchEvent(b);

			return false;
		};

		function downloadURI(uri, name) {
			console.log("downloading " + uri + " -> " + name);
			var link = document.createElement("a");
			link.download = name;
			link.href = uri;
			document.body.appendChild(link);
			link.click();
			document.body.removeChild(link);
		};

		var screenshot = function () {
			var name = "screenshot"; 
			if (self.useMapping || true) {
				//name += ".png";
				//var dataurl = self.img.toDataURL("image/png");
				//download(dataurl, name);
				// workaround for currently flipped stuff.
				console.log("mapping enabled -> using fallback download mechanism via screenshot service...");
				name += ".jpg";
				var url3 = window.top.location.href + "rendering/screenshot/" + self.id + "?w=" + self.div.clientWidth + "&h=" + self.div.clientHeight + "&samples=8&fmt=png" ;
				downloadURI(url3, name);
			} 
			else {
				name += ".jpg";
				downloadURI(self.img.src, name);
			}
		};
		var ctrlDown = false;
		this.div.addEventListener("keydown", (e) => {
			if (e.keyCode === 123 && !ctrlDown) { //F12 {
				screenshot();
			}
		});

		if (top.aardvark.captureFullscreen && top.aardvark.electron) {
			console.log("installing fullscreen capturer");
			this.div.addEventListener("keydown", (e) => {
				if (e.keyCode === 17) ctrlDown = true;
				else if (e.keyCode === 123 && ctrlDown) {
					var path = top.aardvark.dialog.showSaveDialog({
						filters: [
							{ name: 'Images', extensions: ['png','jpg'] }
						]});
					console.log("saving fullscreen screenshot to" + path);
					top.aardvark.captureFullscreen(path);
				}
			});
			this.div.addEventListener("keyup", (e) => {
				if (e.keyCode === 17) ctrlDown = false;
			});
		}


        connect();

		this.div.oncontextmenu = function (e) { e.preventDefault(); };

        var $self = $(this.div);
        var w = $self.width();
        var h = $self.height();
        var check = function () {
            var cw = $self.width();
            var ch = $self.height();
            if(cw !== w || ch !== h)
            {
                w = cw;
                h = ch;
                self.render();
            }
        };
        check();
        setInterval(check, 50);
    }

    change(scene, samples) {
        if (this.scene != scene || this.samples != samples) {
            console.warn("changing to " + scene + "/" + samples);
            var message = { Case: "Change", scene: scene, samples: samples };
            this.send(JSON.stringify(message));
            this.scene = scene;
            this.samples = samples;
        }
    }

    send(data) {
        if (this.isOpen) {
            this.socket.send(data);
        }
        else if(!this.isClosed) {
            this.buffer.push(data);
        }
    }

    processEvent() {
        var sender = this.id;
        var name = arguments[0];
        var args = [];
        for (var i = 1; i < arguments.length; i++) {
            args.push(JSON.stringify(arguments[i]));
        }
        var message = JSON.stringify({ sender: sender, name: name, args: args });
        this.send(message);
    }

    subscribe(eventName) {
        var self = this;

        switch (eventName) {
            case "click":
                this.img.onclick = function (e) {
                    self.processEvent('click', e.offsetX, e.offsetY, e.button);
                    e.preventDefault();
                };
                break;

            case "dblclick":
                this.img.ondblclick = function (e) {
                    self.processEvent('dblclick', e.offsetX, e.offsetY, e.button);
                    e.preventDefault();
                };
                break;
                    
            case "mousedown":
                this.img.onmousedown = function (e) {
                    self.processEvent('mousedown', e.offsetX, e.offsetY, e.button);
                    e.preventDefault();
                }
                break;

            case "mouseup":
                this.img.onmouseup = function (e) {
                    self.processEvent('mouseup', e.offsetX, e.offsetY, e.button);
                    e.preventDefault();
                }
                break;

            case "mousemove":
                this.img.onmousemove = function (e) {
                    self.processEvent('mousemove', e.offsetX, e.offsetY);
                    e.preventDefault();
                }
                break;

            case "mouseenter":
                this.img.onmouseenter = function (e) {
                    self.processEvent('mouseenter', e.offsetX, e.offsetY);
                    e.preventDefault();
                }
                break;

            case "mouseout":
                this.img.onmouseout = function (e) {
                    self.processEvent('mouseout', e.offsetX, e.offsetY);
                    e.preventDefault();
                }
                break;

            case "mousewheel":
                this.img.onmousewheel = function(e) {
                    if (document.activeElement === this.img) {
                        self.processEvent('mousewheel', e.wheelDelta);
                        e.preventDefault();
                    }
                };
                break;

            case "keydown":
                this.img.onkeydown = function (e) {
                    self.processEvent('keydown', e.keyCode);
                    e.preventDefault();
                };
                break;

            case "keyup":
                this.img.onkeyup = function (e) {
                    self.processEvent('keyup', e.keyCode);
                    e.preventDefault();
                };
                break;

            case "keypress":
                this.img.onkeypress = function (e) {
                    self.processEvent('keypress', e.key);
                    e.preventDefault();
                };
                break;

            default:
                console.warn("cannot subscribe to event " + eventName);

        }
    }

    unsubscribe(eventName) {
        switch (eventName) {
            case "click":
                delete this.img.onclick;
                break;

            case "dblclick":
                delete this.img.ondblclick;
                break;

            case "mousedown":
                delete this.img.onmousedown;
                break;

            case "mouseup":
                delete this.img.onmouseup;
                break;

            case "mousemove":
                delete this.img.onmousemove;
                break;

            case "mouseenter":
                delete this.img.onmousemouseenter;
                break;

            case "mouseout":
                delete this.img.onmouseout;
                break;

            case "mousewheel":
                delete this.img.onmousewheel;
                break;

            case "keydown":
                delete this.img.onkeydown;
                break;

            case "keyup":
                delete this.img.onkeyup;
                break;

            case "keypress":
                delete this.img.onkeypress;
                break;

            default:
                console.warn("cannot unsubscribe from event " + eventName);

        }
    }

    fadeIn() {
        if (this.loading) {
            this.loading = false;
            var self = this;
            console.debug("[Aardvark] initialized renderControl " + this.id);
            $(this.img).animate({ opacity: 1.0 }, 400, "swing", function () {
                self.destroyLoader();
            });

        }
    }

    fadeOut() {
        if (!this.loading) {
            this.loading = true;
            this.createLoader();
            if (this.mapping) {
                this.mapping.close();
                delete this.mapping;
            }

            console.debug("[Aardvark] closed renderControl " + this.id);
            $(this.img).animate({ opacity: 0.0 }, 400, "swing");
        }
    }

    setRenderAlways(r) {
        if (r) {
            this.renderAlways = true;
            this.render();
        }
        else this.renderAlways = false;
    }

    getWorldPosition(pixel, callback) {
        this.depthCallbacks.push({ pixel: pixel, callback: callback });
        this.send(JSON.stringify({ Case: "RequestWorldPosition", pixel: { X: pixel.x, Y: pixel.y } }));
    }

    received(msg) {
        if (msg.data instanceof Blob) {
            var now = performance.now();
            if (!this.lastTime) {
                this.lastTime = now;
            }

            if (now - this.lastTime > 1000.0) {
                if (this.frameCount > 0) {
                    var dt = now - this.lastTime;
                    var cnt = this.frameCount;
                    this.lastTime = now;
                    this.frameCount = 0;
                    var fps = 1000.0 * cnt / dt;
                    this.overlay.innerText = fps.toFixed(2) + " fps";
                    if (this.overlay.style.opacity < 0.5) {
                        $(this.overlay).animate({ opacity: 1.0 }, 400, "swing");
                    }
                }
                else {
                    if (this.overlay.style.opacity > 0.5) {
                        $(this.overlay).animate({ opacity: 0.0 }, 400, "swing");
                    }
                }
            }

            this.frameCount++;

            var oldUrl = this.img.src;
            this.img.src = urlCreator.createObjectURL(msg.data.slice(32));
            delete msg.data;

            var self = this;
            var reader = new FileReader();
            reader.onload = function () {
                var dataUrl = reader.result;
                var base64 = dataUrl.split(',')[1];
                self.send(JSON.stringify({ Case: "Rendered", header: base64 }));
            };
            reader.readAsDataURL(msg.data.slice(0, 32));


            


            //var shouldSay = this.div.getAttribute("onRendered");
            //if (shouldSay) {
            //    if (this.div.onRenderedCode != shouldSay) {
            //        this.div.onRenderedCode = shouldSay;
            //        var f = new Function(shouldSay);
            //        this.div.onRendered = f.bind(this.div);
            //    }
            //    this.div.onRendered();
            //}
            //else {
            //    delete this.div.onRenderedCode;
            //    delete this.div.onRendered;
            //}

            if (this.loading) {
                this.fadeIn();
            }

            if (this.renderAlways) {

                //artificial render looop (uncommend in invalidate)
                this.render();
            }
        }
        else {
            if (msg.data.startsWith("#")) {
                this.send(msg.data);
            }
            else {
                var o = JSON.parse(msg.data);

                //type Command =
                //    | Invalidate
                //    | Subscribe of eventName : string
                //    | Unsubscribe of eventName : string

                if (o.Case === "Invalidate") {
                    if (!this.renderAlways) {
                        // TODO: what if not visible??
                        this.render();
                    }
                }
                else if (o.Case === "WorldPosition" && o.pos) {
                    if (this.depthCallbacks.length > 0) {
                        var cb = this.depthCallbacks[0];
                        cb.callback(o.pos);
                        this.depthCallbacks.splice(0, 1);
                    }
                }
                else if (o.Case === "Subscribe") {
                    var evt = o.eventName;
                    this.subscribe(evt);
                }
                else if (o.Case === "Unsubscribe") {
                    var evt = o.eventName;
                    this.unsubscribe(evt);
                }
                else if (o.name && o.size && o.length) {
                    var now = performance.now();
                    if (!this.lastTime) {
                        this.lastTime = now;
                    }

                    if (now - this.lastTime > 1000.0) {
                        if (this.frameCount > 0) {
                            var dt = now - this.lastTime;
                            var cnt = this.frameCount;
                            this.lastTime = now;
                            this.frameCount = 0;
                            var fps = 1000.0 * cnt / dt;
                            this.overlay.innerText = fps.toFixed(2) + " fps";
                            if (this.overlay.style.opacity < 0.5) {
                                $(this.overlay).animate({ opacity: 1.0 }, 400, "swing");
                            }
                        }
                        else {
                            if (this.overlay.style.opacity > 0.5) {
                                $(this.overlay).animate({ opacity: 0.0 }, 400, "swing");
                            }
                        }
                    }

                    this.frameCount++;

                    //HERE
                    if (this.mapping) {
                        if (this.mapping.name !== o.name) {
                            this.mapping.close();
                            this.mapping = top.aardvark.openMapping(o.name, o.length);
                        }
                    }
                    else {
                        this.mapping = top.aardvark.openMapping(o.name, o.length);
                    }

                    if (this.frameBufferSize) {
                        if (this.frameBufferSize.X != o.size.X || this.frameBufferSize.Y != o.size.Y) {
                            var len = o.size.X * o.size.Y * 4;
                            this.frameBuffer = new Uint8ClampedArray(len);
                            this.frameBufferSize = o.size;
                            this.frameBufferLength = len;
                        }
                    }
                    else {
                        var len = o.size.X * o.size.Y * 4;
                        this.frameBuffer = new Uint8ClampedArray(len);
                        this.frameBufferSize = o.size;
                        this.frameBufferLength = len;
                    }

                    this.canvas.width = o.size.X;
                    this.canvas.height = o.size.Y;
                    this.frameBuffer.set(new Uint8ClampedArray(this.mapping.buffer, 0, this.frameBufferLength));
                    this.ctx.putImageData(new ImageData(this.frameBuffer, o.size.X, o.size.Y), 0, 0);

                    this.send(JSON.stringify({ Case: "Rendered", header: "" }));

                    //var shouldSay = this.div.getAttribute("onRendered");
                    //if (shouldSay) {
                    //    if (this.div.onRenderedCode != shouldSay) {
                    //        this.div.onRenderedCode = shouldSay;
                    //        var f = new Function(shouldSay);
                    //        this.div.onRendered = f.bind(this.div);
                    //    }
                    //    this.div.onRendered();
                    //}
                    //else {
                    //    delete this.div.onRenderedCode;
                    //    delete this.div.onRendered;
                    //}

                    if (this.loading) {
                        this.fadeIn();
                    }

                    if (this.renderAlways) {

                        //artificial render looop (uncommend in invalidate)
                        this.render();
                    }
                }
                else {
                    console.warn("unexpected message " + o);
                }
            }
        }
    }

    render() {
        var rect = this.div.getBoundingClientRect();

        var color = { r: 0, g: 0, b: 0 };
        var bg = window.getComputedStyle(this.div).backgroundColor;
        if(typeof bg != undefined)
            color = new RGBColor(bg);

        this.send(JSON.stringify({ Case: "RequestImage", background: { A: 255, B: color.b, G: color.g, R: color.r }, size: { X: Math.round(rect.width), Y: Math.round(rect.height) } }));
    }

}

if (!aardvark.addReferences) {
    aardvark.addReferences = function (refs, realCont) {
        
        aardvark.promise = aardvark.promise.then(function () {
            return new Promise(function (s, e) {
                var cont = function () { realCont(); s(); };

                function acc(i) {
                    if (i >= refs.length) {
                        return cont;
                    }
                    else {
                        var ref = refs[i];
                        var kind = ref.kind;
                        var name = ref.name;
                        var url = ref.url;
                        if (kind === "script") {
                            if (!aardvark.referencedScripts[name]) {
                                aardvark.referencedScripts[name] = true;
                                return function () {
                                    var script = document.createElement("script");
                                    var cc = function (evt) {
                                        console.debug("[Aardvark] referenced script \"" + name + "\" (" + url + ")");
                                        acc(i + 1)();
                                    };
                                    var err = function (evt) {
                                        console.warn("[Aardvark] failed to referenced script \"" + name + "\" (" + url + ")");
                                        acc(i + 1)();
                                    };
                                    script.src = url;
                                    script.async = true;
                                    script.addEventListener("load", cc);
                                    script.addEventListener("error", err);
                                    document.getElementsByTagName("script")[0].parentNode.appendChild(script);
                                };
                            }
                            else return acc(i + 1);
                        }
                        else {
                            if (!aardvark.referencedStyles[name]) {
                                aardvark.referencedStyles[name] = true;
                                return function () {
                                    var script = document.createElement("link");
                                    var cc = function (evt) {
                                        console.debug("[Aardvark] referenced stylesheet \"" + name + "\" (" + url + ")");
                                        acc(i + 1)();
                                    };
                                    var err = function (evt) {
                                        console.warn("[Aardvark] failed to reference stylesheet \"" + name + "\" (" + url + ")");
                                        acc(i + 1)();
                                    };
                                    script.addEventListener("load", cc);
                                    script.addEventListener("error", err);
                                    script.setAttribute("rel", "stylesheet");
                                    script.setAttribute("href", url);
                                    document.head.appendChild(script);
                                };
                            }
                            else return acc(i + 1);
                        }

                    }
                }
                var real = acc(0);
                real();
            });
        });
    };
}


if (!aardvark.openFileDialog) {

    if (top.aardvark.openFileDialog) {
        aardvark.openFileDialog = top.aardvark.openFileDialog;
    }
    else {
        aardvark.openFileDialog = function () {
            alert("Aardvark openFileDialog is not yet available");
        };

        var refs =
            [
                { kind: "stylesheet", name: "semui-css", url: "./rendering/semantic.css" },
                { kind: "stylesheet", name: "semui-overrides-css", url: "./rendering/semantic-overrides.css" },
                { kind: "script", name: "semui-js", url: "./rendering/semantic.js" },
                { kind: "stylesheet", name: "jtree-base", url: "https://cdnjs.cloudflare.com/ajax/libs/jstree/3.1.1/themes/default/style.min.css" },
                { kind: "stylesheet", name: "jtree-dark", url: "https://cdnjs.cloudflare.com/ajax/libs/jstree/3.3.3/themes/default-dark/style.min.css" },
                { kind: "script", name: "jstree", url: "https://cdnjs.cloudflare.com/ajax/libs/jstree/3.1.1/jstree.min.js" },
                { kind: "script", name: "tablesort", url: "https://semantic-ui.com/javascript/library/tablesort.js" },
                { kind: "script", name: "colresize", url: "http://www.bacubacu.com/colresizable/js/colResizable-1.6.min.js" },
                { kind: "stylesheet", name: "aardfs-css", url: aardvark.getScriptRelativeUrl("http", "aardfs.css") },
                { kind: "script", name: "aardfs-js", url: aardvark.getScriptRelativeUrl("http", "aardfs.js") }
            ]

        $(document).ready(function () {


            aardvark.addReferences(refs, function () {
                var modal = document.getElementById("filebrowser-modal");
                if (!modal) {
                    var root = document.createElement("div");
                    root.setAttribute("id", "filebrowser-modal");
                    root.setAttribute("class", "ui modal");
                    $(root).html(
                        "<div class='content'>" +
                        "	<div id='filebrowser-browser'>" +
                        "	</div>" +
                        "</div>" +
                        "	<div class='actions'>" +
                        "		<div class='ui approve button'>OK</div>" +
                        "		<div class='ui cancel button'>Cancel</div>" +
                        "	</div>" +
                        "</div>"
                    );

                    document.body.appendChild(root);


                    modal = root;

                }

                console.debug("[FS] filebrowser installed")
                aardvark.openFileDialog = function (openFileConfig, callback) {

                    // if only one argument
                    if (!callback) {
                        callback = openFileConfig;
                        openFileConfig = {};
                    }

                    if (!openFileConfig.mode) openFileConfig.mode = "file";
                    if (!openFileConfig.startPath) openFileConfig.startPath = "/";
                    if (!openFileConfig.title) openFileConfig.title = "Open File";
                    if (!openFileConfig.filters) openFileConfig.filters = [];
                    if (!openFileConfig.activeFilter) openFileConfig.activeFilter = -1;
                    if (!openFileConfig.allowMultiple) openFileConfig.allowMultiple = false;

                    var config =
                    {
                        url: aardvark.getScriptRelativeUrl("http", "fs"),
                        caching: true,
                        folderSelect: (openFileConfig.mode === "folder"),
                        fileSelect: (openFileConfig.mode === "file"),
                        hideFiles: false,
                        onselect: function (path) { },
                        submit: function (path) { callback([path]); $(modal).modal('hide'); },
                        cancel: function () { console.log("[FS] cancel"); }
                    };

                    var browser = new FileBrowser(config);
                    var $browser = $('#filebrowser-browser');
                    $browser.filebrowser(browser);
                    $browser.height(screen.height - 600);

                    $(modal).modal({
                        keyboardShortcuts: true,
                        blurring: true,
                        onDeny: function () {
                            browser.cancel();
                            return true;
                        },
                        onApprove: function () {
                            browser.submit();
                        }
                    });
                    $(modal).modal('show');

                };
            });
        });
    }
}

class Channel {

    constructor(name) {
        this.name = name;
        this.pending = undefined;
        this._recv = undefined;
    }


    received(data) {
        if (this._recv) {
            for (var i = 0; i < data.length; i++) {
                var jmsg = data[i];
                var msg = JSON.parse(jmsg);
                if (msg === "commit-suicide") {
                    console.debug("[Aardvark] channel " + this.name + " was closed");
                    delete aardvark.channels[name];
                    break;
                }
                this._recv(msg);
            }
        }
    }

    get onmessage() {
        return this.received;
    }

    set onmessage(cb) {
        this._recv = cb;
    }

}

if (!aardvark.getChannel) {
    aardvark.getChannel = function (id, name) {
        var channelName = id + "_" + name;
        var channel = aardvark.channels[channelName];
        if (channel) {
            return channel;
        }
        else {
            channel = new Channel(channelName);
            aardvark.channels[channelName] = channel;
            return channel;
        }

    };
}

// rendering related
if (!aardvark.getRenderer) {
    aardvark.getRenderer = function (id) {
        var div = document.getElementById(id);
        if (!div.renderer) {
            var renderer = new Renderer(id);
            div.renderer = renderer;
        }

        return div.renderer;
    }


    $.fn.renderer = function () {
        
        var self = this.get(0);
        if (self && self.id) {
            return aardvark.getRenderer(self.id);
        }
        else return undefined;
    };
}

if (!aardvark.render) {
    aardvark.render = function (id) {
        var r = aardvark.getRenderer(id);
        r.render();
    }
}

if (!aardvark.getWorldPosition) {
    aardvark.getWorldPosition = function (id, pixel, callback) {
        var r = aardvark.getRenderer(id);
        r.getWorldPosition(pixel, callback)
    };
}

if (!aardvark.connect) {
    aardvark.connect = function (path) {
        var urlParams;
        var match,
            pl = /\+/g,  // Regex for replacing addition symbol with a space
            search = /([^&=]+)=?([^&]*)/g,
            decode = function (s) { return decodeURIComponent(s.replace(pl, " ")); },
            query = window.location.search.substring(1);


        var wsQuery = '?session=' + aardvark.guid;

        while (match = search.exec(query))
            wsQuery = wsQuery + "&" + decode(match[1]) + "=" + decode(match[2]);


        var url = aardvark.getRelativeUrl('ws', path + wsQuery);
        var eventSocket = new WebSocket(url);

        //var doPing = function () {
        //    if (eventSocket.readyState <= 1) {
        //        eventSocket.send("#ping");
        //        setTimeout(doPing, 500);
        //    }
        //};
        

        eventSocket.onopen = function () {
            aardvark.processEvent = function () {
                var sender = arguments[0];
                var name = arguments[1];
                var args = [];
                for (var i = 2; i < arguments.length; i++) {
                    args.push(JSON.stringify(arguments[i]));
                }
                var message = JSON.stringify({ sender: sender, name: name, args: args });
                eventSocket.send(message);
            };
            //doPing();
        };

        var exectutedCode = "";

        eventSocket.onmessage = function (m) {
            var c = m.data.substring(0, 1);
            if (c === "x") {
                var data = m.data.substring(1, m.data.length);
                aardvark.promise = aardvark.promise.then(function () {
                    try {
                        //exectutedCode = exectutedCode + "\r\n\r\n\r\n" + data;
                        (new Function("{\r\n" + data + "\r\n}"))();
                    } catch (e) {
                        console.warn("could not execute event message with exn " + e + ":\n" + data);
                        debugger;
                    }
                });
            }
            else {
                var data = m.data;
                // { targetId : string; channel : string; data : 'a }
                var message = JSON.parse(data);
                var channelName = message.targetId + "_" + message.channel;

                aardvark.promise = aardvark.promise.then(function () {
                    try{
                        var channel = aardvark.channels[channelName];

                        if (channel && channel.onmessage) {
                            channel.onmessage(message.data);
                        }
                    } catch (e) {
                        console.warn("channel onmessage faulted: " + e);
                        debugger;
                    }
                });
            }
        };

        eventSocket.onclose = function () {
            aardvark.processEvent = function () { };
        };

        eventSocket.onerror = function (e) {
            aardvark.processEvent = function () { };
        };

        
    }
}

if (!aardvark.setAttribute) {
    aardvark.setAttribute = function (id, name, value) {
        if (name == "value") {
            id.setAttribute(name, value);
            id.value = value;
        }
        else if (name == "checked") {
            id.setAttribute(name, value);
            id.checked = (value ? true : false);
        }
        else if (name == "selected") {
            id.setAttribute(name, value);
            id.selected = value;
        }
        else {
            id.setAttribute(name, value);
        }
    };
}

$(document).ready(function () {
    // initialize all aardvark-controls 
    $('div.aardvark').each(function () {
        var $div = $(this);
        var div = $div.get(0);
        if (!div.id) div.id = aardvark.newguid();
        aardvark.getRenderer(div.id);
    });
});


if (!aardvark.getCursor) {
    function findAncestor(el, cls) {
        if (el.classList.contains(cls)) return el;
        while ((el = el.parentElement) && !el.classList.contains(cls));
        return el;
    }

    aardvark.getCursor = function (evt,containerClassName) {
        var source = evt.target || evt.srcElement;
        var svg = findAncestor(source, containerClassName);
        if (svg) {
            var pt = svg.createSVGPoint();
            pt.x = evt.clientX;
            pt.y = evt.clientY;
            return pt.matrixTransform(svg.getScreenCTM().inverse());
        } else {
            return { x: NaN, y: NaN };
        }
    };

}

var getCursor = aardvark.getCursor;

if (!aardvark.getRelativeCoords) {

    aardvark.getRelativeCoords = function relativeCoords(event,container) {
        var source = event.target || event.srcElement;
        var containers = document.getElementsByClassName(container);
        if (container && container.length > 0) {
            var container = containers[0];
            var bounds = container.getBoundingClientRect();
            var x = event.clientX - bounds.left;
            var y = event.clientY - bounds.top;
            return { x: x, y: y };
        } else
            return { x: NaN, y: NaN };
    }

}

var getRelativeCoords = aardvark.getRelativeCoords;

if (!aardvark.toFixedV2d) {

    aardvark.toFixedV2d = function toFixedV2d(v) {
        return { X: v.x.toFixed(10), Y: v.y.toFixed(10) };
    }

}

var toFixedV2d = aardvark.toFixedV2d;

if (!aardvark.getRelativePercent) {

    aardvark.getRelativeCoords = function relativeCoords(event, container) {
        var source = event.target || event.srcElement;
        var containers = document.getElementsByClassName(container);
        if (container && container.length > 0) {
            var container = containers[0];
            var bounds = container.getBoundingClientRect();
            var x = event.clientX - bounds.left;
            var y = event.clientY - bounds.top;
            return { x: x / container.width, y: y / container.height };
        } else
            return { x: NaN, y: NaN };
    }

}

var getRelativePercent = aardvark.getRelativeCoords;

if (!aardvark.gamepad) {
    const gamepad = new Gamepad();

    function send(evtName, player, evt) {
        const el = document.activeElement;
        if (!el) return;

        evtName = evtName + "_" + player;
        const handler = el.getAttribute(evtName);
        if (!handler) return;

        const codeName = evtName + "_code";
        const handlerName = evtName + "_handler";
        if (el[codeName] !== handler) {
            el[codeName] = handler;
            var f = new Function("event", handler);
            el[handlerName] = f.bind(el);
        }
        el[handlerName](evt);
    }

    gamepad.on('connect', e => {
        send("gp_connect", e.index, e);
    });

    gamepad.on('disconnect', e => {
        send("gp_disconnect", e.index, e);
    });

    for (var i = 0; i < 4; i++) {
        const pn = "gp_press" + i;
        const rn = "gp_release" + i;

        gamepad.on("press", "button_" + (i + 1), function (e) { send(pn, e.player, e); });
        gamepad.on("release", "button_" + (i + 1), function (e) { send(rn, e.player, e); });
    }


    gamepad.on("press", "shoulder_top_left", function (e) { send("gp_press_shoulder_top_left", e.player, e);});
    gamepad.on("release", "shoulder_top_left", function (e) { send("gp_release_shoulder_top_left", e.player, e);});

    gamepad.on("press", "shoulder_top_right", function (e) { send("gp_press_shoulder_top_right", e.player, e); });
    gamepad.on("release", "shoulder_top_right", function (e) { send("gp_release_shoulder_top_right", e.player, e); });

    gamepad.on("press", "d_pad_left", function (e) { send("gp_press_left", e.player, e); });
    gamepad.on("release", "d_pad_left", function (e) { send("gp_release_left", e.player, e); });

    gamepad.on("press", "d_pad_right", function (e) { send("gp_press_right", e.player, e); });
    gamepad.on("release", "d_pad_right", function (e) { send("gp_release_right", e.player, e); });

    gamepad.on("press", "d_pad_up", function (e) { send("gp_press_up", e.player, e); });
    gamepad.on("release", "d_pad_up", function (e) { send("gp_release_up", e.player, e); });

    gamepad.on("press", "d_pad_down", function (e) { send("gp_press_down", e.player, e); });
    gamepad.on("release", "d_pad_down", function (e) { send("gp_release_down", e.player, e); });

    gamepad.on("press", "start", function (e) { send("gp_press_start", e.player, e); });
    gamepad.on("release", "start", function (e) { send("gp_release_start", e.player, e); });

    gamepad.on("press", "select", function (e) { send("gp_press_select", e.player, e); });
    gamepad.on("release", "select", function (e) { send("gp_release_select", e.player, e); });

    gamepad.on("press", "vendor", function (e) { send("gp_press_home", e.player, e); });
    gamepad.on("release", "vendor", function (e) { send("gp_release_home", e.player, e); });

    gamepad.on("press", "stick_button_left", function (e) { send("gp_press_leftstick", e.player, e); });
    gamepad.on("release", "stick_button_left", function (e) { send("gp_release_leftstick", e.player, e); });

    gamepad.on("press", "stick_button_right", function (e) { send("gp_press_rightstick", e.player, e); });
    gamepad.on("release", "stick_button_right", function (e) { send("gp_release_rightstick", e.player, e); });

    var lx = -1.0;
    var ly = -1.0;
    gamepad.on('hold', 'stick_axis_left', function (e) { if (lx != e.value[0] || ly != e.value[1]) { lx = e.value[0]; ly = e.value[1]; send("gp_leftstick_changed", e.player, { X: e.value[0], Y: e.value[1] }); } });
    gamepad.on('release', 'stick_axis_left', function (e) { send("gp_leftstick_changed", e.player, { X: 0, Y: 0 }); });

    var rx = -1.0;
    var ry = -1.0;
    gamepad.on('hold', 'stick_axis_right', function (e) { if (rx != e.value[0] || ry != e.value[1]) { rx = e.value[0]; ry = e.value[1]; send("gp_rightstick_changed", e.player, { X: e.value[0], Y: e.value[1] }); } });
    gamepad.on('release', 'stick_axis_right', function (e) { send("gp_rightstick_changed", e.player, { X: 0, Y: 0 }); });

    var oldl = -1.0;
    gamepad.on('hold', 'shoulder_bottom_left', function (e) { if (oldl != e.value) { oldl = e.value; send("gp_leftshoulder_changed", e.player, e.value); } });
    gamepad.on('release', 'shoulder_bottom_left', function (e) { if (oldl != 0.0) { oldl = 0.0; send("gp_leftshoulder_changed", e.player, 0); } });


    var oldr = -1.0;
    gamepad.on('hold', 'shoulder_bottom_right', function (e) { if (oldr != e.value) { oldr = e.value; send("gp_rightshoulder_changed", e.player, e.value); } });
    gamepad.on('release', 'shoulder_bottom_right', function (e) { if (oldr != 0.0) { oldr = 0.0; send("gp_rightshoulder_changed", e.player, 0); } });
}
