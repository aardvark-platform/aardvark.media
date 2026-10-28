// Actual gamepad polling and generated handlers; only device snapshots, frame scheduling
// and WebSocket transport are controlled by this fixture.
(function () {
    const failures = [], left = [], right = [], controls = [];
    const check = (condition, message) => { if (!condition) failures.push(message); };
    const equal = (actual, expected, message) => check(JSON.stringify(actual) === JSON.stringify(expected), message + ': ' + JSON.stringify(actual));
    const zero = [0, 0];
    const directions = [[0.75, 0], [-0.75, 0], [0, 0.8], [0, -0.8], [0.6, -0.8], [-0.6, 0.8]];
    try {
        aardvark.connect('events');
        gamepadSocket.onopen();
        const host = document.getElementById('gamepad-host');
        host.focus();
        check(document.activeElement === host, 'Gamepad receiver must have focus');
        // One callback per frame, including idle frames; never call Gamepad.trigger directly.
        window.pollGamepads = (count, axes) => {
            gamepadSnapshot.axes = axes;
            for (let i = 0; i < count; i++) {
                const callback = nextGamepadFrame;
                nextGamepadFrame = null;
                if (!callback) throw new Error('Polling stopped');
                callback(i * 16);
            }
        };
        const stage = (list, phase, frames, axes, expectedLeft, expectedRight) => {
            const polls = gamepadPolls, scheduled = scheduledFrames;
            pollGamepads(frames, axes);
            check(gamepadPolls - polls === frames && scheduledFrames - scheduled === frames && nextGamepadFrame !== null, phase + ': changed polling cadence');
            list.push({ phase, left: expectedLeft, right: expectedRight, packets: packets.splice(0) });
        };
        const axes = (stick, value) => stick === 'left' ? [...value, 0, 0] : [0, 0, ...value];
        for (const stick of ['left', 'right']) {
            for (const direction of directions) {
                const stages = [], name = stick + ':' + direction.join(',');
                const step = (phase, frames, input, expected) => stage(stages, phase, frames, axes(stick, input), stick === 'left' ? expected : zero, stick === 'right' ? expected : zero);
                step('neutral', 1, zero, zero);
                step('press', 1, direction, zero);
                step('engage', 1, direction, direction);
                step('hold', 16, direction, direction);
                step('release', 1, zero, zero);
                step('neutral hold', 16, zero, zero);
                step('repress', 1, direction, zero);
                step('reengage', 1, direction, direction);
                const changed = [-direction[1], direction[0]];
                step('changed', 1, changed, changed);
                step('changed hold', 16, changed, changed);
                step('final release', 1, zero, zero);
                (stick === 'left' ? left : right).push({ name, stages });
            }
        }
        // Threshold is component-wise and strict, not a radial dead zone.
        stage(controls, 'dead zone boundary', 4, [0.3, -0.3, -0.3, 0.3], zero, zero);
        stage(controls, 'dead zone diagonal', 4, [0.29, 0.29, -0.29, -0.29], zero, zero);
        stage(controls, 'idle polling', 256, [0, 0, 0, 0], zero, zero);
        const l = [0.30001, -0.7], r = [-0.5, 0.30001];
        stage(controls, 'both press', 1, [...l, ...r], zero, zero);
        stage(controls, 'both engage', 1, [...l, ...r], l, r);
        stage(controls, 'both held polling', 256, [...l, ...r], l, r);
        stage(controls, 'left release only', 1, [0.3, -0.3, ...r], zero, r);
        stage(controls, 'left repress only', 1, [...l, ...r], zero, r);
        stage(controls, 'left reengage only', 1, [...l, ...r], l, r);
        stage(controls, 'right release only', 1, [...l, -0.3, 0.3], l, zero);
        stage(controls, 'right repress only', 1, [...l, ...r], l, zero);
        stage(controls, 'right reengage only', 1, [...l, ...r], l, r);
        stage(controls, 'both release', 1, [0, 0, 0, 0], zero, zero);
        stage(controls, 'neutral polling after release', 256, [0, 0, 0, 0], zero, zero);

        const child = document.getElementById('gamepad-child');
        const hostBounds = host.getBoundingClientRect.bind(host), childBounds = child.getBoundingClientRect.bind(child);
        host.getBoundingClientRect = () => { trace.push('bounds:host'); return hostBounds(); };
        child.getBoundingClientRect = () => { trace.push('bounds:child'); return childBounds(); };
        child.addEventListener('mousemove', event => {
            check(event.target === child && event.currentTarget === child, 'Child receiver');
            trace.push('child');
        });
        document.addEventListener('mousemove', event => {
            check(event.target === child && event.currentTarget === document, 'Document bubbling');
            trace.push('document');
        }, { once: true });
        const event = new MouseEvent('mousemove', { bubbles: true, cancelable: true, clientX: 111, clientY: 83 });
        check(child.dispatchEvent(event) && !event.defaultPrevented, 'Default action must remain uncancelled');
        equal(trace, ['child', 'receiver:host', 'before', 'bounds:host', 'send:host', 'after', 'document'], 'Receiver, coordinates and side-effect order');
        equal(packets[0]?.args.map(JSON.parse), [0, { X: '71.0000000000', Y: '33.0000000000' }, { X: '200.0000000000', Y: '120.0000000000' }], 'Host-relative bubbling coordinates');
    } catch (error) {
        failures.push(String(error));
    }
    failures.push(...scriptErrors);
    const result = document.createElement('pre');
    result.id = 'gamepad-results';
    result.textContent = JSON.stringify({ left, right, controls, failures, bubbling: packets });
    document.body.append(result);
})();
