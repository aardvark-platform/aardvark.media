// Run only in a rendered Chromium document, with the bundled plugins and F# boot/channel output.
(async function () {
    const failures = [], packets = [], callbacks = [], lifecycle = [];
    window.trace = [];
    let completed = 0, showing = 0, closing = 0;
    const assert = (condition, message) => { if (!condition) throw new Error(message); };
    const equal = (actual, expected, message) =>
        assert(JSON.stringify(actual) === JSON.stringify(expected), `${message}: ${JSON.stringify(actual)} != ${JSON.stringify(expected)}`);
    const owner = box => $(box).closest('[data-notification-owner]').attr('id');
    const run = async (name, action) => {
        try { await action(); } catch (e) { failures.push(name + ': ' + e.message); }
        completed++;
    };
    const until = async predicate => {
        for (let i = 0; i < 200; i++) {
            if (predicate()) return;
            await new Promise(resolve => setTimeout(resolve, 10));
        }
        throw new Error('Toast transition did not finish');
    };

    // Capture transport only: keep the real aardvark.processEvent serializer.
    let socket;
    window.WebSocket = class {
        constructor() { socket = this; this.readyState = 1; }
        send(text) {
            if (text === '#ping') return;
            const packet = JSON.parse(text);
            packets.push(packet);
            if (packet.sender === 'receiver') {
                trace.push('send:' + packet.sender);
                return;
            }
            const id = JSON.parse(packet.args[0]);
            callbacks.push({ sender: packet.sender, id });
            lifecycle.push('send:' + packet.sender);
            assert(document.getElementById(packet.sender).querySelector('.toast-box'), 'onremove must precede DOM removal');
        }
    };
    aardvark.connect('events');
    socket.onopen();

    // Shorten animations only in this fixture; still use real Fomantic transitions and DOM removal.
    Object.assign($.fn.toast.settings.transition, { showDuration: 0, hideDuration: 0, closeDuration: 0, closeEasing: '' });
    $.fn.toast.settings.onShow = function () { showing++; };
    $.fn.toast.settings.onVisible = function () { showing--; };
    $.fn.toast.settings.onHide = function () { closing++; lifecycle.push('hide:' + owner(this)); };
    $.fn.toast.settings.onHidden = function () { closing--; lifecycle.push('hidden:' + owner(this)); };
    const originalConsoleError = console.error;
    console.error = (...args) => { scriptErrors.push(args.join(' ')); originalConsoleError(...args); };

    for (const fixture of fixtures) {
        await run(fixture.name, async () => {
            callbacks.length = 0;
            lifecycle.length = 0;
            for (const id of fixture.owners) {
                const container = document.createElement('div');
                container.id = id;
                container.style.position = 'relative';
                container.dataset.notificationOwner = '';
                document.body.append(container);
            }
            new Function(fixture.boot)();
            for (const step of fixture.steps) {
                if (step.kind === 'deliver' || step.kind === 'replay') {
                    const data = step.kind === 'deliver' ? step.data : fixture.steps.find(s => s.kind === 'deliver' && !s.data.data && s.data.id === step.id).data;
                    const container = document.getElementById(step.target);
                    const before = container.querySelectorAll('.toast-box').length;
                    // Feed the actual generated channel data through the real client channel dispatcher.
                    socket.onmessage({ data: JSON.stringify({ targetId: step.target, channel: 'channelNotify', data: [JSON.stringify(data)] }) });
                    await aardvark.promise;
                    await until(() => showing === 0 && closing === 0);
                    if (data.data) {
                        assert(container.querySelectorAll('.toast-box').length === before + 1, 'Creation must render in its owning container');
                        const messages = [...container.querySelectorAll('.toast .message')].map(e => e.textContent);
                        assert(messages.includes(data.data.message), 'Rendered message must retain the channel payload');
                        assert(container.querySelector('.toast .close.icon'), 'Configured dismissal icon must render');
                    }
                } else if (step.kind === 'dismiss') {
                    const container = document.getElementById(step.target);
                    const before = container.querySelectorAll('.toast-box').length;
                    const close = container.querySelector('.toast .close.icon');
                    assert(close, 'Dismiss a real rendered close icon');
                    close.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
                    await until(() => closing === 0 && container.querySelectorAll('.toast-box').length === before - 1);
                } else if (step.kind === 'check') {
                    const rendered = fixture.owners.map(id => [...document.getElementById(id).querySelectorAll('.toast .message')].map(e => e.textContent).sort());
                    const key = callback => `${callback.sender}:${callback.id}`;
                    equal({ messages: rendered, callbacks: callbacks.map(key).sort() },
                          { messages: step.messages.map(messages => messages.slice().sort()), callbacks: step.callbacks.map(key).sort() },
                          'Rendered ownership and callback destinations');
                    // Each actual close preserves onHide -> onHidden -> server callback order.
                    for (let i = 0; i < lifecycle.length; i += 3) {
                        const id = lifecycle[i].substring('hide:'.length);
                        equal(lifecycle.slice(i, i + 3), ['hide:' + id, 'hidden:' + id, 'send:' + id], 'Plugin callback order');
                    }
                } else throw new Error('Unknown fixture step');
            }
        });
        for (const id of fixture.owners) document.getElementById(id)?.remove();
    }

    await run('distinct-child bubbling control', async () => {
        const parent = document.getElementById('receiver'), child = document.getElementById('child');
        const parentBounds = parent.getBoundingClientRect.bind(parent), childBounds = child.getBoundingClientRect.bind(child);
        parent.getBoundingClientRect = () => { trace.push('bounds:receiver'); return parentBounds(); };
        child.getBoundingClientRect = () => { trace.push('bounds:child'); return childBounds(); };
        child.addEventListener('click', event => {
            assert(event.target === child && event.currentTarget === child, 'Distinct child must be the target');
            trace.push('child');
        });
        document.addEventListener('click', event => {
            assert(event.target === child && event.currentTarget === document, 'Event must bubble to document');
            trace.push('document');
        }, { once: true });
        const event = new MouseEvent('click', { bubbles: true, cancelable: true, clientX: 111, clientY: 83, button: 0 });
        assert(child.dispatchEvent(event) && !event.defaultPrevented, 'Default action must not be cancelled');
        equal(trace, ['child', 'receiver:receiver', 'before', 'bounds:receiver', 'send:receiver', 'after', 'document'], 'Receiver, geometry source and side-effect order');
        const packet = packets.filter(packet => packet.sender === 'receiver');
        assert(packet.length === 1 && packet[0].name === 'onclick', 'Exactly one generated mouse event');
        equal(packet[0].args.map(JSON.parse), [0, { X: '71.0000000000', Y: '33.0000000000' }, { X: '200.0000000000', Y: '120.0000000000' }], 'Literal parent-local coordinates');
    });
    socket.readyState = 3;
    failures.push(...scriptErrors.map(error => 'Script error: ' + error));
    const result = document.createElement('pre');
    result.id = 'notification-results';
    result.textContent = JSON.stringify({ completed, failures, packets });
    document.body.append(result);
})().catch(error => {
    const result = document.createElement('pre');
    result.id = 'notification-results';
    result.textContent = JSON.stringify({ completed: -1, failures: [String(error)], packets: [] });
    document.body.append(result);
});
