
function initCanvasEvents($canvas, id) {
    var canvas = $canvas.get(0);

    canvas.isRendering = false;
    canvas.contentEditable = true;

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
    var $div = $('#' + id);
    var div = $div.get(0);
    if (div.renderTarget)
        return div.renderTarget;

    var $canvas = $('#' + id + ' canvas');
    var canvas = $canvas.get(0);
    initCanvasEvents($canvas, id);

    var width = 
        function() { 
            var w = $div.width();
            return w;
        };

    var height = 
        function() { 
            var h = $div.height(); 
            return h;
        };

    var renderTarget =
        {
            div: div,
            canvas: canvas,
            width: width,
            height: height
        };

    div.renderTarget = renderTarget;

    return renderTarget;
}

function getGLBlitFunction(target) {
    if (target.blit)
        return target.blit;

    var canvas = target.canvas;
    var gl = canvas.getContext('webgl');
    canvas.style.removeProperty("transform");

    var positions =
        [
            -1, -1, 0,
             1, -1, 0,
            -1, 1, 0,
             1, 1, 0
        ]

    var vb = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, vb);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(positions), gl.STATIC_DRAW);
    gl.bindBuffer(gl.ARRAY_BUFFER, null);

    var vsc =
        "attribute vec3 pos;\n" +
        "varying vec2 tc;\n" +
        "void main(void) {\n" +
        "    gl_Position = vec4(pos, 1.0);\n" +
        "    tc = vec2(0.5 * pos.x + 0.5, 0.5 + 0.5 * pos.y);\n" +
        "}\n";

    var fsc =
        "precision highp float;\n" +
        "varying vec2 tc;\n" +
        "uniform sampler2D tex;\n" +
        "void main(void) {\n" +
        "    gl_FragColor = texture2D(tex, tc, 0.0);\n" +
        "}\n";

    var vs = gl.createShader(gl.VERTEX_SHADER);
    gl.shaderSource(vs, vsc);
    gl.compileShader(vs);
    var log = gl.getShaderInfoLog(vs);

    var fs = gl.createShader(gl.FRAGMENT_SHADER);
    gl.shaderSource(fs, fsc);
    gl.compileShader(fs);
    var log = gl.getShaderInfoLog(fs);

    var prog = gl.createProgram();
    gl.attachShader(prog, vs);
    gl.attachShader(prog, fs);
    gl.linkProgram(prog);
    gl.useProgram(prog);

    var tex = gl.createTexture()

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.bindTexture(gl.TEXTURE_2D, null);

    var ltex = gl.getUniformLocation(prog, "tex");
    gl.uniform1i(ltex, 0);
    gl.bindBuffer(gl.ARRAY_BUFFER, vb);
    var lpos = gl.getAttribLocation(prog, "pos");
    gl.vertexAttribPointer(lpos, 3, gl.FLOAT, false, 0, 0);
    gl.enableVertexAttribArray(lpos);
    gl.clearColor(0.0, 0.0, 0.0, 0.0);
    gl.clear(gl.COLOR_BUFFER_BIT);
    gl.disable(gl.DEPTH_TEST);

    var blit =
        function (data, w, h) {
            var bufferData = new Uint8Array(data);
            gl.activeTexture(gl.TEXTURE0);
            gl.bindTexture(gl.TEXTURE_2D, tex);
            if (canvas.width != w || canvas.height != h) {
                canvas.width = w;
                canvas.height = h;
                gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, bufferData);
            }
            else {
                gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, bufferData);
            }


            gl.viewport(0, 0, w, h);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindTexture(gl.TEXTURE_2D, null);
        };

    target.blit = blit;
    return blit;
}

function getBlitFunction(target) {
    if (target.blit)
        return target.blit;

    var canvas = target.canvas;
    var ctx = canvas.getContext('2d');
    canvas.style.transform = "scale(1,-1)";
    var blit =
        function (data, w, h) {
            if (canvas.width != w || canvas.height != h) {
                canvas.width = w;
                canvas.height = h;
            }

            var byteArray = new Uint8ClampedArray(data);
            var imageData = new ImageData(byteArray, w, h);
            ctx.putImageData(imageData, 0, 0);
        };

    target.blit = blit;
    return blit;
}

function renderOld(id) {
    var target = getRenderTarget(id);

    var blit = getBlitFunction(target);
    //var blit = getGLBlitFunction(target);

    var w = target.width();
    var h = target.height();

    var request = new XMLHttpRequest();
    request.open("GET", "http://aardvark.local/render/" + id + "?w=" + w + "&h=" + h, true);
    request.responseType = "arraybuffer";

    request.onload =
        function (oEvent) {
            try {
                var arrayBuffer = request.response;
                aardvark.processEvent(id, 'received');
                if (arrayBuffer) {
                    blit(arrayBuffer, w, h);
                }
            }
            finally {
                aardvark.processEvent(id, 'rendered');
            }
        };

    request.onerror =
        function () {
            aardvark.processEvent(id, 'rendered');
        };

    request.send(null);
}

function render(id) {
    var target = getRenderTarget(id);
    var blit = getBlitFunction(target);
    //var blit = getGLBlitFunction(target);

    var w = target.width();
    var h = target.height();
    var url = "http://aardvark.local/render/" + id + "?w=" + w + "&h=" + h;


    fetch(url).then(function (res) {
        res.arrayBuffer().then(function (data) {
            try {
                aardvark.processEvent(id, 'received');
                blit(data, w, h);
            }
            finally {
                aardvark.processEvent(id, 'rendered');
            }
        });
    });
}


function invalidate(id) {
    render(id);
}

function testFS() {
    localStorage.setItem("lastname", "Smith");
    console.log(localStorage.getItem("lastname"));
}

$(document).ready(function () {
    $('div.aardvark').each(function () {
        var $div = $(this);
        var id = $div.get(0).id;
        $div.append($('<canvas/>'));

        // render on resize
        $div.resize(function () {
            render(id);
        });

        // render once
        render(id);
    });
});