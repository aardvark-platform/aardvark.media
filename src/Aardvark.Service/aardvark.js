var webSocketUrl = "ws://" + window.location.hostname + ":" + window.location.port;
var urlCreator = window.URL || window.webkitURL;


var aardvark = {};
aardvark.channels = {};
aardvark.referencedScripts = {};
aardvark.referencedScripts["jquery"] = true;
aardvark.referencedStyles = {};

aardvark.processEvent = function () {
    console.warn("websocket not opened yet");
};

class Renderer {

    constructor(id, samples) {
        this.id = id;
        this.div = document.getElementById(id);

        if (!samples) this.samples = 1;
        else this.samples = samples;

        this.buffer = [];
        this.isOpen = false;
        this.isClosed = false;

        this.init();
    }

    init() {
        var img = document.createElement("img");
        img.setAttribute("class", "rendercontrol");
        this.div.appendChild(img);
        this.img = img;


        this.div.contentEditable = true;
        
        img.style.cursor = "default";

        var socket = new WebSocket(webSocketUrl + "/render/" + this.id + "?samples=" + this.samples);
        socket.binaryType = "blob";
        this.socket = socket;


        var self = this;
        this.img.onclick = function () { self.div.focus(); };
        
        socket.onopen = function () {
            for (var i = 0; i < self.buffer.length; i++) {
                socket.send(self.buffer[i]);
            }
            self.isOpen = true;
            self.buffer = [];

            self.render();

        };

        socket.onmessage = function (msg) {
            self.received(msg);
        };

        socket.onclose = function () {
            self.isOpen = false;
            self.isClosed = true;
        };

        socket.onerror = function (err) {
            console.warn(err);
            self.isClosed = true;
        };

        this.img.oncontextmenu = function (e) { e.preventDefault(); };

        $(this.div).resize(function () {
            self.render();
        });

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
        var message = JSON.stringify({ Case: "Event", evt: { sender: sender, name: name, args: args } });
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


    received(msg) {
        if (msg.data instanceof Blob) {
            this.img.src = urlCreator.createObjectURL(msg.data);
            this.send(JSON.stringify({ Case: "Rendered" }));
        }
        else {
            var o = JSON.parse(msg.data);

            //type Command =
            //    | Invalidate
            //    | Subscribe of eventName : string
            //    | Unsubscribe of eventName : string

            if (o.Case === "Invalidate") {
                // TODO: what if not visible??
                this.render();
            }
            else if (o.Case === "Subscribe") {
                var evt = o.eventName;
                this.subscribe(evt);
            }
            else if (o.Case === "Unsubscribe") {
                var evt = o.eventName;
                this.unsubscribe(evt);
            }
            else {
                console.warn("unexpected message " + o);
            }
        }
    }

    render() {
        var rect = this.div.getBoundingClientRect();
        this.send(JSON.stringify({ Case: "RequestImage", size: { X: Math.round(rect.width), Y: Math.round(rect.height) } }));
    }

}

aardvark.addReferences = function (refs, cont) {
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
                    console.log("script " + name + " (" + url + ")");
                    aardvark.referencedScripts[name] = true;
                    return function () {
                        var script = document.createElement("script");
                        script.setAttribute("src", url);
                        script.onload = acc(i + 1);
                        document.head.appendChild(script);
                    };
                }
                else return acc(i + 1);
            }
            else {
                if (!aardvark.referencedStyles[name]) {
                    console.log("style " + name + " (" + url + ")");
                    aardvark.referencedStyles[name] = true;
                    return function () {
                        var script = document.createElement("link");
                        script.setAttribute("rel", "stylesheet");
                        script.setAttribute("href", url);
                        script.onload = acc(i + 1);
                        document.head.appendChild(script);
                    };
                }
                else return acc(i + 1);
            }

        }
    }

    var real = acc(0);
    real();
    //for (key in refs) {
    //    var ref = refs[key];
    //    var kind = ref.kind;
    //    var name = ref.name;
    //    var url = ref.url;
    //    var old = cc;
    //    if (kind === "script") {
    //        if (!aardvark.referencedScripts[name]) {
    //            console.log("referencing " + name + " (" + url + ")");
    //            aardvark.referencedScripts[name] = true;
    //            cc = function () {
    //                var script = document.createElement("script");
    //                script.setAttribute("src", url);
    //                script.onload = old;
    //                document.head.appendChild(script);
    //            };
    //        }
    //    }
    //    else {
    //        if (!aardvark.referencedStyles[name]) {
    //            console.log("referencing " + name + " (" + url + ")");
    //            aardvark.referencedStyles[name] = true;
    //            cc = function () {
    //                var script = document.createElement("link");
    //                script.setAttribute("rel", "stylesheet");
    //                script.setAttribute("href", url);
    //                script.onload = old;
    //                document.head.appendChild(script);
    //            };
    //        }
    //    }
    //}
    //cc();

};

class Channel {

    constructor(name) {
        this.name = name;
        this.pending = undefined;
        this._recv = undefined;
    }


    received(data) {
        if (data === "commit-suicide") {
            console.warn(this.name + " couldn't take it no more and committed suicide")
            delete aardvark.channels[name];
        }
        else {
            if (this._recv) {
                this._recv(data);
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


// rendering related
aardvark.getRenderer = function (id) {
    var div = document.getElementById(id);
    if (!div.renderer) {
        var renderer = new Renderer(id, 8);
        div.renderer = renderer;
    }

    return div.renderer;
}

aardvark.render = function (id) {
    var r = aardvark.getRenderer(id);
    r.render();
}

$(document).ready(function () {

    $("head").append($("<style type='text/css'>img.rendercontrol:focus { outline: none; } div.aardvark { background-color: black } </style>"));


    function getUrl(proto, subpath) {
        var l = window.location;
        var path = l.pathname;
        if (l.port === "") {
            return proto + l.hostname + path + subpath;
        }
        else {
            return proto + l.hostname + ':' + l.port + path + subpath;
        }
    }
    var url = getUrl('ws://', 'events');
    var eventSocket = new WebSocket(url);

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
        }
    };

    eventSocket.onmessage = function (m) {
        var c = m.data.substring(0, 1);
        var data = m.data.substring(1, m.data.length);
        if (c === "x") {
            eval("{\r\n" + data + "\r\n}");
        }
        else {
            var message = JSON.parse(data);
            var channelName = message.targetId + "_" + message.channel;
            var channel = aardvark.channels[channelName];

            if (channel && channel.onmessage) {
                channel.onmessage(message.data);
            }
        }
    };

    function checkDOMChange() {
        $('div.aardvark').each(function () {
            var $div = $(this);
            var div = $div.get(0);
            aardvark.getRenderer(div.id);

        });

        setTimeout(checkDOMChange, 100);
    }

    checkDOMChange();

});

