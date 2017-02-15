var webSocketUrl = "ws://" + window.location.hostname + ":" + window.location.port;
var urlCreator = window.URL || window.webkitURL;


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
        processEvent(id, 'click', e.clientX, e.clientY, e.button);
        e.preventDefault();
    });

    $canvas.dblclick(function (e) {
        $canvas.focus();
        processEvent(id, 'dblclick', e.clientX, e.clientY, e.button);
        e.preventDefault();
    });

    $canvas.mousedown(function (e) {
        $canvas.focus();
        processEvent(id, 'mousedown', e.clientX, e.clientY, e.button);
        e.preventDefault();
    });

    $canvas.mouseup(function (e) {
        processEvent(id, 'mouseup', e.clientX, e.clientY, e.button);
        e.preventDefault();
    });

    $canvas.mousemove(function (e) {
        processEvent(id, 'mousemove', e.clientX, e.clientY);
        e.preventDefault();
    });

    $canvas.mouseenter(function (e) {
        processEvent(id, 'mouseenter', e.clientX, e.clientY);
        e.preventDefault();
    });

    $canvas.mouseout(function (e) {
        processEvent(id, 'mouseout', e.clientX, e.clientY);
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