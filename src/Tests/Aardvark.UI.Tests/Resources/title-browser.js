// A small rendered-page fixture: real template resources, isolated frames, captured transport only.
(async function () {
    if (window.titleFixture) {
        const failures = [];
        const check = (condition, message) => { if (!condition) failures.push(message); };
        const equal = (a, b, message) => check(JSON.stringify(a) === JSON.stringify(b), message + ': ' + JSON.stringify(a));
        try {
            for (let i = 0; !window.titleSocket?.onopen && i < 100; i++) await new Promise(resolve => setTimeout(resolve, 10));
            if (!window.titleSocket?.onopen) throw new Error('Template connection did not initialize');
            titleSocket.onopen();
            check(connections === 1, 'Exactly one isolated template connection');
            check(typeof jQuery === 'function' && typeof RGBColor === 'function' && typeof Renderer === 'function', 'Bundled template resources must load');
            equal(document.title, titleFixture.expected, 'Initial title must be literal text');
            check(titleInjected === 0, 'Title payload executed script');
            check(document.getElementById('title-injected') === null, 'Title payload inserted an element');
            check(document.querySelectorAll('title').length === 1, 'Title payload inserted another title');
            check(document.querySelectorAll('script').length === 8, 'Title payload inserted a script element');

            const host = document.getElementById('title-host'), child = document.getElementById('title-child');
            const hostBounds = host.getBoundingClientRect.bind(host), childBounds = child.getBoundingClientRect.bind(child);
            host.getBoundingClientRect = () => { trace.push('bounds:host'); return hostBounds(); };
            child.getBoundingClientRect = () => { trace.push('bounds:child'); return childBounds(); };
            child.addEventListener('mousemove', event => {
                check(event.target === child && event.currentTarget === child, 'Distinct child target/currentTarget');
                trace.push('child');
            });
            document.addEventListener('mousemove', event => {
                check(event.target === child && event.currentTarget === document, 'Event must bubble to document');
                trace.push('document');
            }, { once: true });
            const event = new MouseEvent('mousemove', { bubbles: true, cancelable: true, clientX: 111, clientY: 83, buttons: 0 });
            check(child.dispatchEvent(event) && !event.defaultPrevented, 'Default action must remain uncancelled');
            equal(trace, ['child', 'receiver:host', 'before', 'bounds:host', 'send:host', 'after', 'document'], 'Actual receiver, geometry access and side-effect order');
            check(packets.length === 1, 'Exactly one generated mouse packet');
            equal(packets[0]?.args.map(JSON.parse), [0, { X: '71.0000000000', Y: '33.0000000000' }, { X: '200.0000000000', Y: '120.0000000000' }], 'Literal host-relative coordinates and dimensions');

            // Use actual Html.title boot/channel output, not a reimplementation of its assignment.
            const priorEffect = titleInjected, priorMarkup = document.querySelectorAll('title,script,#title-injected').length;
            new Function("const chDocumentTitle=aardvark.getChannel('dynamic-title','chDocumentTitle');" + titleFixture.dynamicBoot)();
            titleSocket.onmessage({ data: JSON.stringify({ targetId: 'dynamic-title', channel: 'chDocumentTitle', data: titleFixture.dynamicBatch }) });
            await aardvark.promise;
            equal(document.title, titleFixture.dynamicExpected, 'Dynamic title updates remain literal and are not pre-encoded');
            check(titleInjected === priorEffect && document.querySelectorAll('title,script,#title-injected').length === priorMarkup, 'Dynamic update must not introduce further markup or effects');
            failures.push(...scriptErrors.map(error => 'Script error: ' + error));
        } catch (error) {
            failures.push(String(error));
        }
        parent.postMessage({ kind: 'title-case', failures, packets }, '*');
    } else {
        const failures = [], packets = [];
        let completed = 0;
        for (const fixture of titleCases) {
            const frame = document.createElement('iframe');
            frame.width = '800'; frame.height = '600';
            const result = await new Promise(resolve => {
                const receive = event => {
                    if (event.source === frame.contentWindow && event.data?.kind === 'title-case') {
                        clearTimeout(timer);
                        removeEventListener('message', receive);
                        resolve(event.data);
                    }
                };
                const timer = setTimeout(() => {
                    removeEventListener('message', receive);
                    resolve({ failures: ['Incomplete isolated browser result'], packets: [] });
                }, 3000);
                addEventListener('message', receive);
                frame.src = fixture.url;
                document.body.append(frame);
            });
            completed++;
            failures.push(...result.failures.map(failure => fixture.name + ': ' + failure));
            packets.push(...result.packets);
            frame.remove();
        }
        const result = document.createElement('pre');
        result.id = 'title-results';
        result.textContent = JSON.stringify({ completed, failures, packets });
        document.body.append(result);
    }
})().catch(error => {
    const result = document.createElement('pre');
    result.id = 'title-results';
    result.textContent = JSON.stringify({ completed: -1, failures: [String(error)], packets: [] });
    document.body.append(result);
});
