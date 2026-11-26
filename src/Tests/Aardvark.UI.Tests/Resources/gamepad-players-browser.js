// Real resource, polling and generated handlers. Only device snapshots, frame scheduling
// and WebSocket transport are controlled; expected notifications are explicit lifecycle inputs.
(function () {
    const failures = [], output = { controls: [] };
    const check = (ok, message) => { if (!ok) failures.push(message); };
    const equal = (a, b, message) => check(JSON.stringify(a) === JSON.stringify(b), message + ': ' + JSON.stringify(a));
    const host = document.getElementById('gamepad-host'), alternate = document.getElementById('gamepad-alternate');
    const zero = control => control.endsWith('stick') ? [0, 0] : [0];
    const notification = (control, player, values, sender = 'gamepad-host') => ({ control, player, values, sender });
    const device = (player, values = {}) => {
        const buttons = Array.from({ length: 17 }, () => ({ pressed: false, value: 0 }));
        for (const [key, index] of [['leftshoulder', 6], ['rightshoulder', 7]]) {
            if (values[key]) buttons[index] = { pressed: values[key][0] !== 0, value: values[key][0] };
        }
        return { index: player, connected: true, buttons, axes: [...(values.leftstick || [0, 0]), ...(values.rightstick || [0, 0])] };
    };
    window.pollGamepadFrames = count => {
        for (let i = 0; i < count; i++) {
            const callback = nextGamepadFrame;
            nextGamepadFrame = null;
            if (!callback) throw new Error('Polling stopped');
            callback(i * 16);
        }
    };
    const stage = (list, phase, values, frames, expected) => {
        window.snapshots = values;
        const polls = gamepadPolls, scheduled = scheduledFrames;
        pollGamepadFrames(frames);
        check(gamepadPolls - polls === frames && scheduledFrames - scheduled === frames && nextGamepadFrame !== null, phase + ': polling cadence');
        const actual = packets.splice(0);
        equal(trace.splice(0), actual.flatMap(p => ['before:' + p.sender, 'send:' + p.sender, 'after:' + p.sender]), phase + ': receiver and dispatch side-effect order');
        list.push({ phase, expected, packets: actual });
    };
    const snapshot = (control, pairs) => {
        const values = [null, null, null, null];
        for (const [player, value] of pairs) values[player] = device(player, { [control]: value });
        return values;
    };
    try {
        aardvark.connect('events');
        gamepadSocket.onopen();
        host.focus();
        check(document.activeElement === host, 'Initial focus');
        for (const control of ['leftstick', 'rightstick', 'leftshoulder', 'rightshoulder']) {
            const list = output[control] = [], stick = control.endsWith('stick');
            const value = stick ? [0.65, -0.75] : [0.6], changed = stick ? [-0.8, 0.55] : [0.8];
            const players = [0, 1, 2, 3];
            const all = players.map(p => [p, value]);
            const send = pairs => pairs.map(([p, v]) => notification(control, p, v));
            const step = (phase, pairs, frames, expected = []) => stage(list, control + ': ' + phase, snapshot(control, pairs), frames, send(expected));
            const neutral = players.map(p => [p, zero(control)]);
            // Initial -1 stick sentinel and trigger release before any hold retain old behavior.
            const initial = players.map(p => [p, stick ? [-1, -1] : value]);
            step('initial press', initial, 1);
            if (stick) step('initial sentinel hold', initial, 1);
            step('initial neutral release', neutral, 1, neutral);
            step('equal simultaneous press', all, 1);
            step('equal simultaneous hold', all, 1, all);
            step('equal unchanged holds', all, 16);
            step('equal simultaneous release', neutral, 1, neutral);
            step('neutral unchanged polls', neutral, 16);
            step('identical repress', all, 1);
            step('identical reengagement', all, 1, all);
            const oneChanged = all.map(([p, v]) => [p, p === 2 ? changed : v]);
            step('only player2 changes', oneChanged, 1, [[2, changed]]);
            step('only player1 releases', oneChanged.map(([p, v]) => [p, p === 1 ? zero(control) : v]), 1, [[1, zero(control)]]);
            step('player1 repress', oneChanged, 1);
            step('player1 reengagement', oneChanged, 1, [[1, value]]);
            step('all final release', neutral, 1, neutral);
            const sparse = [[1, value], [3, changed]], sparseZero = [[1, zero(control)], [3, zero(control)]];
            step('sparse distinct press', sparse, 1);
            step('sparse distinct hold', sparse, 1, sparse);
            step('sparse distinct unchanged polls', sparse, 256);
            step('sparse simultaneous neutral release', sparseZero, 1, sparseZero);
            for (const direction of (stick ? [[0.75, 0], [-0.75, 0], [0, 0.8], [0, -0.8], [0.6, -0.8], [-0.6, 0.8]] : [[0.6], [0.8]])) {
                const single = [[3, direction]], released = [[3, zero(control)]];
                step('single press ' + direction, single, 1);
                step('single hold ' + direction, single, 1, single);
                step('single unchanged ' + direction, single, 16);
                step('single release ' + direction, released, 1, released);
                step('single neutral ' + direction, released, 16);
                step('single identical repress ' + direction, single, 1);
                step('single identical reengagement ' + direction, single, 1, single);
                step('single final release ' + direction, released, 1, released);
            }
        }
        const controls = output.controls;
        stage(controls, 'dead zone boundary', [device(0, { leftstick: [0.3, -0.3], rightstick: [-0.3, 0.3] }), null, null, null], 4, []);
        stage(controls, 'dead zone is component-wise', [device(0, { leftstick: [0.29, 0.29] }), null, null, null], 4, []);
        stage(controls, 'idle single player', [device(0), null, null, null], 256, []);
        const both = [device(0, { leftstick: [0.6, -0.8], rightstick: [-0.7, 0.5], leftshoulder: [0.6], rightshoulder: [0.8] }), null, null, null];
        stage(controls, 'combined press', both, 1, []);
        const combined = [['leftshoulder', [0.6]], ['rightshoulder', [0.8]], ['leftstick', [0.6, -0.8]], ['rightstick', [-0.7, 0.5]]];
        stage(controls, 'buttons precede axes', both, 1, combined.map(([c, v]) => notification(c, 0, v)));
        stage(controls, 'combined unchanged holds', both, 256, []);
        stage(controls, 'combined release', [device(0), null, null, null], 1, combined.map(([c]) => notification(c, 0, zero(c))));
        const focused = snapshot('rightshoulder', [[3, [0.6]]]);
        stage(controls, 'focus press', focused, 1, []);
        alternate.focus();
        stage(controls, 'focused alternate hold', focused, 1, [notification('rightshoulder', 3, [0.6], 'gamepad-alternate')]);
        host.focus();
        stage(controls, 'focus change does not synthesize analog change', focused, 1, []);
        // Preserve existing disconnect semantics: no new neutral event or cache reset.
        stage(controls, 'disconnect without neutral synthesis', [null, null, null, null], 1, []);
        stage(controls, 'reconnect press', focused, 1, []);
        stage(controls, 'reconnect same cached hold', focused, 1, []);
        window.rebound = 0;
        const name = 'gp_rightshoulder_changed_3', previous = host.getAttribute(name);
        host.setAttribute(name, previous + ';window.rebound++;');
        stage(controls, 'changed attribute rebound', snapshot('rightshoulder', [[3, [0.8]]]), 1, [notification('rightshoulder', 3, [0.8])]);
        check(rebound === 1, 'Changed handler attribute must be rebound');
        host.setAttribute(name, previous);
        stage(controls, 'focused neutral release', snapshot('rightshoulder', [[3, [0]]]), 1, [notification('rightshoulder', 3, [0])]);

        const child = document.getElementById('gamepad-child');
        const hostBounds = host.getBoundingClientRect.bind(host), childBounds = child.getBoundingClientRect.bind(child);
        host.getBoundingClientRect = () => { trace.push('bounds:host'); return hostBounds(); };
        child.getBoundingClientRect = () => { trace.push('bounds:child'); return childBounds(); };
        child.addEventListener('mousemove', e => { check(e.target === child && e.currentTarget === child, 'Child receiver'); trace.push('child'); });
        document.addEventListener('mousemove', e => { check(e.target === child && e.currentTarget === document, 'Document receiver'); trace.push('document'); }, { once: true });
        const event = new MouseEvent('mousemove', { bubbles: true, cancelable: true, clientX: 111, clientY: 83 });
        check(child.dispatchEvent(event) && !event.defaultPrevented, 'Native default action remains uncancelled');
        equal(trace, ['child', 'receiver:host', 'before:gamepad-host', 'bounds:host', 'send:gamepad-host', 'after:gamepad-host', 'document'], 'Distinct-child receiver, geometry and side-effect ordering');
        equal(packets[0]?.args.map(JSON.parse), [0, { X: '71.0000000000', Y: '33.0000000000' }, { X: '200.0000000000', Y: '120.0000000000' }], 'Host-relative coordinates');
    } catch (error) {
        failures.push(String(error));
    }
    failures.push(...scriptErrors);
    output.failures = failures;
    output.bubbling = packets;
    const result = document.createElement('pre');
    result.id = 'gamepad-results';
    result.textContent = JSON.stringify(output);
    document.body.append(result);
})();
