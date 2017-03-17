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

function initRenderTargetEvents(eventSocket, canvas, id) {
    canvas.isRendering = false;
    canvas.contentEditable = true;
    var $canvas = $(canvas);

    var processEvent =
        function () {
            var sender = arguments[0];
            var name = arguments[1];
            var args = [];
            for (var i = 2; i < arguments.length; i++) {
                args.push(JSON.stringify(arguments[i]));
            }
            var message = JSON.stringify({ Case: "Event", evt: { sender: sender, name: name, args: args } });
            eventSocket.send(message);
        };

    $canvas.click(function (e) {
        $canvas.focus();
        processEvent(id, 'click', e.offsetX, e.offsetY, e.button);
        e.preventDefault();
    });

    $canvas.dblclick(function (e) {
        $canvas.focus();
        processEvent(id, 'dblclick', e.offsetX, e.offsetY, e.button);
        e.preventDefault();
    });

    $canvas.mousedown(function (e) {
        $canvas.focus();
        processEvent(id, 'mousedown', e.offsetX, e.offsetY, e.button);
        e.preventDefault();
    });

    $canvas.mouseup(function (e) {
        processEvent(id, 'mouseup', e.offsetX, e.offsetY, e.button);
        e.preventDefault();
    });

    $canvas.mousemove(function (e) {
        console.log(e);
        processEvent(id, 'mousemove', e.offsetX, e.offsetY);
        e.preventDefault();
    });

    $canvas.mouseenter(function (e) {
        processEvent(id, 'mouseenter', e.offsetX, e.offsetY);
        e.preventDefault();
    });

    $canvas.mouseout(function (e) {
        processEvent(id, 'mouseout', e.offsetX, e.offsetY);
        e.preventDefault();
    });

    $canvas.bind('mousewheel', function (e) {
        if (document.activeElement === canvas) {
            var delta = e.originalEvent.wheelDelta;
            processEvent(id, 'mousewheel', delta);
            e.originalEvent.preventDefault();
        }
    });

    $canvas.keydown(function (e) {
        processEvent(id, 'keydown', e.keyCode);
        e.preventDefault();
    });

    $canvas.keyup(function (e) {
        processEvent(id, 'keyup', e.keyCode);
        e.preventDefault();
    });

    $canvas.keypress(function (e) {
        processEvent(id, 'keypress', e.key);
        e.preventDefault();
    });

    canvas.oncontextmenu = function (e) {
        e.preventDefault();
    };

}

function getRenderFunction(id) {
    var $div = $('#' + id);
    var div = $div.get(0);
    if (div.renderFunction)
        return div.renderFunction;

    $div.append($('<img class="rendercontrol"/>'));
    var $img = $('#' + id + ' img');
    var img = $img.get(0);

    var socket = new WebSocket(webSocketUrl + "/render/" + id);
    socket.binaryType = "blob";
    //img.style.transform = "scale(1,-1)";
    img.style.cursor = "default";
    //img.style.focus.outline = "none";

    var oldTime = window.performance.now();
    var frameCounter = 0;

    var blit =
        function (data) {
            img.src = urlCreator.createObjectURL(data);

            /*if (frameCounter > 30) {
                var now = window.performance.now();
                var dt = (now - oldTime) / 1000.0;
                var fps = frameCounter / dt;
                $('#framerate').html(fps);
                oldTime = window.performance.now();
                frameCounter = 0;
            }
            else {
                frameCounter++;
            }*/

            socket.send(JSON.stringify({ Case: "Rendered" }));


        };

    var socketOpen = false;

    var requestRender =
        function () {
            var w = $div.width();
            var h = $div.height();
            socket.send(JSON.stringify({ Case: "RequestImage", size: { X: Math.round(w), Y: Math.round(h) } }));
        };

    socket.onopen =
        function () {
            socketOpen = true;
            requestRender();

            initRenderTargetEvents(socket, img, id);

            $div.resize(function () {
                requestRender();
            });

        };

    socket.onmessage =
        function (m) {
            if (m.data instanceof Blob) {
                blit(m.data);
            }
            else {
                var msg = JSON.parse(m.data);
                if (msg.Case === "Invalidate") {
                    // TODO: what if not visible??
                    requestRender();
                }
                else {
                    console.warn("unexpected message " + msg);
                }
            }
        };

    div.renderFunction = requestRender;
    return requestRender;
}

function render(id) {
    var r = getRenderFunction(id);
    r();
}

$(document).ready(function () {

    $("head").append($("<style type='text/css'>img.rendercontrol:focus { outline: none; }</style>"));


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
            getRenderFunction(div.id);

        });

        setTimeout(checkDOMChange, 100);
    }

    checkDOMChange();

});

