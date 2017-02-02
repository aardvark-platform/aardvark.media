namespace Aardvark.Cef.WinForms


type Content =
    | Css of string
    | Javascript of string
    | Html of string
    | Binary of byte[]
    | Error


module Bootstrap =

    let style (u : Map<string, string>) =
        Css """

        body {
            width: 100%;
            height: 100%;
            margin: 0px;
            padding: 0px;
            border: 0px;
        }

        div.aardvark {
            
        }

        canvas {
            transform: scale(1,-1);
            cursor: default;
        }

        canvas:focus {
            
            outline: none;
        }

        """

    let boot (u : Map<string, string>) =
        Javascript """

            function initEvents(id) {
                var $canvas = $('#'+ id + ' canvas');
                var canvas = $canvas.get(0);
                if(canvas.hasEventHandlers)
                    return;

                canvas.isRendering = false;
                canvas.contentEditable = true;
                canvas.hasEventHandlers = true;

                var hasFocus = document.activeElement == canvas;

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

                $canvas.bind('mousewheel', function(e){
                    if(document.activeElement == canvas) {
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

                canvas.oncontextmenu = function(e) {
                    e.preventDefault();
                };

            }

            function initGL(id) { 
                initEvents(id);
                var $div = $('#'+ id);
                var w = $div.width();
                var h = $div.height();
	            var canvas = $('#'+ id + ' canvas').get(0);

                if(canvas.gl != undefined)
                    return;

                var gl = canvas.getContext('webgl');
                canvas.gl = gl;

                var positions =
                    [
                        -1, -1, 0,
                         1, -1, 0,
                        -1,  1, 0,
                         1,  1, 0
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

                canvas.texture = tex;

            }

            function renderGL(id, w, h, data) {
                initGL(id);
	            var canvas = $('#'+ id + ' canvas').get(0);

                var gl = canvas.gl;
                var tex = canvas.texture;

                gl.activeTexture(gl.TEXTURE0);
                gl.bindTexture(gl.TEXTURE_2D, tex);
                if(canvas.width != w || canvas.height != h) {
                    canvas.width = w;
                    canvas.height = h;
                    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
                }
                else {
                    gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, data);
                }
                

                gl.viewport(0,0,w,h);
                gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
                gl.bindTexture(gl.TEXTURE_2D, null);
            }

            function renderNew(id) {
                var $div = $('#'+ id);
                var w = $div.width();
                var h = $div.height();
                var div = $div.get(0);

                if(div.isRendering) return;
                div.isRendering = true;

	            var oReq = new XMLHttpRequest();
	            oReq.open("GET", "http://aardvark.local/render/" + id + "?w=" + w + "&h=" + h, true);
	            oReq.responseType = "arraybuffer";

	            oReq.onload = 
		            function (oEvent) {
			            var arrayBuffer = oReq.response;
			            if (arrayBuffer) { 
				            var byteArray = new Uint8Array(arrayBuffer);
                            renderGL(id, w, h, byteArray);
                        }
                        div.isRendering = false;
                        aardvark.processEvent(id, 'rendered');
                    };

                oReq.onerror =
                    function () {
                        div.isRendering = false;
                        aardvark.processEvent(id, 'rendered');
                    };

	            oReq.send(null);
            }
       
            function render(id) {
                initEvents(id);
                var $div = $('#'+ id);
                var w = $div.width();
                var h = $div.height();
                $canvas = $('#'+ id + ' canvas');
	            var canvas = $canvas.get(0);
           

	            var oReq = new XMLHttpRequest();
	            oReq.open("GET", "http://aardvark.local/render/" + id + "?w=" + w + "&h=" + h, true);
	            oReq.responseType = "arraybuffer";

	            oReq.onload = 
		            function (oEvent) {
			            var arrayBuffer = oReq.response;
			            if (arrayBuffer) { 

                            if(canvas.width != w || canvas.height != h) {
                                canvas.width = w;
                                canvas.height = h;
                            }
                            
				            var byteArray = new Uint8ClampedArray(arrayBuffer);
                            var imageData = new ImageData(byteArray, w, h);
	                        var ctx = canvas.getContext('2d');
                            ctx.save();
				            ctx.putImageData(imageData, 0, 0);
                            ctx.restore();
			            }
                        aardvark.processEvent(id, 'rendered');
		            };

                oReq.onerror =
                    function() {
                        aardvark.processEvent(id, 'rendered');
                    };

	            oReq.send(null);
            }

            function invalidate(id) {
                render(id);
            }

            function init(id) {
                var $div = $('#'+ id);
                var w = $div.width();
                var h = $div.height();
                $div.append($('<canvas/>'));

                $div.resize(function () {
                    render(id);
                });

                render(id);
            }


            $(document).ready(function() {
	            $('div.aardvark').each(function() {
                    
                    init($(this).get(0).id);



	            });
            });
        """