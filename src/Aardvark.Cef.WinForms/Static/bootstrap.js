var aardvark = {};
var wsPort = "8888";

// rendering related

function initRenderTargetEvents(canvas, id) {
    canvas.isRendering = false;
    canvas.contentEditable = true;
    var $canvas = $(canvas);

    $canvas.click(function (e) {
        $canvas.focus();
        aardvark.processEvent(id, 'click', e.clientX, e.clientY, e.button);
        e.preventDefault();
    });

    $canvas.dblclick(function (e) {
        $canvas.focus();
        aardvark.processEvent(id, 'dblclick', e.clientX, e.clientY, e.button);
        e.preventDefault();
    });

    $canvas.mousedown(function (e) {
        $canvas.focus();
        aardvark.processEvent(id, 'mousedown', e.clientX, e.clientY, e.button);
        e.preventDefault();
    });

    $canvas.mouseup(function (e) {
        aardvark.processEvent(id, 'mouseup', e.clientX, e.clientY, e.button);
        e.preventDefault();
    });

    $canvas.mousemove(function (e) {
        aardvark.processEvent(id, 'mousemove', e.clientX, e.clientY);
        e.preventDefault();
    });

    $canvas.mouseenter(function (e) {
        aardvark.processEvent(id, 'mouseenter', e.clientX, e.clientY);
        e.preventDefault();
    });

    $canvas.mouseout(function (e) {
        aardvark.processEvent(id, 'mouseout', e.clientX, e.clientY);
        e.preventDefault();
    });

    $canvas.bind('mousewheel', function (e) {
        if (document.activeElement == canvas) {
            var delta = e.originalEvent.wheelDelta;
            aardvark.processEvent(id, 'mousewheel', delta);
            e.originalEvent.preventDefault();
        }
    });

    $canvas.keydown(function (e) {
        aardvark.processEvent(id, 'keydown', e.keyCode);
        e.preventDefault();
    });

    $canvas.keyup(function (e) {
        aardvark.processEvent(id, 'keyup', e.keyCode);
        e.preventDefault();
    });

    $canvas.keypress(function (e) {
        aardvark.processEvent(id, 'keypress', e.key);
        e.preventDefault();
    });

    canvas.oncontextmenu = function (e) {
        e.preventDefault();
    };

}

function getRenderTarget(id) {
    var $img = $('#' + id + ' img')
    var img = $img.get(0);
    if (img.renderTarget)
        return img.renderTarget;

    initRenderTargetEvents(img, id);

    var $div = $('#' + id)
    var width =
        function () {
            return $div.width();
        };

    var height =
        function () {
            return $div.height();
        };

    var socket = new WebSocket("ws://" + host + ":" + wsPort + "/render/" + id + "/" + sessionId);
    socket.binaryType = "blob"
    var lastHeight = 1;
    var lastWidth = 1;
    img.style.transform = "scale(1,-1)";
    var urlCreator = window.URL || window.webkitURL;

    var blit =
        function (data) {
            //var w = lastWidth;
            //var h = lastHeight;
            //if (img.style.width != w || img.style.height != h) {
            //    img.style.width = w;
            //    img.style.height = h;
            //}


            var imageUrl = urlCreator.createObjectURL(data);
            img.src = imageUrl;

            console.log("size: " + img.width + "x" + img.height);


            //var byteArray = new Uint8ClampedArray(data);
            //var imageData = new ImageData(byteArray, w, h);
            //ctx.putImageData(imageData, 0, 0);
        };


    socket.onmessage =
        function (m) {
            if (m.data instanceof Blob) {
                socket.send("g");
                blit(m.data);
                socket.send("d");
            }
        };

    var requestRenderOnOpen = false;
    var requestRender =
        function () {
            if (socket.readyState != 1) {
                requestRenderOnOpen = true;
            }
            else {
                lastWidth = width();
                lastHeight = height();
                socket.send("r" + JSON.stringify({ X: lastWidth, Y: lastHeight }));
            }
        };

    socket.onopen =
        function () {
            if (requestRenderOnOpen) {
                requestRender();
            }
        };


    var renderTarget =
        {
            id: id,
            width: width,
            height: height,
            render: requestRender
        };

    img.renderTarget = renderTarget;
    return renderTarget;
}

function render(id) {
    var target = getRenderTarget(id);
    target.render();
}

function initDocument() {
    $(document).ready(function () {
        $('div.aardvark').each(function () {
            var $div = $(this);
            var id = $div.get(0).id;
            $div.append($('<img/>'));

            // render on resize
            $div.resize(function () {
                render(id);
            });

            // render once
            render(id);
        });
    });
}

// event related things (WebSocket)
var host = window.location.hostname;
var eventSocket = new WebSocket("ws://" + host + ":" + wsPort + "/events")
var sessionId = "noid";

aardvark.processEvent = function () {
    console.warn("websocket not opened yet");
}

eventSocket.onopen = function () {
    aardvark.processEvent = function () {
        var sender = arguments[0];
        var name = arguments[1];
        var args = [];
        for (var i = 2; i < arguments.length; i++) {
            args.push(JSON.stringify(arguments[i]));
        }
        var message = "e" + JSON.stringify({ sender: sender, name: name, args: args });
        eventSocket.send(message);
    }
};

eventSocket.onmessage = function (m) {
    var o = JSON.parse(m.data);

    if (o.kind == "sessionid") {
        sessionId = o.payload;
        console.log(sessionId);
        initDocument();
    }

    else if (o.kind == "eval") {
        try {
            var res = eval(o.payload);
            if (o.id >= 0) {
                var payload = null;
                if (res != undefined) payload = JSON.stringify(res);

                eventSocket.send("m" + JSON.stringify({ id: o.id, kind: "value", payload: payload }));
            }
        }
        catch (e) {
            if (o.id >= 0) eventSocket.send("m" + JSON.stringify({ id: o.id, kind: "exception", payload: JSON.stringify(e) }));
        }
    }
};





