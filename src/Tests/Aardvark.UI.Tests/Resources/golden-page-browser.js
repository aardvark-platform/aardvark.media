// Real generated lifecycle code and bundled GoldenLayout; only WebSocket transport is captured.
(async function () {
    const failures = [], packets = [];
    let completed = 0;
    const check = (condition, message) => { if (!condition) failures.push(message); };
    const equal = (actual, expected, message) => check(JSON.stringify(actual) === JSON.stringify(expected), message + ': ' + JSON.stringify(actual));
    const wait = async (predicate, message) => {
        for (let i = 0; i < 300; i++) {
            if (predicate()) return;
            await new Promise(resolve => setTimeout(resolve, 10));
        }
        throw new Error(message);
    };
    if (window.goldenFixtures) {
        aardvark.connect('events');
        if (!fixtureSocket?.onopen) throw new Error('Actual client connection did not initialize');
        fixtureSocket.onopen();
        check(connections === 1, 'One isolated client connection');
        for (const fixture of goldenFixtures) {
            const context = expectedPath + ', ' + JSON.stringify(fixture.id) + ', keepAlive=' + fixture.keepAlive;
            const host = document.createElement('div');
            host.id = fixture.self;
            host.className = 'gl-aard-container';
            host.style.cssText = 'position:relative;width:720px;height:420px';
            document.getElementById('layouts').append(host);
            let instance;
            try {
                new Function(
                    "const channelSet=aardvark.getChannel('" + fixture.self + "','channelSet');" +
                    "const channelSave=aardvark.getChannel('" + fixture.self + "','channelSave');" +
                    "const channelLoad=aardvark.getChannel('" + fixture.self + "','channelLoad');" + fixture.boot)();
                instance = aardvark.golden.instances.get(host.id);
                check(instance?.layout instanceof goldenLayout.VirtualLayout, context + ': Actual VirtualLayout instance');
                check(instance.components.size === 1, context + ': One bound component');
                const first = Array.from(instance.components.values())[0].rootHtmlElement;
                await wait(() => first.contentWindow?.requestedPage, context + ': Initial iframe page did not load');
                const checkPage = element => {
                    const url = new URL(element.src), response = element.contentWindow.requestedPage;
                    equal(url.origin, location.origin, context + ': Unchanged origin');
                    equal(url.pathname, expectedPath, context + ': Unchanged application path');
                    equal(Array.from(url.searchParams.entries()), [['page', fixture.id]], context + ': Exact decoded URL page, no injected parameters');
                    equal(url.hash, '', context + ': No injected fragment');
                    equal(response.page, fixture.id, context + ': Actual HTTP page selection');
                    equal(response.path, expectedPath, context + ': Actual requested path');
                    equal(response.query, [{ name: 'page', values: [fixture.id] }], context + ': Actual request has only the page parameter');
                    check(response.known === true, context + ': Server selected a registered page');
                };
                checkPage(first);
                for (const container of instance.components.keys()) equal(container.componentType, fixture.id, context + ': Raw bound registry identity');
                equal(Array.from(instance.elements.keys()), fixture.keepAlive ? [fixture.id] : [], context + ': Raw keepAlive registry keys');
                const token = first.contentWindow.requestedPage.token;
                first.contentWindow.keepAliveProbe = fixture.id;

                const channel = aardvark.getChannel(fixture.self, 'channelSet');
                channel.received(fixture.emptyBatch);
                check(instance.components.size === 0, context + ': Empty layout unbinds component');
                if (fixture.keepAlive) {
                    check(first.isConnected && first.style.display === 'none', context + ': keepAlive hides without detaching');
                } else {
                    check(!first.isConnected, context + ': Disposable iframe is removed');
                }
                channel.received(fixture.restoreBatch);
                check(instance.components.size === 1, context + ': Restored component binds');
                const restored = Array.from(instance.components.values())[0].rootHtmlElement;
                await wait(() => restored.contentWindow?.requestedPage, context + ': Restored iframe page did not load');
                checkPage(restored);
                if (fixture.keepAlive) {
                    check(restored === first && restored.style.display !== 'none', context + ': Same iframe reused and shown');
                    equal(restored.contentWindow.requestedPage.token, token, context + ': Document was not reloaded');
                    equal(restored.contentWindow.keepAliveProbe, fixture.id, context + ': Iframe state survived');
                } else {
                    check(restored !== first && restored.contentWindow.requestedPage.token !== token, context + ': New iframe navigated again');
                }
            } catch (error) {
                failures.push(context + ': ' + String(error));
            } finally {
                try {
                    // Actual shutdown releases layout ownership; as in the updater, the parent removes its DOM.
                    new Function(fixture.shutdown)();
                    new Function(fixture.shutdown)();
                    check(!aardvark.golden.instances.has(host.id), context + ': Instance registry cleared');
                    if (instance) check(!instance.layout.isInitialised, context + ': Native layout destroyed');
                    const elements = Array.from(host.querySelectorAll('iframe'));
                    host.remove();
                    check(elements.every(element => !element.isConnected), context + ': Owner removal detaches remaining keepAlive DOM');
                } catch (error) { failures.push(context + ': Cleanup: ' + String(error)); host.remove(); }
                completed++;
            }
        }
        check(aardvark.golden.instances.size === 0 && document.querySelectorAll('iframe').length === 0, expectedPath + ': No remaining layouts or panel frames');

        const host = document.getElementById('receiver'), child = document.getElementById('child');
        const hostBounds = host.getBoundingClientRect.bind(host), childBounds = child.getBoundingClientRect.bind(child);
        host.getBoundingClientRect = () => { trace.push('bounds:parent'); return hostBounds(); };
        child.getBoundingClientRect = () => { trace.push('bounds:child'); return childBounds(); };
        child.addEventListener('mousemove', event => {
            check(event.currentTarget === child && event.target === child, 'Distinct child/currentTarget'); trace.push('child');
        });
        document.addEventListener('mousemove', event => {
            check(event.currentTarget === document && event.target === child, 'Event bubbles to document'); trace.push('document');
        }, { once: true });
        const event = new MouseEvent('mousemove', { bubbles: true, cancelable: true, clientX: 111, clientY: 83, buttons: 0 });
        check(child.dispatchEvent(event) && !event.defaultPrevented, 'Default action remains uncancelled');
        equal(trace, ['child', 'receiver:parent', 'before', 'bounds:parent', 'send:receiver', 'after', 'document'], 'Receiver, bounds source and side-effect order');
        const mousePackets = window.packets.filter(packet => packet.name === 'onmousemove');
        equal(mousePackets.map(packet => packet.args.map(JSON.parse)), [[0, { X: '71.0000000000', Y: '33.0000000000' }, { X: '200.0000000000', Y: '120.0000000000' }]], 'Literal host-relative coordinates and dimensions');
        failures.push(...scriptErrors.map(error => 'Script error: ' + error));
        parent.postMessage({ kind: 'golden-result', completed, failures, packets: mousePackets }, '*');
    } else {
        for (const path of goldenPaths) {
            const frame = document.createElement('iframe');
            frame.width = '900'; frame.height = '650';
            const result = await new Promise(resolve => {
                const receive = event => {
                    if (event.source === frame.contentWindow && event.data?.kind === 'golden-result') {
                        clearTimeout(timer); removeEventListener('message', receive); resolve(event.data);
                    }
                };
                const timer = setTimeout(() => {
                    removeEventListener('message', receive);
                    resolve({ completed: 0, failures: [path + ': Incomplete browser result'], packets: [] });
                }, 20000);
                addEventListener('message', receive);
                frame.src = path + '?fixture=1&outer=kept#host-fragment';
                document.body.append(frame);
            });
            completed += result.completed;
            failures.push(...result.failures); packets.push(...result.packets);
            frame.remove();
        }
        const result = document.createElement('pre');
        result.id = 'golden-results'; result.textContent = JSON.stringify({ completed, failures, packets });
        document.body.append(result);
    }
})().catch(error => {
    if (window.goldenFixtures) parent.postMessage({ kind: 'golden-result', completed: -1, failures: [String(error)], packets: [] }, '*');
    else {
        const result = document.createElement('pre');
        result.id = 'golden-results'; result.textContent = JSON.stringify({ completed: -1, failures: [String(error)], packets: [] });
        document.body.append(result);
    }
});
