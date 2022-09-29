
class FileBrowser {
    constructor(config) {
        this.config = config;
        this.browseCache = {};
        this.caching = config.caching == undefined ? true : config.caching;

        if (!config.browse) {
            if (!config.url) {
                console.error("[FS] no url given");
                throw "[FS] no url given";
            }
            var self = this;
            this.browsefn = function (path, cont) {
                var pathUrl = self.config.url + "?path=" + path;
                console.debug("[FS] reading: " + pathUrl);

                var request = new XMLHttpRequest();
                request.open("GET", pathUrl);

                request.onerror = function () {
                    console.warn("[FS] got no response");
                    cont({ success: false, entries: [] });
                }

                request.onload = function (e) {
                    var response = request.responseText;

                    if (response) {
                        var data = JSON.parse(response);

                        for (var i = 0; i < data.entries.length; i++) {
                            data.entries[i].id = data.entries[i].path;
                        }

                        console.debug("[FS] got response: { success: " + data.success + "}");
                        cont(data);
                    }
                    else {
                        console.warn("[FS] got no response");
                        cont({ success: false, entries: [] });
                    }
                };

                request.send()
            };

        }
        else {
            this.browsefn = config.browse;
        }

    }

    process(res) {
        if (res.success) {
            for (var i = 0; i < res.entries.length; i++) {
                var e = res.entries[i];

                var icon = "folder";
                if (e.kind === "Disk") icon = "disk outline";
                else if (e.kind === "DVD") icon = "file code outline";
                else if (e.kind === "Removable") icon = "folder outline";
                else if (e.kind === "Share") icon = "cloud";
                else if (e.kind === "Folder") icon = "folder outline";
                else if (e.kind === "File") icon = "file outline";
                e.icon = icon;
            }
        }

        return res;
    }

    browse(path, cont) {
        var self = this;

        if (this.caching) {
            var cacheEntry = self.browseCache[path];
            if (cacheEntry) {
                cont(cacheEntry);
            }
            else {
                this.browsefn(path, function (res) {
                    var res1 = self.process(res);
                    self.browseCache[path] = res1;
                    cont(res1);
                });
            }

        }
        else {
            function real(res) { self.cont(process(res)); }
            this.browsefn(path, real);
        }
    }

	submit(path) {
		if(!path) path = this.selectedPath;
		if(path) {
			if(this.config.submit)
				this.config.submit(path);
		}
		else console.log("empty");
	}
	
	cancel() {
		if(this.config.cancel)
			this.config.cancel();
	}
	
}


function initTree(element, browser) {

    function getChildren(element, obj, cb) {
        var path = "/"
        if (obj.id !== '#') path = obj.original.id;
        console.warn("tree: " + path);

        browser.browse(path, function (result) {
            if (result.success) {

                var all =
                    result.entries
                        .filter(e => (e.isDevice || e.hasChildren) && !e.isHidden && !e.isSystem)
                        .map(function (e) {
                            return {
                                id: e.path,
                                path: e.path,
                                text: e.name,
                                children: e.hasFolders,
                                icon: e.icon + ' icon inverted jstree-themeicon-custom'
                            }
                        });

                cb(all);

            }
        });


    }

    var treeInstance =
        $(element).jstree(
            {
                core:
                {
                    themes: {
                        name: "default-dark"
                    },
                    data: function (o, a) { getChildren(this, o, a); },
                    multiple: false
                }
            }
        );

    return treeInstance;
}

