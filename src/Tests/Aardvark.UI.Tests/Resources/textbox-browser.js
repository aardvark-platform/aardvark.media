(async function () {
    "use strict";
    const results = [];
    let active = null;
    let phase = "";
    let trace = [];
    let packets = [];
    const assert = (value, message) => { if (!value) throw new Error(message); };
    const same = (a, b, message) => assert(JSON.stringify(a) === JSON.stringify(b), message + ": " + JSON.stringify(a));
    const nativeBounds = HTMLElement.prototype.getBoundingClientRect;
    HTMLElement.prototype.getBoundingClientRect = function () {
        if (active) trace.push(this === active.host ? "bounds:host" : "bounds:child");
        return nativeBounds.call(this);
    };
    class CapturedSocket {
        constructor() { window.eventSocket = this; this.readyState = 0; }
        send(text) {
            if (text === "#ping") return;
            const packet = JSON.parse(text);
            packets.push(packet);
            if (packet.args) trace.push(packet.name === "data-event" ? "value-send" : "parent-send");
        }
    }
    window.WebSocket = CapturedSocket;
    aardvark.connect("events");
    eventSocket.readyState = 1;
    eventSocket.onopen();
    const NativeRegExp = window.RegExp;
    let constructions = [];
    window.RegExp = function (pattern, flags) {
        if (active && pattern === active.config.pattern) constructions.push(pattern);
        return new NativeRegExp(pattern, flags);
    };
    window.RegExp.prototype = NativeRegExp.prototype;
    window.beforeBubble = function (receiver, event) {
        assert(receiver === active.host && event.currentTarget === active.host && event.target === active.input,
               "parent receiver/currentTarget differs from the distinct child event target");
        assert(active.input.value === active.expectedValue, phase + ": rollback/value update must precede bubbling");
        assert(!active.host.classList.contains("error"), phase + ": change removes rejection class before bubbling");
        trace.push("parent-before");
    };
    window.afterBubble = function (receiver, event) {
        assert(receiver === active.host && event.currentTarget === active.host, "parent receiver survives generated handler");
        trace.push("parent-after");
    };
    document.addEventListener("change", function (event) {
        if (active && event.target === active.input) {
            assert(active.input.value === active.expectedValue, "document sees accepted/rolled back value");
            trace.push("document");
        }
    });
    const load = src => new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = src;
        script.onload = resolve;
        script.onerror = () => reject(new Error("Cannot load generated updater " + src));
        document.body.appendChild(script);
    });
    const push = async messages => {
        eventSocket.onmessage({ data: JSON.stringify({ targetId: active.config.id, channel: "valueCh", data: messages }) });
        await aardvark.promise;
    };
    function preview(value, valid) {
        const before = packets.length;
        active.input.value = value;
        active.input.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: value }));
        assert(active.host.classList.contains("error") === !valid, phase + ": input validation/error class");
        assert(packets.length === before, "input alone does not dispatch a server message");
    }
    function change(candidate, value, dispatch) {
        active.input.value = candidate;
        active.expectedValue = value;
        trace = [];
        const before = packets.filter(p => p.name === "data-event" && p.args).length;
        const event = new MouseEvent("change", { bubbles: true, cancelable: true, clientX: 111, clientY: 83, button: 0 });
        assert(active.input.dispatchEvent(event) && !event.defaultPrevented, "change does not cancel defaults");
        const after = packets.filter(p => p.name === "data-event" && p.args).length;
        assert(after - before === (dispatch ? 1 : 0), phase + ": change-only accepted dispatch");
        same(trace, dispatch ? ["child", "value-send", "parent-before", "bounds:host", "parent-send", "parent-after", "document"]
                             : ["child", "parent-before", "bounds:host", "parent-send", "parent-after", "document"],
             phase + ": actual child/message-or-rollback/receiver/geometry/parent/document order");
        assert(active.input.value === value, phase + ": visible accepted value or rollback");
    }
    for (const config of fixtures) {
        packets = [];
        constructions = [];
        const errorsBefore = scriptErrors.length;
        active = { config, host: null, input: null, expectedValue: config.initial };
        let failure = "";
        try {
            await load(config.script);
            const errors = scriptErrors.slice(errorsBefore);
            assert(errors.length === 0, "generated initialization script failed: " + errors.join("; "));
            const host = document.getElementById(config.id);
            assert(host, "generated updater created textbox");
            const input = host.querySelector("input");
            assert(input && input.type === "text" && input.maxLength === 73, "actual generated child/attributes");
            active.host = host;
            active.input = input;
            const parent = nativeBounds.call(host), child = nativeBounds.call(input);
            same([parent.left, parent.top, parent.width, parent.height], [40, 50, 200, 120], "real parent geometry");
            same([child.left, child.top, child.width, child.height], [90, 70, 40, 30], "real distinct child geometry");
            input.addEventListener("change", function (event) {
                assert(this === input && event.currentTarget === input && event.target === input, "child receiver/currentTarget");
                // Native target capture precedes Boot's jQuery target bubble listener.
                trace.push("child");
            }, { capture: true });
            await push(config.initialMessages);
            assert(input.value === config.initial, "initial actual channel payload");
            phase = "accept";
            preview(config.accepted, true);
            change(config.accepted, config.accepted, true);
            if (config.rejected !== null) {
                phase = "reject accepted";
                preview(config.rejected, false);
                change(config.rejected, config.accepted, false);
            }
            const beforePush = packets.length;
            await push(config.serverMessages);
            assert(input.value === config.server && packets.length === beforePush, "server synchronization does not dispatch");
            if (config.rejected !== null) {
                phase = "reject server";
                preview(config.rejected, false);
                change(config.rejected, config.server, false);
            }
            phase = "empty";
            preview("", config.empty);
            change("", config.empty ? "" : config.server, config.empty);
            phase = "accept after server";
            preview(config.accepted, true);
            change(config.accepted, config.accepted, true);
            phase = "duplicate change";
            change(config.accepted, config.accepted, true);
            if (config.rejected !== null) {
                phase = "reject latest accepted";
                preview(config.rejected, false);
                change(config.rejected, config.accepted, false);
            }
            assert(constructions.length <= 1, "pattern is not constructed during repeated input/change validation");
            if (constructions.length) same(constructions, [config.pattern], "exact constructor argument transport");
        } catch (error) { failure = String(error && error.stack || error); }
        results.push({ name: config.name, failure, packets });
        const host = document.getElementById(config.id);
        if (host) host.remove();
        active = null;
    }
    eventSocket.readyState = 3;
    const result = document.createElement("pre");
    result.id = "textbox-result";
    result.textContent = JSON.stringify({ cases: results });
    document.body.appendChild(result);
})().catch(error => { console.error(error); });
