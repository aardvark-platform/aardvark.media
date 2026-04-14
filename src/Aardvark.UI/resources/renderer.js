class Renderer {
    constructor(id) {
        this.id = id;
        this.div = document.getElementById(id);

        this.scene               = this.div.getAttribute("data-scene") || id;
        this.samples             = this.div.getAttribute("data-samples") || 1;
        this.quality             = this.div.getAttribute("data-quality") || 80;
        this.showFPS             = this.div.getAttribute("showFPS") === "true";
        this.showLoader          = this.div.getAttribute("showLoader") !== "false";
        this.useMapping          = this.div.getAttribute("useMapping") !== "false";
        this.onRendered          = this.div.getAttribute("onRendered");
        this.renderAlways        = this.div.getAttribute("data-renderalways") === "true";
        this.customLoaderImg     = this.div.getAttribute("data-customLoaderImg");
        this.customLoaderImgSize = this.div.getAttribute("data-customLoaderSize");

        this.buffer = [];
        this.loading = true;

        const self = this;
        const useMapping = aardvark.localhost && getTopAardvark().openMapping && this.useMapping;

        if (useMapping) {
            this.canvas = document.createElement("canvas");
            this.context = this.canvas.getContext("2d", { willReadFrequently: true }); // Explicitly disable GPU acceleration
        } else {
            this.canvas = document.createElement("img");
        }

        this.div.appendChild(this.canvas);
        this.canvas.setAttribute("class", "rendercontrol");
        this.canvas.style.cursor = "default";

        if (this.showLoader) {
            this.createLoader();
        }

        this.overlay = document.createElement("span");
        if (!this.showFPS) this.overlay.style = "display:none;";
        this.div.appendChild(this.overlay);
        this.overlay.className = "fps";
        this.overlay.innerText = "";
        this.frameCount = 0;
        this.div.tabIndex = 1;

        const url =
            aardvark.getScriptRelativeUrl(
                "ws",
                `../rendering/render/${this.id}?session=${aardvark.guid}&scene=${this.scene}&samples=${this.samples}&mapped=${useMapping}&quality=${this.quality}`
            );

        const onGlobalClick = function (event) {
            if (event.target === self.div) self.div.focus();
        };
        document.addEventListener("click", onGlobalClick, false);

        const socket = new WebSocket(url);
        socket.binaryType = "blob";
        self.socket = socket;

        const doPing = function () {
            const state = self.getState();

            if (state === WebSocket.OPEN) {
                socket.send("#ping");
            }

            if (state <= WebSocket.OPEN) {
                setTimeout(doPing, 1000);
            }
        };

        socket.onopen = function () {
            for (let i = 0; i < self.buffer.length; i++) {
                socket.send(self.buffer[i]);
            }
            self.buffer = [];

            self.requestImage();

            doPing();
        };

        socket.onmessage = function (msg) {
            self.received(msg);
        };

        socket.onclose = function () {
            delete self.socket;
            self.fadeOut();
        };

        socket.onerror = function (err) {
            console.error(err);
            socket.close();
            delete self.socket;
            self.fadeOut();
        };

        this.div.oncontextmenu = function (e) { e.preventDefault(); };

        if (!this.renderAlways) {
            const onSizeChanged = function (entries) {
                for (const entry of entries) {
                    const w = entry.borderBoxSize[0].inlineSize;
                    const h = entry.borderBoxSize[0].blockSize;
                    self.requestImage({ width: w, height: h });
                }
            }

            this.resizeObserver = new ResizeObserver(onSizeChanged);
            this.resizeObserver.observe(this.div);

            let currentColor = { r: 0, g: 0, b: 0 };

            const onStyleChanged = function (entries) {
                for (const mutation of entries) {
                    const bg = mutation.target.style.backgroundColor;
                    const color = bg ? new RGBColor(bg) : { r: 0, g: 0, b: 0 };

                    if (currentColor.r !== color.r || currentColor.g !== color.g || currentColor.b !== color.b) {
                        currentColor = color;
                        self.requestImage({ color: currentColor })
                    }
                }
            }

            this.mutationObserver = new MutationObserver(onStyleChanged);
            this.mutationObserver.observe(this.div, { attributes: true, attributeFilter: ["style"] });
        }
    }

    getState() {
        return this.socket ? this.socket.readyState : WebSocket.CLOSED;
    }

    createLoader() {
        if (!this.loader) {
            const loader = document.createElement("div");
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
            const loader = this.loader;
            delete this.loader;
            this.div.removeChild(loader);
        }
    }

    destroy() {
        if (this.socket) {
            const socket = this.socket;
            delete this.socket;
            socket.close();
        }

        if (this.resizeObserver) {
            this.resizeObserver.disconnect();
            delete this.resizeObserver;
        }

        if (this.mutationObserver) {
            this.mutationObserver.disconnect();
            delete this.mutationObserver;
        }
    }

    send(data) {
        switch (this.getState()) {
            case WebSocket.OPEN: this.socket.send(data); break;
            case WebSocket.CONNECTING: this.buffer.push(data); break;
        }
    }

    processEvent() {
        const sender = this.id;
        const name = arguments[0];
        const args = [];
        for (let i = 1; i < arguments.length; i++) {
            args.push(JSON.stringify(arguments[i]));
        }
        const message = JSON.stringify({ sender: sender, name: name, args: args });
        this.send(message);
    }

    fadeIn() {
        if (this.loading) {
            this.loading = false;
            const self = this;
            console.debug("[Aardvark] initialized renderControl " + this.id);
            $(this.canvas).animate({ opacity: 1.0 }, 400, "swing", function () {
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
            $(this.canvas).animate({ opacity: 0.0 }, 400, "swing");
        }
    }

    updateOverlay() {
        const now = performance.now();
        if (!this.lastTime) {
            this.lastTime = now;
        }

        const deltaTime = now - this.lastTime;
        if (deltaTime < 1000) {
            return;
        }

        if (this.frameCount > 0) {
            const count = this.frameCount;
            this.lastTime = now;
            this.frameCount = 0;
            const fps = 1000.0 * count / deltaTime;
            this.overlay.innerText = fps.toFixed(2) + " fps";
            if (this.overlay.style.opacity < 0.5) {
                $(this.overlay).animate({ opacity: 1.0 }, 400, "swing");
            }
        } else {
            if (this.overlay.style.opacity > 0.5) {
                $(this.overlay).animate({ opacity: 0.0 }, 400, "swing");
            }
        }
    }

    afterRender() {
        this.send(JSON.stringify({ case: "Rendered" }));

        if (this.loading) {
            this.fadeIn();
        }

        if (this.renderAlways) {
            this.requestImage();
        }
    }

    received(msg) {
        if (msg.data instanceof Blob) {
            this.updateOverlay();
            this.frameCount++;

            const oldUrl = this.canvas.src;
            this.canvas.src = window.URL.createObjectURL(msg.data);
            delete msg.data;
            window.URL.revokeObjectURL(oldUrl);

            this.afterRender();
        } else {
            const data = JSON.parse(msg.data);

            if (data.Case === "Invalidate") {
                if (!this.renderAlways) {
                    // TODO: what if not visible??
                    this.requestImage();
                }
            } else if (data.name && data.size && data.length) {
                this.updateOverlay();
                this.frameCount++;

                if (this.mapping) {
                    if (this.mapping.name !== data.name) {
                        this.mapping.close();
                        this.mapping = getTopAardvark().openMapping(data.name, data.length);
                    }
                } else {
                    this.mapping = getTopAardvark().openMapping(data.name, data.length);
                }

                if (this.frameBufferSize) {
                    if (this.frameBufferSize.X !== data.size.X || this.frameBufferSize.Y !== data.size.Y) {
                        const len = data.size.X * data.size.Y * 4;
                        this.frameBuffer = new Uint8ClampedArray(len);
                        this.frameBufferSize = data.size;
                        this.frameBufferLength = len;
                    }
                } else {
                    const len = data.size.X * data.size.Y * 4;
                    this.frameBuffer = new Uint8ClampedArray(len);
                    this.frameBufferSize = data.size;
                    this.frameBufferLength = len;
                }

                this.canvas.width = data.size.X;
                this.canvas.height = data.size.Y;
                this.frameBuffer.set(new Uint8ClampedArray(this.mapping.buffer, 0, this.frameBufferLength));
                this.context.putImageData(new ImageData(this.frameBuffer, data.size.X, data.size.Y), 0, 0);

                this.afterRender();
            } else {
                console.warn("Unexpected render message: " + JSON.stringify(data));
            }
        }
    }

    requestImage(args) {
        const rect = (args?.width !== undefined && args?.height !== undefined) ? args : this.div.getBoundingClientRect();

        let color = args?.color;
        if (color === undefined) {
            const bg = args?.color ?? window.getComputedStyle(this.div).backgroundColor;
            color = bg ? new RGBColor(bg) : { r: 0, g: 0, b: 0 };
        }

        const message = {
            case: "RequestImage",
            size: { X: Math.round(rect.width), Y: Math.round(rect.height) },
            background: { A: 255, B: color.b, G: color.g, R: color.r }
        };

        this.send(JSON.stringify(message));
    }
}

if (!aardvark.getRenderer) {
    aardvark.getRenderer = function (id) {
        const div = document.getElementById(id);
        if (!div.renderer) {
            div.renderer = new Renderer(id);
        }
        return div.renderer;
    }
}

if (!aardvark.destroyRenderer) {
    aardvark.destroyRenderer = function (id) {
        const div = document.getElementById(id);
        div.renderer?.destroy();
    }
}