var TimeAgo = (function () {
    var self = {};

    // Public Methods
    self.locales = {
        prefix: '',
        sufix: 'ago',

        seconds: 'less than a minute',
        minute: 'about a minute',
        minutes: '%d minutes',
        hour: 'about an hour',
        hours: 'about %d hours',
        day: 'a day',
        days: '%d days',
        month: 'about a month',
        months: '%d months',
        year: 'about a year',
        years: '%d years'
    };

    self.inWords = function (timeAgo) {
        var seconds = Math.floor((new Date() - timeAgo) / 1000),
            separator = this.locales.separator || ' ',
            words = this.locales.prefix + separator,
            interval = 0,
            intervals = {
                year: seconds / 31536000,
                month: seconds / 2592000,
                day: seconds / 86400,
                hour: seconds / 3600,
                minute: seconds / 60
            };

        var distance = this.locales.seconds;

        for (var key in intervals) {
            interval = Math.floor(intervals[key]);

            if (interval > 1) {
                distance = this.locales[key + 's'];
                break;
            } else if (interval === 1) {
                distance = this.locales[key];
                break;
            }
        }

        distance = distance.replace(/%d/i, interval);
        words += distance + separator + this.locales.sufix;

        return words.trim();
    };

    return self;
}());

function createUI(element, browser) {
    element.innerHTML = "";

    var d = document.createDocumentFragment();

    var root = document.createElement("div")
    root.className = "filebrowser";

    var header = document.createElement("div");
    header.classList = "filebrowser header";



    var pathElement = document.createElement("div");
    pathElement.classList = "filebrowser path";

    var text = document.createElement("div");

    var textBox = document.createElement("input");
    textBox.type = "text";
    text.appendChild(textBox);

    text.classList = "ui input"

    pathElement.appendChild(text);

    var overlay = document.createElement("div");
    overlay.classList = "browsebuttons ui buttons inverted small"
    pathElement.appendChild(overlay);

    var nav = document.createElement("div")
    nav.classList = "ui icon buttons inverted";


    var back = document.createElement("button");
    back.classList = "ui button inverted";
    back.innerHTML = "<i class='arrow left icon'></i>"

    var forward = document.createElement("button");
    forward.classList = "ui button inverted";
    forward.innerHTML = "<i class='arrow right icon'></i>"

    var up = document.createElement("button");
    up.classList = "ui button inverted";
    up.innerHTML = "<i class='arrow up icon'></i>"

    nav.appendChild(back);
    nav.appendChild(forward);
    nav.appendChild(up);




    var tree = document.createElement("div");
    tree.classList = "filebrowser tree";
    var splitter = document.createElement("div");
    splitter.classList = "filebrowser splitter";
    var list = document.createElement("div");
    list.classList = "filebrowser list";
    var realTree = document.createElement("div");
    tree.appendChild(realTree);
    var jstree = initTree(realTree, browser);

    var table = document.createElement("table");
    table.classList = "ui selectable inverted sortable celled small single line table";
    var thead = document.createElement("thead");
    var tbody = document.createElement("tbody");
    table.appendChild(thead);
    table.appendChild(tbody);
    $(thead).html("<tr><th>Name</th><th class='date'>Date Modified</th><th>Type</th></tr>");

    var body = document.createElement("div");
    body.classList = "filebrowser body";
    body.appendChild(tree);
    body.appendChild(splitter);
    body.appendChild(list);
    header.appendChild(nav);
    header.appendChild(pathElement);
    root.appendChild(header);
    root.appendChild(body);
    list.appendChild(table);
    d.appendChild(root);
    element.appendChild(d);


    textBox.onblur = function (e) {
        overlay.style.display = "inline-flex";
        textBox.style.textIndent = "100%";
        textBox.style.whiteSpace = "nowrap";
        textBox.style.overflow = "hidden";
    };


    textBox.onfocus = function (e) {
        overlay.style.display = "none";
        textBox.style.textIndent = "initial";
        textBox.style.whiteSpace = "initial";
        textBox.style.overflow = "initial";
        textBox.select();
    };



    var drag = false;
    var off = 0;
    var org = 0;
    var splitWidth = 10;
    var standardBG = "";

    splitter.onmousedown = function (e) {
        org = tree.getBoundingClientRect().width;
        off = e.screenX;
        drag = true;
        splitWidth = splitter.getBoundingClientRect().width;
        standardBG = splitter.style.backgroundColor;
        splitter.style.backgroundColor = "white";
    };

    $(document).mouseup(function (e) {
        if (drag) {
            drag = false;
            splitter.style.backgroundColor = standardBG;
        }
    });

    $(document).mousemove(function (e) {
        if (drag) {
            var max = root.getBoundingClientRect();
            var newWidth = org + (e.screenX - off);
            if (newWidth < 50) newWidth = 50;
            if (newWidth > max.width - 50) newWidth = max.width - 50;

            var lp = 100.0 * newWidth / max.width;
            var rp = 100.0 - lp;

            // flex: 0 0 25%;
            tree.style.flex = "0 0 " + newWidth + "px";
            list.style.width = "100%";
            e.preventDefault();
        }
    });


    var past = [];
    var future = [];
    var currentPath = "/";
    var currentComp = [];
    var windowsPaths = true;


    function parsePath(path) {
        var comp =
            path.split(/\\|\//)
            .map(function (c) {
                if (c.endsWith(":")) return c.substring(0, c.length - 1);
                else return c;
            })
            .filter(c => c.length > 0);


        return comp;
    }

    function setEntries(entries) {

        var currentlyActiveTR = undefined;

        function createRow(value) {
             if (value.kind === "File" && browser.config.hideFiles)
                 return undefined;

            var icon = "<i class='" + value.icon + " icon inverted'></i>";
            var date = new Date(value.lastWriteTime);

            var tr = document.createElement("tr")
            var name = document.createElement("td")
            var modified = document.createElement("td")
            var kind = document.createElement("td")

            name.innerHTML = icon + value.name;

            modified.setAttribute("data-sort-value", date.valueOf());
            modified.innerText = TimeAgo.inWords(date);
            kind.innerText = value.kind;

            tr.appendChild(name); tr.appendChild(modified); tr.appendChild(kind);


            name.style.cursor = 'default';
            modified.style.cursor = 'default';
            kind.style.cursor = 'default';
            tr.style.userSelect = "none";

            function select(tr) {
                if (tr.classList.contains("active")) {
                    tr.classList.remove("active");
                }
                else {
                    if (currentlyActiveTR) {
                        currentlyActiveTR.classList.remove("active");
                    }
                    currentlyActiveTR = tr;
                    tr.classList.add("active");
                }

                browser.selectedPath = value.path;
                if (browser.config.onselect) browser.config.onselect(value.path);
            };

            if (value.kind == "File") {

                tr.onclick = function (e) {
                    if (e.detail === 1) {
                        select(tr);
                    }
                    else {
                        if (browser.config.fileSelect) {
                            if (currentlyActiveTR) {
                                currentlyActiveTR.classList.remove("active");
                            }
                            currentlyActiveTR = tr;
                            tr.classList.add("active");
                            if (browser.config.submit) browser.config.submit(value.path);
                        }

                    }
                };

            }
            else {
                tr.onclick = function (e) {
                    if (e.detail === 1) {
                        if (browser.config.folderSelect) {
                            select(tr);
                        }
                    }
                    else if (e.detail === 2) {
                        navigate(value.path);
                    }

                };
                tr.ondblclick = function () { navigate(value.path); };
            }


            return tr;
        }

        var code =
            entries
                .filter(e => !(e.isHidden || e.isSystem))
                .map(function (value) { return createRow(value); });
		
		// var old = tbody.style.display;
		// tbody.style.display = "none";
		// tbody.style.display = old;
		tbody.innerHTML = "";
        for (var i = 0; i < code.length; i++) {
            var c = code[i];
            if (c) tbody.appendChild(c);
        }
    }

    function setAddressBar(comp) {

        function buildButtons(list, path, comp, i) {
            if (i < comp.length) {
                var content = comp[i];
                if (i < comp.length - 1) content += "<i class='angle right icon'></i>";

                var b = document.createElement("button");
                b.classList = "ui button inverted small";
                b.setAttribute("style", "height: 100%");
                b.innerHTML = content;

                var ownPath = path + comp[i];

                b.onclick = function () { navigate(ownPath); };//console.warn("clicky: " + ownPath); };

                list.push(b);
                return buildButtons(list, path + comp[i] + "/", comp, i + 1);
            }
            else return list;
        }

        var inner = buildButtons([], "/", comp, 0);

        var path = "";
        if (windowsPaths) path = comp.map(function (e, index) { if (index == 0) return e + ":"; else return e; }).join("\\");
        else path = "/" + comp.join("/");

        textBox.value = path;

        overlay.innerHTML = "";
        for (var i = 0; i < inner.length; i++) {
            overlay.appendChild(inner[i]);
        }

    }

    function navigateInternal(path, cont) {
        var comp = parsePath(path);
        var fullPath = "/" + comp.join("/");
        if (fullPath !== currentPath) {
            browser.browse(fullPath, function (res) {
                if (res.success) {
                    currentPath = fullPath;
                    currentComp = comp;

                    setEntries(res.entries);
                    setAddressBar(comp);
                    if (cont) cont(true, comp);
                }
                else {
                    setAddressBar(currentComp);
                }
            });
        }
        else {
            if (cont) cont(false, currentComp);
        }
    }

    function navigate(path, cont) {
        var old = currentPath;

        function whenDone(changed, comp) {
            if (cont) cont(comp);
            if (changed) {
                future = [];
                past.push(old);
            }
        }

        navigateInternal(path, whenDone);
    }

    function goback() {
        if (past.length > 0) {
            var pp = past.pop();
            future.push(currentPath);
            navigateInternal(pp);
        }
    }

    function goforward() {
        if (future.length > 0) {
            var fp = future.pop();
            past.push(currentPath);
            navigateInternal(fp);
        }
    }

    jstree.on("select_node.jstree", function (e, a) {
        setEntries([]);
        var entry = a.node.original;
        navigate(entry.path);
    });

    textBox.onchange = function (e) {
        textBox.blur();
        navigate(textBox.value);
    };

    up.onclick = function () {
        if (currentComp.length > 0) {
            var newComp = currentComp.slice(0, currentComp.length - 1);
            var newPath = "/" + newComp.join("/");
            navigate(newPath);
        }
    };
    back.onclick = goback;
    forward.onclick = goforward;

    if (browser.config.startPath) {
        navigateInternal(browser.config.startPath);
    }
    else {
        navigateInternal("/");
    }

    $(document).ready(function () {
        $(table)
            .tablesort()
            .colResizable({
                liveDrag: true,
                resizeMode: 'fit'
            });
    });
}

$.fn.filebrowser = function (browser) {
    // extract the browser-config (if any)
    if (!browser) {
        if (this.length > 0) {
            return this.get(0).filebrowser;
        }
        else {
            return undefined;
        }
    }


    this.each(function (index, item) {
        createUI(item, browser);
        item.filebrowser = browser;
    });


    return browser;
};


function splitPath(path) {
    var dirPart, filePart;
    path.replace(/^(.*\/)?([^/]*)$/, function (_, dir, file) {
        dirPart = dir; filePart = file;
    });
    return { dirPart: dirPart, filePart: filePart };
}

var getRelativeUrl = function (protocol, relativePath) {
    var location = window.location;
    var path = splitPath(location.pathname);
    var dir = path.dirPart;

    if (relativePath.startsWith("/")) relativePath = relativePath.substring(1);
    if (!dir.startsWith("/")) dir = "/" + dir;
    if (!dir.endsWith("/")) dir = dir + "/";

    var path = protocol + "://" + window.location.host + path.dirPart + relativePath;
    console.warn(path);

    return path;
};
