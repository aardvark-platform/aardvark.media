// Native synthetic touches and real layout/SVG/listeners; only WebSocket transport is captured.
const results = [], errors = [], byId = new Map();
let current = null;
window.addEventListener("error", e => (current ? current.errors : errors).push(e.message));
window.WebSocket = class {
    constructor() { this.readyState = 1; window.socket = this; }
    send(text) {
        if (text === "#ping") return;
        const p = JSON.parse(text), s = byId.get(p.sender);
        if (!s) throw new Error("Unexpected sender " + p.sender);
        s.packets.push(p);
        const kind = p.name.split("_")[0].substring(10);
        s.trace.push("send:" + kind);
        const name = p.name.substring(p.name.indexOf("_") + 1);
        const circle = document.getElementById("touch_circle_" + name + "_" + s.id);
        const line = document.getElementById("touch_line_" + name + "_" + s.id);
        check(s, kind === "start" ? !circle && !line : !!circle && !!line, "send precedes overlay creation/update/removal");
        if (kind === "move") check(s, line === s.linesBefore.get(name), "movement is sent before line replacement");
        check(s, s.event && !s.event.defaultPrevented, "send precedes preventDefault");
    }
};
aardvark.connect("events");
socket.onopen();
function check(s, ok, message) { if (!ok) s.problems.push(message); }
function ids(s) {
    return [...document.querySelectorAll("svg")].map(e => e.id).filter(id => id.endsWith("_" + s.id)).sort();
}
function overlays(s, names) {
    const expected = names.flatMap(n => ["touch_circle_" + n + "_" + s.id, "touch_line_" + n + "_" + s.id]).sort();
    check(s, JSON.stringify(ids(s)) === JSON.stringify(expected), "exact literal overlay IDs: " + JSON.stringify(ids(s)));
}
function prepare(id, names) {
    const host = document.getElementById(id), child = host.firstElementChild;
    const s = { id, names, host, child, packets: [], problems: [], errors: [], trace: [], bounds: { host: 0, child: 0 }, contacts: new Map() };
    results.push(s); byId.set(id, s); current = s;
    for (const [el, key, expected] of [[host, "host", [40, 50, 200, 120]], [child, "child", [90, 70, 40, 30]]]) {
        const original = el.getBoundingClientRect, r = original.call(el);
        check(s, JSON.stringify([r.x, r.y, r.width, r.height]) === JSON.stringify(expected), "actual rendered " + key + " geometry");
        el.getBoundingClientRect = function () {
            check(s, this === el, "native bounds receiver");
            s.bounds[key]++; s.trace.push("bounds:" + key);
            return original.call(this);
        };
    }
    for (const type of ["touchstart", "touchmove", "touchend", "touchcancel"]) {
        child.addEventListener(type, () => s.trace.push("child"));
        host.addEventListener(type, function (e) {
            s.trace.push("before");
            check(s, this === host && e.currentTarget === host && e.target === child && !e.defaultPrevented, "native receiver/currentTarget versus child target before handling");
        });
    }
}
function finish() {
    const s = current;
    for (const type of ["touchstart", "touchmove", "touchend", "touchcancel"]) {
        s.host.addEventListener(type, function (e) {
            s.trace.push("after");
            check(s, this === s.host && e.currentTarget === s.host && e.target === s.child && e.defaultPrevented, "native receiver and canceled default after handling");
        });
        document.addEventListener(type, e => {
            if (e.target === s.child) s.trace.push("document");
        });
    }
    current = null;
}
function touch(s, type, points, sends) {
    s.trace = []; s.bounds = { host: 0, child: 0 };
    s.linesBefore = new Map(s.names.map(n => [n, document.getElementById("touch_line_" + n + "_" + s.id)]));
    const changed = points.map(([identifier, clientX, clientY]) => new Touch({ identifier, clientX, clientY, target: s.child }));
    for (const t of changed) {
        if (type === "touchstart" || (type === "touchmove" && s.contacts.has(t.identifier))) s.contacts.set(t.identifier, t);
        if (type === "touchend" || type === "touchcancel") s.contacts.delete(t.identifier);
    }
    const active = [...s.contacts.values()];
    s.event = new TouchEvent(type, { bubbles: true, cancelable: true, changedTouches: changed, touches: active, targetTouches: active });
    const accepted = s.child.dispatchEvent(s.event);
    check(s, !accepted && s.event.defaultPrevented, "dispatch returns canceled only after handlers finish");
    const expected = ["child", "before"];
    if (type === "touchstart") {
        for (let i = 0; i < points.length; i++) {
            expected.push("bounds:host");
            if (i < sends) expected.push("bounds:host", "send:start");
        }
    } else {
        const kind = type === "touchmove" ? "move" : "stop";
        for (let i = 0; i < sends; i++) expected.push("send:" + kind);
    }
    expected.push("after", "document");
    check(s, JSON.stringify(s.trace) === JSON.stringify(expected), "side-effect/default/bubbling order: " + JSON.stringify(s.trace));
    check(s, s.bounds.host === (type === "touchstart" ? points.length + sends : 0) && s.bounds.child === 0, "only real host bounds are used");
    if (type === "touchmove" && sends) for (const [name, old] of s.linesBefore) {
        if (old) check(s, document.getElementById("touch_line_" + name + "_" + s.id) !== old, "line replacement finishes before dispatch returns");
    }
}
function run() {
    for (const s of results) {
        current = s;
        try {
            if (s.names.length < 2) {
                const count = s.names.length;
                touch(s, "touchstart", [[1, 111, 83]], count); overlays(s, s.names);
                touch(s, "touchmove", [[1, 114, 87]], count); overlays(s, s.names);
                touch(s, "touchmove", [[1, 171, 163]], count); overlays(s, s.names);
                touch(s, "touchcancel", [[1, 171, 163]], count); overlays(s, []);
                touch(s, "touchend", [[1, 171, 163]], 0);
                touch(s, "touchmove", [[1, 114, 87]], 0);
                touch(s, "touchstart", [[1, 111, 83]], count); overlays(s, s.names);
                touch(s, "touchmove", [[1, 108, 79]], count);
                touch(s, "touchend", [[1, 108, 79]], count); overlays(s, []);
            } else {
                const [left, right] = s.names;
                touch(s, "touchstart", [[2, 191, 83], [1, 111, 83]], 2); overlays(s, s.names);
                touch(s, "touchmove", [[1, 114, 87], [2, 185, 75]], 2); overlays(s, s.names);
                touch(s, "touchcancel", [[1, 114, 87]], 1); overlays(s, [right]);
                touch(s, "touchmove", [[2, 187, 80]], 1); overlays(s, [right]);
                touch(s, "touchend", [[2, 187, 80]], 1); overlays(s, []);
            }
        } catch (e) { s.errors.push(e.message); }
        overlays(s, []);
        check(s, s.contacts.size === 0, "all native contacts drained");
        // Clean only after observing failures so one malformed-name case cannot mask another.
        for (const el of [...document.querySelectorAll("svg")]) if (el.id.endsWith("_" + s.id)) el.remove();
    }
    current = null;
    const output = document.createElement("pre"); output.id = "touchstick-names-result";
    output.textContent = JSON.stringify({ errors, results: results.map(({ id, packets, problems, errors }) => ({ id, packets, problems, errors })) });
    document.body.appendChild(output);
}
