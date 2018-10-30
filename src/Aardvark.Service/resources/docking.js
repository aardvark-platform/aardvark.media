var Docking;
(function (Docking) {
    var Kind;
    (function (Kind) {
        Kind["Vertical"] = "vertical";
        Kind["Horizontal"] = "horizontal";
        Kind["Stack"] = "stack";
        Kind["Element"] = "element";
    })(Kind = Docking.Kind || (Docking.Kind = {}));
    var DockMode;
    (function (DockMode) {
        DockMode["Above"] = "above";
        DockMode["Below"] = "below";
        DockMode["Before"] = "before";
        DockMode["After"] = "after";
        DockMode["On"] = "on";
        DockMode["Header"] = "header";
        DockMode["Window"] = "window";
    })(DockMode || (DockMode = {}));
    function getCssProperties(className, props) {
        var frag = document.createDocumentFragment();
        var e = document.createElement("div");
        frag.appendChild(e);
        e.style.visibility = "hidden";
        e.className = className;
        document.body.appendChild(e);
        var style = getComputedStyle(e);
        var result = {};
        props.forEach(function (p) {
            result[p] = style[p];
        });
        e.remove();
        return result;
    }
    var ptToPxRatio = null;
    function ptToPx() {
        if (!ptToPxRatio) {
            var frag = document.createDocumentFragment();
            var testDiv = document.createElement("div");
            frag.appendChild(testDiv);
            testDiv.style.visibility = "hidden";
            testDiv.style.width = "100pt";
            document.body.appendChild(testDiv);
            var ratio = testDiv.clientWidth / 100;
            ptToPxRatio = ratio;
            testDiv.remove();
        }
        return ptToPxRatio;
    }
    function parseSize(size) {
        if (size.endsWith("px")) {
            return parseFloat(size.substr(0, size.length - 2));
        }
        else if (size.endsWith("pt")) {
            return parseFloat(size.substr(0, size.length - 2)) * ptToPx();
        }
        else
            return undefined;
    }
    function hasLeftRootSplit(cfg) {
        if (cfg.kind == Kind.Vertical) {
            return true;
        }
        else if (cfg.kind == Kind.Horizontal) {
            if (cfg.children.length > 0) {
                return hasLeftRootSplit(cfg.children[0]);
            }
            else
                return false;
        }
        else
            return false;
    }
    function hasRightRootSplit(cfg) {
        if (cfg.kind == Kind.Vertical) {
            return true;
        }
        else if (cfg.kind == Kind.Horizontal) {
            if (cfg.children.length > 0) {
                return hasRightRootSplit(cfg.children[cfg.children.length - 1]);
            }
            else
                return false;
        }
        else
            return false;
    }
    function hasTopRootSplit(cfg) {
        if (cfg.kind == Kind.Horizontal) {
            return true;
        }
        else if (cfg.kind == Kind.Vertical) {
            if (cfg.children.length > 0) {
                return hasTopRootSplit(cfg.children[0]);
            }
            else
                return false;
        }
        else
            return false;
    }
    function hasBottomRootSplit(cfg) {
        if (cfg.kind == Kind.Horizontal) {
            return true;
        }
        else if (cfg.kind == Kind.Vertical) {
            if (cfg.children.length > 0) {
                return hasBottomRootSplit(cfg.children[cfg.children.length - 1]);
            }
            else
                return false;
        }
        else
            return false;
    }
    var DockNodeConfig = /** @class */ (function () {
        function DockNodeConfig(kind, weight, children) {
            this.kind = kind;
            this.weight = weight;
            this.children = children;
        }
        return DockNodeConfig;
    }());
    Docking.DockNodeConfig = DockNodeConfig;
    var DockConfig = /** @class */ (function () {
        function DockConfig() {
        }
        return DockConfig;
    }());
    Docking.DockConfig = DockConfig;
    function createHeaderContent(state, element) {
        var cfg = state.config;
        var root = state.root;
        var layouter = root.parent;
        var text = document.createElement("div");
        text.style.cssFloat = "left";
        text.innerHTML = cfg.title || cfg.id;
        element.appendChild(text);
        if (!cfg.isCloseable) {
            return;
        }
        var closeButton = document.createElement("a");
        closeButton.innerText = "";
        element.appendChild(closeButton);
        closeButton.className = "icon close";
        closeButton.style.cursor = "pointer";
        closeButton.style.marginLeft = "10pt";
        closeButton.style.cssFloat = "right";
        closeButton.style.right = "0";
        closeButton.onclick = function (e) {
            var state = root.getState(cfg.id);
            if (state) {
                if (state.parent) {
                    if (state.parent.config.kind == Kind.Stack && state.parent.config.activeTabId == cfg.id) {
                        delete state.parent.config.activeTabId;
                    }
                }
                if (state.remove()) {
                    state.kill();
                    root.updateConfig(root.config, true);
                }
            }
            e.stopPropagation();
        };
        //element.innerHTML = "<button>" + (cfg.title || cfg.id) + "</button>";
        //return cfg.title || cfg.id;
    }
    var DockNodeState = /** @class */ (function () {
        function DockNodeState(root, node, parent) {
            var self = this;
            if (node.id) {
                root.setState(node.id, this);
            }
            if (node.children)
                this.children = node.children.map(function (a) { return new DockNodeState(root, a, self); });
            else
                this.children = [];
            if (parent)
                this.parent = parent;
            this.root = root;
            this.config = node;
            this.top = -1;
            this.left = -1;
            this.width = -1;
            this.height = -1;
        }
        DockNodeState.prototype.updateStyle = function (s, x, y, w, h, px, py) {
            if (x !== undefined) {
                if (px !== undefined && px != 0)
                    s.left = "calc(" + x + "% + " + px + "px)";
                else
                    s.left = x + "%";
            }
            if (y !== undefined) {
                if (py !== undefined && py != 0)
                    s.top = "calc(" + y + "% + " + py + "px)";
                else
                    s.top = y + "%";
            }
            if (w !== undefined) {
                if (px !== undefined && px != 0)
                    s.width = "calc(" + w + "% - " + px + "px)";
                else
                    s.width = w + "%";
            }
            if (h !== undefined) {
                if (py !== undefined && py != 0)
                    s.height = "calc(" + h + "% - " + py + "px)";
                else
                    s.height = h + "%";
            }
        };
        DockNodeState.prototype.hide = function () {
            if (this._element) {
                this._element.style.display = "none";
            }
        };
        DockNodeState.prototype.show = function () {
            if (this._element) {
                this._element.style.display = "";
            }
        };
        Object.defineProperty(DockNodeState.prototype, "weight", {
            get: function () {
                return this.config.weight;
            },
            enumerable: true,
            configurable: true
        });
        DockNodeState.prototype.updateLayout = function (x, y, w, h, px, py) {
            var _this = this;
            var splitterSize = this.root.parent.splitterSize;
            this.left = x;
            this.top = y;
            this.width = w;
            this.height = h;
            switch (this.config.kind) {
                case Kind.Vertical:
                    var sumOfWeights = 0;
                    this.children.forEach(function (element) { sumOfWeights += element.weight; });
                    var heightPerWeight = h / sumOfWeights;
                    var splitterCount = (this.children.length - 1);
                    var i = 0;
                    var last = null;
                    this.children.forEach(function (element) {
                        if (i > 0) {
                            var prev = last;
                            var next = element;
                            var splitter = _this.root.createSplitter(true);
                            py = 0;
                            _this.updateStyle(splitter.style, x, y, w, undefined, px, py);
                            splitter.onmousedown = function (e) { _this.root.startResize(true, prev, next, e, splitter); return false; };
                            splitter.style.display = "";
                            py += splitterSize;
                        }
                        var h = element.weight * heightPerWeight;
                        element.updateLayout(x, y, w, h, px, py);
                        y += h;
                        last = element;
                        i++;
                    });
                    break;
                case Kind.Horizontal:
                    var sumOfWeights = 0;
                    this.children.forEach(function (element) { sumOfWeights += element.weight; });
                    var widthPerWeight = w / sumOfWeights;
                    var last = null;
                    var i = 0;
                    this.children.forEach(function (element) {
                        if (i > 0) {
                            var prev = last;
                            var next = element;
                            var splitter = _this.root.createSplitter(false);
                            px = 0;
                            splitter.style.display = "";
                            _this.updateStyle(splitter.style, x, y, undefined, h, px, py);
                            splitter.onmousedown = function (e) { _this.root.startResize(false, prev, next, e, splitter); return false; };
                            px += splitterSize;
                        }
                        var w = element.weight * widthPerWeight;
                        element.updateLayout(x, y, w, h, px, py);
                        x += w;
                        last = element;
                        i++;
                    });
                    break;
                case Kind.Element:
                    if (!this._element) {
                        var self = this;
                        this._element = this.root.getElement(this.config.id, (function (e) { return createHeaderContent(self, e); }), this.config.userInfo);
                    }
                    this._element.style.display = "";
                    this.updateStyle(this._element.style, x, y, w, h, px, py);
                    break;
                case Kind.Stack:
                    var e = this._element;
                    var activeId = this.config.activeTabId || this.config.children[0].id;
                    if (!e) {
                        var frag = document.createDocumentFragment();
                        var newHeader = document.createElement("div");
                        frag.appendChild(newHeader);
                        newHeader.className = "dock-element-tab-header";
                        this.root.parent.element.appendChild(newHeader);
                        e = newHeader;
                        this._element = e;
                        var activeArr = this.children.filter(function (e) { return e.config.id === activeId; });
                        var active = null;
                        if (activeArr.length > 0)
                            active = activeArr[0];
                        else
                            active = this.children[0];
                        var self = this;
                        var selfCfg = this.config;
                        var currentlyActiveState = active;
                        var currentlyActive = null;
                        var headers = {};
                        var changeActive = function (n, state) {
                            currentlyActive.classList.remove("active");
                            var nh = headers[n];
                            nh.classList.add("active");
                            currentlyActive = nh;
                            currentlyActiveState.hide();
                            state.show();
                            currentlyActiveState = state;
                            selfCfg.activeTabId = n;
                            self.root.parent.triggerLayoutChange();
                        };
                        this.children.forEach(function (state) {
                            var id = state.config.id;
                            var title = state.config.title || id;
                            var e = document.createElement("div");
                            frag.appendChild(e);
                            var activeClass = "";
                            if (id == activeId) {
                                currentlyActive = e;
                                activeClass = "active";
                            }
                            e.className = "dock-header-tab " + activeClass;
                            createHeaderContent(state, e);
                            var self = _this.root;
                            e.onmousedown = function (e) { self.startDrag(self.getState(id), e, window); return false; };
                            e.onclick = function () { changeActive(id, self.getState(id)); return false; };
                            headers[id] = e;
                            newHeader.appendChild(e);
                        });
                    }
                    this.updateStyle(e.style, x, y, w, undefined, px, py);
                    var i = 0;
                    this.children.forEach(function (element) {
                        element.updateLayout(x, y, w, h, px, py);
                        if (element.config.id == activeId)
                            element.show();
                        else
                            element.hide();
                        i++;
                    });
                    break;
                default:
                    console.warn("[Docking] unknown kind: " + this.config.kind);
                    break;
            }
        };
        DockNodeState.prototype.getClientPosition = function (x, y) {
            if (this.config.kind == Kind.Stack) {
                var activeId = this.config.activeTabId || this.config.children[0].id;
                var activeChild = this.children.filter(function (e) { return e.config.id == activeId; });
                if (activeChild.length > 0) {
                    return activeChild[0].getClientPosition(x, y);
                }
                else
                    return undefined;
            }
            else if (this._element) {
                var rect = this._element.getBoundingClientRect();
                return {
                    x: ((100 * x - this.left) / this.width) * rect.width,
                    y: ((100 * y - this.top) / this.height) * rect.height
                };
            }
            else {
                return undefined;
            }
        };
        DockNodeState.prototype.getElementAt = function (x, y) {
            var rx = x - this.left;
            var ry = y - this.top;
            if (rx >= 0 && ry >= 0 && rx <= this.width && ry <= this.height) {
                if (this.config.kind == Kind.Element || this.config.kind == Kind.Stack) {
                    return this;
                }
                else {
                    for (var i = 0; i < this.children.length; i++) {
                        var e = this.children[i].getElementAt(x, y);
                        if (e)
                            return e;
                    }
                    return undefined;
                }
            }
            else {
                return undefined;
            }
        };
        DockNodeState.prototype.replaceWith = function (nElement) {
            if (this.parent) {
                var index = this.parent.children.indexOf(this);
                var pc = this.parent.config;
                var sc = nElement.config;
                this.kill();
                pc.children.splice(index, 1, sc);
                this.parent.children.splice(index, 1, nElement);
            }
            else {
                this.root.config.content = nElement.config;
                this.root.root = nElement;
            }
        };
        DockNodeState.prototype.remove = function () {
            if (this.parent) {
                if (this.parent.children.length == 1) {
                    return this.parent.remove();
                }
                else {
                    var index = this.parent.children.indexOf(this);
                    if (index >= 0 && index < this.parent.children.length) {
                        this.parent.children.splice(index, 1);
                        this.parent.config.children.splice(index, 1);
                        if (this.parent.config.kind == Kind.Stack && this.parent.config.activeTabId == this.config.id) {
                            delete this.parent.config.activeTabId;
                        }
                        if (this.parent.children.length == 1) {
                            var r = this.parent.children[0];
                            r.config.weight = this.parent.weight;
                            if (this.parent._element) {
                                this.parent._element.remove();
                                delete this.parent._element;
                            }
                            this.parent.replaceWith(r);
                        }
                        return true;
                    }
                    else {
                        return false;
                    }
                }
            }
        };
        DockNodeState.prototype.pin = function (x, y) {
            if (this._element && !this._element.classList.contains("moving")) {
                this._element.classList.add("moving");
            }
            this._element.style.display = "";
            this._element.style.left = x + "px";
            this._element.style.top = y + "px";
        };
        DockNodeState.prototype.unpin = function () {
            this._element.classList.remove("moving");
        };
        DockNodeState.prototype.kill = function () {
            if (this._element) {
                this.root.freeElement(this._element, this.config);
                delete this._element;
            }
            this.children.forEach(function (c) {
                c.kill();
            });
            this.children = [];
            this.top = -1;
            this.left = -1;
            this.width = -1;
            this.height = -1;
        };
        Object.defineProperty(DockNodeState.prototype, "isValid", {
            get: function () {
                return this.width >= 0 && this.height >= 0;
            },
            enumerable: true,
            configurable: true
        });
        return DockNodeState;
    }());
    var ResizeState = /** @class */ (function () {
        function ResizeState(horizontal, prev, next, startValue) {
            this.horizontal = horizontal;
            this.prev = prev;
            this.next = next;
            this.startValue = startValue;
            this.startPrevWeight = prev.config.weight;
            this.startNextWeight = next.config.weight;
            this.startPrevSize = horizontal ? prev.height : prev.width;
            this.startNextSize = horizontal ? next.height : next.width;
            this.startPrevOffset = horizontal ? prev.y : prev.x;
            this.startNextOffset = horizontal ? next.y : next.x;
        }
        return ResizeState;
    }());
    var DragState = /** @class */ (function () {
        function DragState(state, hover, startX, startY, w, closeOriginal) {
            this.state = state;
            this.hover = hover;
            this.mode = undefined;
            this.target = undefined;
            this.isRemoved = false;
            this.startX = startX;
            this.startY = startY;
            this.window = w;
            this.closeOriginal = closeOriginal;
        }
        DragState.prototype.closeIfWasLast = function () {
            if (this.closeOriginal) {
                this.closeOriginal.close();
                delete this.closeOriginal;
            }
        };
        return DragState;
    }());
    var DockState = /** @class */ (function () {
        function DockState(parent, config) {
            var hasParent = false;
            if (window["parentLayouter"]) {
                hasParent = true;
                this._rootLayouter = window["parentLayouter"];
            }
            else {
                this._rootLayouter = this;
            }
            var self = this._rootLayouter;
            window.addEventListener("mousemove", function (e) {
                self.mouseMove(e.screenX, e.screenY, e, window, true);
            });
            window.addEventListener("scroll", function (e) {
                self.scrollChanged();
            });
            window.addEventListener("mouseup", function (e) {
                self.mouseUp(e.screenX, e.screenY, window, true);
            });
            this._otherWindows = [];
            this.parent = parent;
            this.config = config;
            this._elements = {};
            this._states = {};
            this._splitters = [];
            this._unused = {};
            this.root = new DockNodeState(this, config.content);
        }
        DockState.prototype.updateLayout = function (raiseEvents) {
            this._splitters.forEach(function (s) { return s.remove(); });
            this._splitters = [];
            this.root.updateLayout(0, 0, 100, 100, 0, 0);
            if (raiseEvents) {
                this.parent.triggerLayoutChange();
            }
        };
        DockState.prototype.getState = function (id) {
            return this._states[id];
        };
        DockState.prototype.setState = function (id, state) {
            this._states[id] = state;
        };
        DockState.prototype.updateConfig = function (cfg, raiseEvents) {
            //this.parent.element.classList.add("changing");
            this.root.kill();
            this.config = cfg;
            this._states = {};
            this.root = new DockNodeState(this, cfg.content);
            this.updateLayout(raiseEvents);
            for (var id in this._unused) {
                var e = this._unused[id];
                e.remove();
                console.log("[Docking] delete " + id);
            }
            this._unused = {};
            //this.parent.element.classList.remove("changing");
        };
        DockState.prototype.getElementAt = function (x, y) {
            var cfg = this.config.content;
            return this.root.getElementAt(100 * x, 100 * y);
        };
        DockState.prototype.freeElement = function (element, cfg) {
            if (cfg.deleteInvisible || cfg.kind == Kind.Stack) {
                if (cfg.id) {
                    this._unused[cfg.id] = element;
                    delete this._elements[cfg.id];
                }
                element.style.display = "none";
            }
            else {
                element.style.display = "none";
            }
        };
        DockState.prototype.createSplitter = function (horizontal) {
            var frag = document.createDocumentFragment();
            var splitter = document.createElement("div");
            frag.appendChild(splitter);
            var className = horizontal ? "horizontal" : "vertical";
            splitter.className = "dock-splitter " + className;
            splitter.style.display = "none";
            this.parent.element.appendChild(splitter);
            this._splitters.push(splitter);
            return splitter;
        };
        DockState.prototype.getElement = function (id, createHeader, info) {
            var e = this._elements[id];
            if (!e) {
                e = this._unused[id];
                if (e) {
                    delete this._unused[id];
                    this._elements[id] = e;
                }
                else {
                    console.log("[Docking] create " + id);
                    var frag = document.createDocumentFragment();
                    var container = document.createElement("div");
                    var header = document.createElement("div");
                    var content = document.createElement("div");
                    var headerContent = document.createElement("div");
                    frag.appendChild(container);
                    container.appendChild(header);
                    container.appendChild(content);
                    header.appendChild(headerContent);
                    headerContent.className = "dock-header-title";
                    container.className = "dock-container";
                    header.className = "dock-element-header";
                    content.className = "dock-element-content";
                    createHeader(headerContent);
                    var self = this;
                    header.onmousedown = function (e) { self.startDrag(self._states[id], e, window); return false; };
                    this.parent.initElement(content, id, info);
                    this.parent.element.appendChild(container);
                    e = container;
                    this._elements[id] = e;
                }
            }
            return e;
        };
        DockState.prototype.makeRelative = function (pageX, pageY) {
            function getBounds(e) {
                var rect = e.getBoundingClientRect();
                return {
                    x: rect.left + window.pageXOffset,
                    y: rect.top + window.pageYOffset,
                    w: rect.width,
                    h: rect.height
                };
            }
            var rect = getBounds(this.parent.element); //.getBoundingClientRect();
            var x = (pageX - rect.x) / rect.w;
            var y = (pageY - rect.y) / rect.h;
            if (x > 1.0) {
                x = 1.0;
            }
            if (x < 0.0) {
                x = 0.0;
            }
            if (y > 1.0) {
                y = 1.0;
            }
            if (y < 0.0) {
                y = 0.0;
            }
            return { x: x, y: y };
            // if(x >= 0 && y >= 0 && x <= rect.w && y <= rect.h) {
            //     return { x: x / rect.w, y: y / rect.h };
            // }
            // else {
            //     return undefined;
            // }
        };
        DockState.prototype.startResize = function (horizontal, prev, next, e, splitter) {
            var pos = this.makeRelative(e.pageX, e.pageY);
            if (pos) {
                this.parent.element.classList.add("noevents");
                var startValue = horizontal ? pos.y : pos.x;
                this._resize = new ResizeState(horizontal, prev, next, startValue);
                splitter.classList.add("dragging");
            }
        };
        DockState.prototype.updateResize = function (pos) {
            var resize = this._resize;
            var value = resize.horizontal ? pos.y : pos.x;
            var diff = value - resize.startValue;
            var weightSum = resize.startPrevWeight + resize.startNextWeight;
            var pns = resize.startPrevSize + 100 * diff;
            var nns = resize.startNextSize - 100 * diff;
            var totalSize = resize.startPrevSize + resize.startNextSize;
            if (pns < 2) {
                pns = 2;
                nns = totalSize - pns;
            }
            if (nns < 2) {
                nns = 2;
                pns = totalSize - nns;
            }
            var nWeight = weightSum / (pns / nns + 1);
            var pWeight = weightSum - nWeight;
            resize.prev.config.weight = pWeight;
            resize.next.config.weight = nWeight;
            this.updateLayout(false);
        };
        DockState.prototype.stopResize = function () {
            this.parent.triggerLayoutChange();
        };
        DockState.prototype.startDrag = function (state, e, w) {
            if (state.parent) {
                this._drag = new DragState(state, undefined, e.pageX, e.pageY, w, undefined);
                state.root.parent.element.classList.add("noevents");
            }
            else if (window["root-layouter"]) {
                this.log("asdasdsad");
                this._drag = new DragState(state, undefined, e.pageX, e.pageY, w, w);
            }
        };
        DockState.prototype.updateDrag = function (target, mode) {
            var drag = this._drag;
            if (!drag.isRemoved) {
                drag.state.remove();
                drag.isRemoved = true;
            }
            if (drag.isRemoved) {
                if (!drag.hover) {
                    var frag = document.createDocumentFragment();
                    var hover = document.createElement("div");
                    frag.appendChild(hover);
                    hover.className = "dock-hover-box";
                    hover.style.display = "none";
                    this.parent.element.appendChild(hover);
                    this.updateConfig(this.config, false);
                    drag.hover = hover;
                }
            }
            drag.mode = mode;
            drag.target = target;
            var hover = drag.hover;
            var x = target.left;
            var y = target.top;
            var w = target.width;
            var h = target.height;
            var xs = x + "%";
            var ys = y + "%";
            var ws = w + "%";
            var hs = h + "%";
            switch (mode) {
                case DockMode.Above:
                    hs = (h / 2) + "%";
                    break;
                case DockMode.Below:
                    hs = (h / 2) + "%";
                    ys = (y + h / 2) + "%";
                    break;
                case DockMode.Before:
                    ws = (w / 2) + "%";
                    break;
                case DockMode.After:
                    ws = (w / 2) + "%";
                    xs = (x + w / 2) + "%";
                    break;
                case DockMode.Header:
                    hs = this.parent.headerSize + "px";
                    break;
                default:
                    break;
            }
            hover.style.display = "";
            hover.style.top = ys;
            hover.style.left = xs;
            hover.style.width = ws;
            hover.style.height = hs;
        };
        DockState.prototype.openNewWindow = function (cfg, screen) {
            var winStr = "menubar=no,scrollbars=no,status=no,titlebar=no,toolbar=no,top=" + screen.y.toFixed() + ",left=" + screen.x.toFixed() + ",width=400,height=400";
            console.warn(winStr);
            function guid() {
                function s4() {
                    return Math.floor((1 + Math.random()) * 0x10000)
                        .toString(16)
                        .substring(1);
                }
                return s4() + s4() + '-' + s4() + '-' + s4() + '-' + s4() + '-' + s4() + s4() + s4();
            }
            var w = window.open(null, guid(), winStr);
            var references = [];
            for (var i = 0; i < document.styleSheets.length; i++) {
                var sheet = document.styleSheets[i];
                if (sheet.href) {
                    var line = "<link rel='stylesheet' type='text/css' href='" + sheet.href + "' />";
                    references.push(line);
                }
            }
            for (var i = 0; i < document.scripts.length; i++) {
                var script = document.scripts[i];
                if (script.src) {
                    var line = "<script src='" + script.src + "'></" + "script>";
                    references.push(line);
                }
            }
            var cfgStr = JSON.stringify(cfg);
            this.log(cfgStr);
            var boot = [
                "var layout = { content: " + cfgStr + " };",
                "document.onreadystatechange = function(e) {",
                "    if(document.readyState == 'complete') {",
                "        var e = document.getElementById('root');",
                "        layouter = new Docking.DockLayout(e, layout, document.initElement);",
                "        window['root-layouter'] = layouter;",
                "    }",
                "};"
            ];
            var headerRefs = references.join("\n");
            var template = "<html><head>" + headerRefs + "\n<script>" + boot.join("\n") + "</" + "script></head><body style='padding: 0; margin: 0; border: 0'><div style='width: 100%; height: 100%' class='dock-root' id='root'></div></body></html>";
            w["parentLayouter"] = this;
            w.document["initElement"] = this.parent._initElement;
            w.document.write(template);
            w.document.close();
            this._otherWindows.push(w);
            w.moveTo(screen.x, screen.y);
        };
        DockState.prototype.stopDrag = function (target, mode, screen) {
            var kind = null;
            var children = null;
            var drag = this._drag;
            var targetThing = target.config;
            var insertThing = drag.state.config;
            insertThing.weight = targetThing.weight;
            drag.state.unpin();
            if (mode == DockMode.Header)
                mode = DockMode.On;
            switch (mode) {
                case DockMode.Above:
                    kind = Kind.Vertical;
                    children = [insertThing, targetThing];
                    break;
                case DockMode.Below:
                    kind = Kind.Vertical;
                    children = [targetThing, insertThing];
                    break;
                case DockMode.Before:
                    kind = Kind.Horizontal;
                    children = [insertThing, targetThing];
                    break;
                case DockMode.After:
                    kind = Kind.Horizontal;
                    children = [targetThing, insertThing];
                    break;
                case DockMode.On:
                    kind = Kind.Stack;
                    children = [targetThing, insertThing];
                    break;
                case DockMode.Window:
                    console.warn("new window");
                    this._rootLayouter.openNewWindow(drag.state.config, screen);
                    return;
                default:
                    console.warn("bad dockmode: " + mode);
                    return;
            }
            if (target.config.kind == Kind.Stack && mode == DockMode.On) {
                targetThing.children.push(insertThing);
                this.updateConfig(this.config, true);
            }
            else if (target.parent) {
                var parentThing = target.parent.config;
                var index = parentThing.children.indexOf(targetThing);
                if (kind == parentThing.kind) {
                    if (kind != Kind.Stack) {
                        targetThing.weight /= 2;
                        insertThing.weight /= 2;
                    }
                    parentThing.children.splice(index, 1, children[0], children[1]);
                }
                else {
                    var newThing = new DockNodeConfig(kind, targetThing.weight, children);
                    parentThing.children.splice(index, 1, newThing);
                }
                this.updateConfig(this.config, true);
            }
            else {
                if (targetThing.kind == kind) {
                    if (children[0] == insertThing) {
                        targetThing.children.splice(0, 0, insertThing);
                    }
                    else {
                        targetThing.children.push(insertThing);
                    }
                    this.updateConfig(this.config, true);
                }
                else {
                    this.config.content = new DockNodeConfig(kind, 10, children);
                    this.updateConfig(this.config, true);
                }
            }
        };
        DockState.prototype.getDockMode = function (rx, ry) {
            if (rx < 0.33333)
                return DockMode.Before;
            else if (rx > 0.66666)
                return DockMode.After;
            else if (ry < 0.33333)
                return DockMode.Above;
            else if (ry > 0.66666)
                return DockMode.Below;
            else
                return DockMode.On;
        };
        DockState.prototype.update = function (windowX, windowY) {
            if ((windowX < 0 || windowY < 0 || windowX >= window.innerWidth || windowY >= window.innerHeight)) {
                if (this._drag) {
                    var drag = this._drag;
                    drag.state.unpin();
                    drag.state.hide();
                    if (drag.hover) {
                        drag.hover.style.display = "none";
                    }
                    drag.mode = DockMode.Window;
                }
                return true;
            }
            else {
                var pageX = windowX + window.pageXOffset;
                var pageY = windowY + window.pageYOffset;
                var pos = this.makeRelative(pageX, pageY);
                if (this._resize) {
                    if (pos) {
                        this.updateResize(pos);
                        return false;
                    }
                }
                else if (this._drag && pos) {
                    var dx = pageX - this._drag.startX;
                    var dy = pageY - this._drag.startY;
                    var len = Math.sqrt(dx * dx + dy * dy);
                    if (len > 10 || this._drag.isRemoved) {
                        var drag = this._drag;
                        var el = this.getElementAt(pos.x, pos.y);
                        if (el) {
                            var abs = el.getClientPosition(pos.x, pos.y);
                            var mode = null;
                            if (abs && abs.y >= 0 && abs.y < this.parent.headerSize) {
                                mode = DockMode.Header;
                            }
                            else {
                                var cfg = this.config.content;
                                var specialDockSize = this.config.specialDockSize || 0.05;
                                if ((pos.x < specialDockSize && hasLeftRootSplit(cfg)) ||
                                    (pos.x > (1 - specialDockSize) && hasRightRootSplit(cfg)) ||
                                    (pos.y < specialDockSize && hasTopRootSplit(cfg)) ||
                                    (pos.y > (1 - specialDockSize) && hasBottomRootSplit(cfg))) {
                                    mode = this.getDockMode(pos.x, pos.y);
                                    el = this.root;
                                }
                                else {
                                    var rx = (100 * pos.x - el.left) / el.width;
                                    var ry = (100 * pos.y - el.top) / el.height;
                                    mode = this.getDockMode(rx, ry);
                                }
                            }
                            drag.mode = mode;
                            drag.state.pin(windowX, windowY);
                            this.updateDrag(el, mode);
                            return false;
                        }
                    }
                }
            }
            return true;
        };
        DockState.prototype.mouseMove = function (screenX, screenY, e, w, dispatch) {
            if (dispatch) {
                var srcState = this;
                if (w["root-layouter"]) {
                    srcState = w["root-layouter"]["state"];
                }
                for (var i = -1; i < this._otherWindows.length; i++) {
                    var wc = i < 0 ? window : this._otherWindows[i];
                    var l = i < 0 ? this.parent : this._otherWindows[i]["root-layouter"];
                    if (l) {
                        var clientX = screenX - (wc.screenLeft + (wc.outerWidth - wc.innerWidth - 8));
                        var clientY = screenY - (wc.screenTop + (wc.outerHeight - wc.innerHeight - 8));
                        if (clientX >= 0 && clientY >= 0 && clientX < wc.innerWidth && clientY < wc.innerHeight) {
                            var drag = this._drag || srcState._drag;
                            if (drag) {
                                if (!l.state._drag || l.state._drag.window != wc) {
                                    var otherState = new DockNodeState(l.state, drag.state.config);
                                    otherState.updateLayout(0, 0, 50, 50, 0, 0);
                                    l.state._drag = new DragState(otherState, undefined, 0, 0, wc, drag.closeOriginal);
                                    l.state._drag.isRemoved = true;
                                }
                                else {
                                    this.log("already has drag");
                                }
                            }
                            l.state.mouseMove(screenX, screenY, e, wc, false);
                            return true;
                        }
                        else {
                            l.state.hideDragAndResize();
                        }
                    }
                }
                return false;
            }
            else {
                var clientX = screenX - (w.screenLeft + (w.outerWidth - w.innerWidth - 8));
                var clientY = screenY - (w.screenTop + (w.outerHeight - w.innerHeight - 8));
                this._lastMove = { x: clientX, y: clientY };
                if (this.update(clientX, clientY)) {
                    e.stopPropagation();
                }
                return true;
            }
        };
        DockState.prototype.scrollChanged = function () {
            if (this._lastMove)
                this.update(this._lastMove.x, this._lastMove.y);
        };
        DockState.prototype.log = function (str) {
            var w = window;
            while (w.opener)
                w = w.opener;
            w.console.warn(str);
        };
        DockState.prototype.killDragAndResize = function () {
            this.parent.element.classList.remove("noevents");
            this._lastMove = null;
            if (this._resize) {
                delete this._resize;
            }
            if (this._drag) {
                if (this._drag.hover) {
                    this._drag.hover.remove();
                }
                if (this._drag.state) {
                    this._drag.state.kill();
                }
                delete this._drag;
            }
        };
        DockState.prototype.hideDragAndResize = function () {
            this.parent.element.classList.remove("noevents");
            this._lastMove = null;
            if (this._resize) {
                delete this._resize;
            }
            if (this._drag) {
                if (this._drag.hover) {
                    this._drag.hover.remove();
                    delete this._drag.hover;
                }
                if (this._drag.state) {
                    this._drag.state.hide();
                }
            }
        };
        DockState.prototype.mouseUp = function (screenX, screenY, w, dispatch) {
            var srcState = this;
            if (w["root-layouter"]) {
                srcState = w["root-layouter"]["state"];
            }
            var drag = this._drag || srcState._drag;
            if (dispatch) {
                var worked = false;
                for (var i = -1; i < this._otherWindows.length; i++) {
                    var wc = i < 0 ? window : this._otherWindows[i];
                    var l = i < 0 ? this.parent : this._otherWindows[i]["layouter"];
                    if (l) {
                        var clientX = screenX - (wc.screenLeft + (wc.outerWidth - wc.innerWidth - 8));
                        var clientY = screenY - (wc.screenTop + (wc.outerHeight - wc.innerHeight - 8));
                        if (clientX >= 0 && clientY >= 0 && clientX < wc.innerWidth && clientY < wc.innerHeight) {
                            l.state.mouseUp(screenX, screenY, wc, false);
                            worked = true;
                            break;
                        }
                    }
                }
                if (worked) {
                    for (var i = -1; i < this._otherWindows.length; i++) {
                        var wc = i < 0 ? window : this._otherWindows[i];
                        var l = i < 0 ? this.parent : this._otherWindows[i]["layouter"];
                        l.state.killDragAndResize();
                    }
                    return true;
                }
                else {
                    if (drag) {
                        this._drag = drag;
                        drag.mode = DockMode.Window;
                        this.mouseUp(screenX, screenY, window, false);
                        for (var i = -1; i < this._otherWindows.length; i++) {
                            var wc = i < 0 ? window : this._otherWindows[i];
                            var l = i < 0 ? this.parent : this._otherWindows[i]["layouter"];
                            l.state.killDragAndResize();
                        }
                        return true;
                    }
                    else {
                        for (var i = -1; i < this._otherWindows.length; i++) {
                            var wc = i < 0 ? window : this._otherWindows[i];
                            var l = i < 0 ? this.parent : this._otherWindows[i]["layouter"];
                            l.state.killDragAndResize();
                        }
                        return false;
                    }
                }
            }
            else {
                var clientX = screenX - (w.screenLeft + (w.outerWidth - w.innerWidth - 8));
                var clientY = screenY - (w.screenTop + (w.outerHeight - w.innerHeight - 8));
                this.parent.element.classList.remove("noevents");
                this._lastMove = null;
                if (this._resize) {
                    this.stopResize();
                    delete this._resize;
                }
                if (this._drag) {
                    if (this._drag.target && this._drag.mode) {
                        this._drag.closeIfWasLast();
                        this.stopDrag(this._drag.target, this._drag.mode, { x: screenX, y: screenY });
                    }
                    if (this._drag.hover) {
                        this._drag.hover.remove();
                    }
                    delete this._drag;
                }
                return true;
            }
        };
        return DockState;
    }());
    var DockLayout = /** @class */ (function () {
        function DockLayout(element, config, createElement) {
            var splitterHeight = getCssProperties("dock-splitter horizontal", ["height"])["height"];
            var headerHeight = getCssProperties("dock-element-header", ["height"])["height"];
            this.splitterSize = parseSize(splitterHeight);
            this.headerSize = parseSize(headerHeight);
            this._layoutChanged = undefined;
            if (config.appName && config.useCachedConfig) {
                var key = "docking-js-" + config.appName;
                var cached = localStorage.getItem(key);
                if (cached) {
                    config = JSON.parse(cached);
                }
                this.onlayoutchanged = function (cfg) {
                    localStorage.setItem(key, JSON.stringify(cfg));
                };
            }
            this.element = element;
            var root = JSON.parse(JSON.stringify(config));
            this._initElement = createElement;
            this.root = root;
            this.state = new DockState(this, root);
            this.state.updateLayout(false);
        }
        Object.defineProperty(DockLayout.prototype, "currentConfig", {
            get: function () {
                return JSON.parse(JSON.stringify(this.root));
            },
            set: function (value) {
                var root = JSON.parse(JSON.stringify(value));
                this.state.updateConfig(root, false);
                this.root = root;
            },
            enumerable: true,
            configurable: true
        });
        DockLayout.prototype.setCurrentConfig = function (value, raiseEvents) {
            var root = JSON.parse(JSON.stringify(value));
            this.root = root;
            this.state.updateConfig(root, raiseEvents);
        };
        DockLayout.prototype.initElement = function (element, id, userInfo) {
            return this._initElement(element, id, userInfo);
        };
        DockLayout.prototype.triggerLayoutChange = function () {
            if (this._layoutChanged) {
                this._layoutChanged(this.currentConfig);
            }
        };
        Object.defineProperty(DockLayout.prototype, "onlayoutchanged", {
            set: function (value) {
                if (value) {
                    var old = this._layoutChanged;
                    if (old) {
                        this._layoutChanged = function (layout) {
                            old(layout);
                            value(layout);
                        };
                    }
                    else {
                        this._layoutChanged = value;
                    }
                }
            },
            enumerable: true,
            configurable: true
        });
        return DockLayout;
    }());
    Docking.DockLayout = DockLayout;
})(Docking || (Docking = {}));
//# sourceMappingURL=docking.js.map