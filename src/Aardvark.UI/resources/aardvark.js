let aardvark = document.aardvark;

if (!aardvark) {
    aardvark = { initialized: false };
    document.aardvark = aardvark;
}

function getTopAardvark() {
    try {
        return top.aardvark;
    } catch {
        return aardvark;
    }
}

if (!aardvark.initialized) {
    aardvark.channels = {};
    aardvark.references = { "jquery-script": true };
    aardvark.promise = new Promise(function (succ, _) { succ(); });
    aardvark.localhost = location.hostname === "localhost" || location.hostname === "127.0.0.1";

    aardvark.processEvent = function () {
        console.warn("[Aardvark] cannot process events yet (websocket not opened)");
    };
    aardvark.setEventHandler = aardvark.processEvent;

    const splitPath = function (path) {
        let dirPart, filePart;
        path.replace(/^(.*\/)?([^/]*)$/, function (_, dir, file) {
            dirPart = dir; filePart = file;
        });
        return { dirPart: dirPart, filePart: filePart };
    };

    const scripts = document.head.getElementsByTagName("script");
    let selfScript = undefined;
    for (let i = 0; i < scripts.length; i++) {
        const t = scripts[i];
        const comp = splitPath(t.src);
        if (comp.filePart === "aardvark.js") {
            selfScript = comp.dirPart;
            break;
        }
    }

    if (selfScript) {
        //document.currentScript.src
        console.debug("[Aardvark] self-url: " + selfScript);

        aardvark.getScriptRelativeUrl = function (protocol, relativePath) {
            if (relativePath.startsWith("/")) relativePath = relativePath.substring(1);
            return selfScript.replace("https://", protocol + "://").replace("http://", protocol + "://") + relativePath;
        };
    }

    aardvark.getRelativeUrl = function (protocol, relativePath) {
        const location = window.location;
        let path = splitPath(location.pathname);
        if (relativePath.startsWith("/")) relativePath = relativePath.substring(1);
        return protocol + "://" + window.location.host + path.dirPart + relativePath;
    }

    try {
        aardvark.guid = sessionStorage.aardvarkId;
    } catch {}

    if (!aardvark.guid) {
        const delim = "-";

        function S4() {
            return (((1 + Math.random()) * 0x10000) | 0).toString(16).substring(1);
        }

        const guid = (S4() + S4() + delim + S4() + delim + S4() + delim + S4() + delim + S4() + S4() + S4());
        aardvark.guid = guid;

        try {
            sessionStorage.aardvarkId = guid;
        } catch {}
    }

    aardvark.addReferences = function (refs, userCode) {
        function loadScript(ref) {
            return new Promise((resolve, _) => {
                const name = ref.name;
                const kind = ref.kind; // "script", "module", or "stylesheet"
                const url = ref.url;

                const key = `${name}-${kind}`; // allow using identical names for different kinds

                if (aardvark.references[key]) {
                    return resolve();
                }

                aardvark.references[key] = true;

                const isScript = kind === "script" || kind === "module";
                const refElem = document.createElement(isScript ? "script" : "link");
                const cc = function () {
                    console.debug(`[Aardvark] referenced ${kind} "${name}" (${url})`);
                    resolve();
                };
                const err = function () {
                    console.warn(`[Aardvark] failed to reference ${kind} "${name}" (${url})`);
                    resolve();
                };

                refElem.addEventListener("load", cc);
                refElem.addEventListener("error", err);

                if (isScript) {
                    if (kind === "module") {
                        refElem.type = "module";
                    }
                    refElem.src = url;
                    refElem.async = true;
                    document.getElementsByTagName("script")[0].parentNode.appendChild(refElem);
                }
                else {
                    refElem.setAttribute("rel", "stylesheet");
                    refElem.setAttribute("href", url);
                    document.head.appendChild(refElem);
                }
            });
        }

        aardvark.promise = aardvark.promise.then(() => Promise.all(refs.map(loadScript)));

        aardvark.promise = aardvark.promise.then(() => userCode());
    };

    aardvark.connect = function (path) {
        const search = /([^&=]+)=?([^&]*)/g;
        const decode = function (s) { return decodeURIComponent(s.replace(/\+/g, " ")); }
        const query = window.location.search.substring(1);

        let wsQuery = '?session=' + aardvark.guid;

        let match;
        while (match = search.exec(query)) {
            wsQuery = wsQuery + "&" + decode(match[1]) + "=" + decode(match[2]);
        }

        const url = aardvark.getRelativeUrl('ws', path + wsQuery);
        const eventSocket = new WebSocket(url);

        const doPing = function () {
            if (eventSocket.readyState <= 1) {
                eventSocket.send("#ping");
                setTimeout(doPing, 500);
            }
        };

        eventSocket.onopen = function () {
            aardvark.processEvent = function () {
                const sender = arguments[0];

                const event = arguments[1];
                const name = event.name ?? event; // event can be a string or an object with name and version
                const version = event.version;

                const args = [];
                for (let i = 2; i < arguments.length; i++) {
                    args.push(JSON.stringify(arguments[i]));
                }

                const message = JSON.stringify({ sender: sender, name: name, version: version, args: args });
                eventSocket.send(message);
            };
            // Sends an empty event message, indicating that the event handler for the given version should be set active
            aardvark.setEventHandler = function () {
                const sender = arguments[0];
                const name = arguments[1];
                const version = arguments[2];

                const message = JSON.stringify({ sender: sender, name: name, version: version });
                eventSocket.send(message);
            };
            doPing();
        };

        eventSocket.onmessage = function (m) {
            const c = m.data.substring(0, 1);
            if (c === "r" || c === "x") {
                const code = m.data.substring(1, m.data.length);
                const evaluate = function () {
                    try {
                        (new Function(`{ ${code} }`))();
                    } catch (e) {
                        console.warn("could not execute event message with exn " + e + ":\n" + code);
                        debugger;
                    }
                }

                if (c === "r") {
                    // addReferences function directly chains script/stylesheet loading and user code execution in aardvark.promise chain
                    evaluate();
                } else {
                    aardvark.promise = aardvark.promise.then(evaluate);
                }
            } else {
                // { targetId : string; channel : string; data : 'a }
                const message = JSON.parse(m.data);
                const channelName = message.targetId + "_" + message.channel;

                aardvark.promise = aardvark.promise.then(function () {
                    try{
                        const channel = aardvark.channels[channelName];

                        if (channel && channel.onmessage) {
                            channel.onmessage(message.data);
                        }
                    } catch (e) {
                        console.error(`Channel '${channelName}' onmessage faulted: ` + e);
                        debugger;
                    }
                });
            }
        };

        eventSocket.onclose = function () {
            aardvark.processEvent = function () { };
        };

        eventSocket.onerror = function () {
            aardvark.processEvent = function () { };
        };
    }

    aardvark.getChannel = function (id, name) {
        const channelName = id + "_" + name;
        let channel = aardvark.channels[channelName];
        if (channel) {
            return channel;
        } else {
            channel = new Channel(channelName);
            aardvark.channels[channelName] = channel;
            return channel;
        }
    };

    aardvark.setAttribute = function (element, name, value) {
        if (name === "value") {
            element.setAttribute(name, value);
            element.value = value;
        }
        else if (name === "checked") {
            element.setAttribute(name, value);
            element.checked = !!value;
        }
        else if (name === "selected") {
            element.setAttribute(name, value);
            element.selected = value;
        }
        else {
            element.setAttribute(name, value);
        }
    };

    aardvark.initialized = true;
}

