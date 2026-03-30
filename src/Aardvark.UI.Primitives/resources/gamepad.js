(function () {
    "use strict";
    let _requestAnimationFrame, _cancelAnimationFrame;
    const hasGamepadSupport = window.navigator.getGamepads !== undefined;
    if (String(typeof window) !== "undefined") {
        ["webkit", "moz"].forEach(function (key) {
            _requestAnimationFrame =
                _requestAnimationFrame || window.requestAnimationFrame || window[key + "RequestAnimationFrame"] || null;
            _cancelAnimationFrame =
                _cancelAnimationFrame || window.cancelAnimationFrame || window[key + "CancelAnimationFrame"] || null;
        });
    }
    function findKeyMapping(index, mapping) {
        const results = [];
        Object.keys(mapping).forEach(function (key) {
            if (mapping[key] === index) {
                results.push(key);
            } else if (Array.isArray(mapping[key]) && mapping[key].indexOf(index) !== -1) {
                results.push(key);
            }
        });
        return results;
    }
    function Gamepad() {
        this._events = { gamepad: [], axes: [], keyboard: {} };
        this._handlers = { gamepad: { connect: null, disconnect: null } };
        this._keyMapping = {
            gamepad: {
                button_1: 0,
                button_2: 1,
                button_3: 2,
                button_4: 3,
                shoulder_top_left: 4,
                shoulder_top_right: 5,
                shoulder_bottom_left: 6,
                shoulder_bottom_right: 7,
                select: 8,
                start: 9,
                stick_button_left: 10,
                stick_button_right: 11,
                d_pad_up: 12,
                d_pad_down: 13,
                d_pad_left: 14,
                d_pad_right: 15,
                vendor: 16,
            },
            axes: { stick_axis_left: [0, 2], stick_axis_right: [2, 4] },
            keyboard: {
                button_1: 32,
                start: 27,
                d_pad_up: [38, 87],
                d_pad_down: [40, 83],
                d_pad_left: [37, 65],
                d_pad_right: [39, 68],
            },
        };
        this._threshold = 0.3;
        this._listeners = [];
        this._handleKeyboardEventListener = this._handleKeyboardEventListener.bind(this);
        this.resume();
    }
    Gamepad.prototype._handleGamepadConnected = function (index) {
        if (this._handlers.gamepad.connect) {
            this._handlers.gamepad.connect({ index: index });
        }
    };
    Gamepad.prototype._handleGamepadDisconnected = function (index) {
        if (this._handlers.gamepad.disconnect) {
            this._handlers.gamepad.disconnect({ index: index });
        }
    };
    Gamepad.prototype._handleGamepadEventListener = function (controller) {
        const self = this;
        if (controller && controller.connected) {
            controller.buttons.forEach(function (button, index) {
                const keys = findKeyMapping(index, self._keyMapping.gamepad);
                if (keys) {
                    keys.forEach(function (key) {
                        if (button.pressed) {
                            if (!self._events.gamepad[controller.index][key]) {
                                self._events.gamepad[controller.index][key] = {
                                    pressed: true,
                                    hold: false,
                                    released: false,
                                    player: controller.index,
                                };
                            }
                            self._events.gamepad[controller.index][key].value = button.value;
                        } else if (!button.pressed && self._events.gamepad[controller.index][key]) {
                            self._events.gamepad[controller.index][key].released = true;
                            self._events.gamepad[controller.index][key].hold = false;
                        }
                    });
                }
            });
        }
    };
    Gamepad.prototype._handleGamepadAxisEventListener = function (controller) {
        const self = this;
        if (controller && controller.connected) {
            Object.keys(self._keyMapping.axes).forEach(function (key) {
                const axes = Array.prototype.slice.apply(controller.axes, self._keyMapping.axes[key]);
                if (Math.abs(axes[0]) > self._threshold || Math.abs(axes[1]) > self._threshold) {
                    self._events.axes[controller.index][key] = {
                        pressed: !self._events.axes[controller.index][key],
                        hold: !!self._events.axes[controller.index][key],
                        released: false,
                        value: axes,
                    };
                } else if (self._events.axes[controller.index][key]) {
                    self._events.axes[controller.index][key] = {
                        pressed: false,
                        hold: false,
                        released: true,
                        value: axes,
                    };
                }
            });
        }
    };
    Gamepad.prototype._handleKeyboardEventListener = function (e) {
        const self = this;
        const keys = findKeyMapping(e.keyCode, self._keyMapping.keyboard);
        if (keys) {
            keys.forEach(function (key) {
                if (e.type === "keydown" && !self._events.keyboard[key]) {
                    self._events.keyboard[key] = { pressed: true, hold: false, released: false };
                } else if (e.type === "keyup" && self._events.keyboard[key]) {
                    self._events.keyboard[key].released = true;
                    self._events.keyboard[key].hold = false;
                }
            });
        }
    };
    Gamepad.prototype._handleEvent = function (key, events, player) {
        if (events[key].pressed) {
            this.trigger("press", key, events[key].value, player);
            events[key].pressed = false;
            events[key].hold = true;
        } else if (events[key].hold) {
            this.trigger("hold", key, events[key].value, player);
        } else if (events[key].released) {
            this.trigger("release", key, events[key].value, player);
            delete events[key];
        }
    };
    Gamepad.prototype._loop = function () {
        const self = this;
        const gamepads = hasGamepadSupport ? window.navigator.getGamepads() : false;
        const length = 4;
        if (gamepads) {
            for (let i = 0; i < length; i = i + 1) {
                if (gamepads[i]) {
                    if (!self._events.gamepad[i]) {
                        self._handleGamepadConnected(i);
                        self._events.gamepad[i] = {};
                        self._events.axes[i] = {};
                    }
                    self._handleGamepadEventListener(gamepads[i]);
                    self._handleGamepadAxisEventListener(gamepads[i]);
                } else if (self._events.gamepad[i]) {
                    self._handleGamepadDisconnected(i);
                    self._events.gamepad[i] = null;
                    self._events.axes[i] = null;
                }
            }
            self._events.gamepad.forEach(function (gamepad, player) {
                if (gamepad) {
                    Object.keys(gamepad).forEach(function (key) {
                        self._handleEvent(key, gamepad, player);
                    });
                }
            });
            self._events.axes.forEach(function (gamepad, player) {
                if (gamepad) {
                    Object.keys(gamepad).forEach(function (key) {
                        self._handleEvent(key, gamepad, player);
                    });
                }
            });
        }
        Object.keys(self._events.keyboard).forEach(function (key) {
            self._handleEvent(key, self._events.keyboard, "keyboard");
        });
        if (self._requestAnimation) {
            self._requestAnimation = _requestAnimationFrame(self._loop.bind(self));
        }
    };
    Gamepad.prototype.on = function (type, button, callback, options) {
        const self = this;
        if (Object.keys(this._handlers.gamepad).indexOf(type) !== -1 && typeof button === "function") {
            this._handlers.gamepad[type] = button;
            this._events.gamepad = [];
        } else {
            if (typeof type === "string" && type.match(/\s+/)) {
                type = type.split(/\s+/g);
            }
            if (typeof button === "string" && button.match(/\s+/)) {
                button = button.split(/\s+/g);
            }
            if (Array.isArray(type)) {
                type.forEach(function (type) {
                    self.on(type, button, callback, options);
                });
            } else if (Array.isArray(button)) {
                button.forEach(function (button) {
                    self.on(type, button, callback, options);
                });
            } else {
                this._listeners.push({ type: type, button: button, callback: callback, options: options });
            }
        }
    };
    Gamepad.prototype.off = function (type, button) {
        const self = this;
        if (typeof type === "string" && type.match(/\s+/)) {
            type = type.split(/\s+/g);
        }
        if (typeof button === "string" && button.match(/\s+/)) {
            button = button.split(/\s+/g);
        }
        if (Array.isArray(type)) {
            type.forEach(function (type) {
                self.off(type, button);
            });
        } else if (Array.isArray(button)) {
            button.forEach(function (button) {
                self.off(type, button);
            });
        } else {
            this._listeners = this._listeners.filter(function (listener) {
                return listener.type !== type && listener.button !== button;
            });
        }
    };
    Gamepad.prototype.setCustomMapping = function (device, config) {
        if (this._keyMapping[device] !== undefined) {
            this._keyMapping[device] = config;
        } else {
            throw new Error('The device "' + device + '" is not supported through gamepad.js');
        }
    };
    Gamepad.prototype.setGlobalThreshold = function (num) {
        this._threshold = parseFloat(num);
    };
    Gamepad.prototype.trigger = function (type, button, value, player) {
        if (this._listeners) {
            this._listeners.forEach(function (listener) {
                if (listener.type === type && listener.button === button) {
                    listener.callback({
                        type: listener.type,
                        button: listener.button,
                        value: value,
                        player: player,
                        event: listener,
                        timestamp: Date.now(),
                    });
                }
            });
        }
    };
    Gamepad.prototype.pause = function () {
        _cancelAnimationFrame(this._requestAnimation);
        this._requestAnimation = null;
        document.removeEventListener("keydown", this._handleKeyboardEventListener);
        document.removeEventListener("keyup", this._handleKeyboardEventListener);
    };
    Gamepad.prototype.resume = function () {
        this._requestAnimation = _requestAnimationFrame(this._loop.bind(this));
        document.addEventListener("keydown", this._handleKeyboardEventListener);
        document.addEventListener("keyup", this._handleKeyboardEventListener);
    };
    Gamepad.prototype.destroy = function () {
        this.pause();
        delete this._listeners;
    };
    if (typeof define === "function" && define.amd !== undefined) {
        define([], function () {
            return Gamepad;
        });
    } else if (typeof module === "object" && module.exports !== undefined) {
        module.exports = Gamepad;
    } else {
        window.Gamepad = Gamepad;
    }
})();

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
            const f = new Function("event", handler);
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

    for (let i = 0; i < 4; i++) {
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

    let lx = -1.0;
    let ly = -1.0;
    gamepad.on('hold', 'stick_axis_left', function (e) { if (lx !== e.value[0] || ly !== e.value[1]) { lx = e.value[0]; ly = e.value[1]; send("gp_leftstick_changed", e.player, { X: e.value[0], Y: e.value[1] }); } });
    gamepad.on('release', 'stick_axis_left', function (e) { send("gp_leftstick_changed", e.player, { X: 0, Y: 0 }); });

    let rx = -1.0;
    let ry = -1.0;
    gamepad.on('hold', 'stick_axis_right', function (e) { if (rx !== e.value[0] || ry !== e.value[1]) { rx = e.value[0]; ry = e.value[1]; send("gp_rightstick_changed", e.player, { X: e.value[0], Y: e.value[1] }); } });
    gamepad.on('release', 'stick_axis_right', function (e) { send("gp_rightstick_changed", e.player, { X: 0, Y: 0 }); });

    let oldl = -1.0;
    gamepad.on('hold', 'shoulder_bottom_left', function (e) { if (oldl !== e.value) { oldl = e.value; send("gp_leftshoulder_changed", e.player, e.value); } });
    gamepad.on('release', 'shoulder_bottom_left', function (e) { if (oldl !== 0.0) { oldl = 0.0; send("gp_leftshoulder_changed", e.player, 0); } });

    let oldr = -1.0;
    gamepad.on('hold', 'shoulder_bottom_right', function (e) { if (oldr !== e.value) { oldr = e.value; send("gp_rightshoulder_changed", e.player, e.value); } });
    gamepad.on('release', 'shoulder_bottom_right', function (e) { if (oldr !== 0.0) { oldr = 0.0; send("gp_rightshoulder_changed", e.player, 0); } });
}