class Channel {
    constructor(name) {
        this.name = name;
        this.pending = undefined;
        this._recv = undefined;
    }

    received(data) {
        if (this._recv) {
            for (let i = 0; i < data.length; i++) {
                const msg = JSON.parse(data[i]);
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

if (aardvark.electron) {
    aardvark.openFileDialog = function (config, callback) {
        if (!callback) callback = config;
        const props = {properties: ['openFile', 'multiSelections']};
        const all = {...props, ...config};
        aardvark.electron.remote.dialog.showOpenDialog(all).then(e => callback(e.filePaths));
    };

    aardvark.saveFileDialog = function (config, callback) {
        if (!callback) callback = config;
        const props = { properties: [] };
        const all = {...props, ...config};
        aardvark.electron.remote.dialog.showSaveDialog(all).then(e => callback([e.filePath]));
    };
} else {
    const showError = () => console.error("File dialogs only work with Aardium.");

    // Defined by Aardium (only need to set if absent)
    if (!aardvark.dialog) {
        aardvark.dialog = {};
        aardvark.dialog.showOpenDialog = () => { showError(); return Promise.resolve({ filePaths: [] }) };
        aardvark.dialog.showSaveDialog = () => { showError(); return Promise.resolve({ filePath: "" }) };
    }

    if (!aardvark.openFileDialog) {
        aardvark.openFileDialog = showError;
    }

    if (!aardvark.saveFileDialog) {
        aardvark.saveFileDialog = showError;
    }
}