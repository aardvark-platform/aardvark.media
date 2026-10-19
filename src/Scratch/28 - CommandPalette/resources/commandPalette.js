if (!aardvark.commandPalette) {

    /**
     * Fuzzy subsequence match (VS Code style). Returns null if the query does not match,
     * otherwise a score (higher is better) and the matched character positions.
     * @param {string} query
     * @param {string} text
     */
    function fuzzyMatch(query, text) {
        const q = query.toLowerCase();
        const t = text.toLowerCase();
        const positions = [];
        let score = 0;
        let ti = 0;
        let last = -2;

        for (let qi = 0; qi < q.length; qi++) {
            const c = q[qi];
            if (c === ' ') continue;

            const found = t.indexOf(c, ti);
            if (found < 0) return null;

            score += 1;
            if (found === last + 1) score += 5;                              // consecutive
            if (found === 0 || /[\s:\-_.]/.test(t[found - 1])) score += 8;   // word start
            score -= (found - ti) * 0.1;                                     // gap penalty

            positions.push(found);
            last = found;
            ti = found + 1;
        }

        return { score: score, positions: positions };
    }

    function escapeHtml(s) {
        return s.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
    }

    function highlight(text, positions) {
        const set = new Set(positions);
        let html = "";
        for (let i = 0; i < text.length; i++) {
            const c = escapeHtml(text[i]);
            html += set.has(i) ? `<b>${c}</b>` : c;
        }
        return html;
    }

    function labelOf(item) {
        return item.category ? `${item.category}: ${item.title}` : item.title;
    }

    /**
     * @param {HTMLElement} root - the overlay element
     * @param {{onmessage: function}} stepChannel - JSON string of { key, placeholder, items }
     * @param {{onmessage: function}} openChannel - bool
     */
    aardvark.commandPalette = function (root, stepChannel, openChannel) {
        const id = root.id;
        const input = root.querySelector('.cp-input');
        const list = root.querySelector('.cp-list');

        let step = { key: "", placeholder: "", items: [] };
        let visible = [];
        let selected = 0;
        let isOpen = false;
        let previousFocus = null;

        const send = function (name, ...args) {
            aardvark.processEvent(id, name, ...args);
        };

        const render = function () {
            const query = input.value.trim();

            if (query.length === 0) {
                visible = step.items.map(item => ({ item: item, positions: [] }));
            } else {
                visible = step.items
                    .map(item => {
                        const m = fuzzyMatch(query, labelOf(item));
                        return m ? { item: item, positions: m.positions, score: m.score } : null;
                    })
                    .filter(x => x)
                    .sort((a, b) => b.score - a.score);
            }

            selected = Math.max(0, Math.min(selected, visible.length - 1));

            list.innerHTML = "";

            if (visible.length === 0) {
                const empty = document.createElement('div');
                empty.className = 'cp-empty';
                empty.textContent = 'No matching commands';
                list.appendChild(empty);
                return;
            }

            visible.forEach((v, i) => {
                const el = document.createElement('div');
                el.className = 'cp-item' + (i === selected ? ' cp-selected' : '');

                const label = document.createElement('span');
                label.className = 'cp-label';
                label.innerHTML = highlight(labelOf(v.item), v.positions) + (v.item.hasChildren ? ' <span class="cp-more">›</span>' : '');

                const detail = document.createElement('span');
                detail.className = 'cp-detail';
                detail.textContent = v.item.detail;

                el.appendChild(label);
                el.appendChild(detail);

                el.addEventListener('mousemove', () => {
                    if (selected !== i) { selected = i; updateSelection(); }
                });
                el.addEventListener('mousedown', e => {
                    e.preventDefault(); // keep focus in the input
                    execute(i);
                });

                list.appendChild(el);
            });
        };

        const updateSelection = function () {
            const items = list.querySelectorAll('.cp-item');
            items.forEach((el, i) => el.classList.toggle('cp-selected', i === selected));
            if (items[selected]) items[selected].scrollIntoView({ block: 'nearest' });
        };

        const execute = function (i) {
            const v = visible[i];
            if (v) send('cp-execute', v.item.id);
        };

        const show = function () {
            if (!isOpen) previousFocus = document.activeElement;
            isOpen = true;
            root.classList.add('cp-open');
            input.value = "";
            selected = 0;
            render();
            input.focus();
        };

        const hide = function () {
            isOpen = false;
            root.classList.remove('cp-open');
            input.blur();
            if (previousFocus && previousFocus.focus) previousFocus.focus();
            previousFocus = null;
        };

        // Global shortcut: Ctrl+P / Ctrl+Shift+P / F1. Capture phase, so it wins over
        // render controls and the browser's print dialog.
        document.addEventListener('keydown', function (e) {
            const isPalette = (e.ctrlKey && !e.altKey && e.code === 'KeyP') || e.key === 'F1';
            if (!isPalette) return;

            e.preventDefault();
            e.stopPropagation();

            if (isOpen) input.focus();
            else send('cp-open');
        }, true);

        input.addEventListener('keydown', function (e) {
            switch (e.key) {
                case 'ArrowDown':
                    if (visible.length > 0) selected = (selected + 1) % visible.length;
                    updateSelection();
                    break;
                case 'ArrowUp':
                    if (visible.length > 0) selected = (selected - 1 + visible.length) % visible.length;
                    updateSelection();
                    break;
                case 'PageDown':
                    selected = Math.min(visible.length - 1, selected + 10);
                    updateSelection();
                    break;
                case 'PageUp':
                    selected = Math.max(0, selected - 10);
                    updateSelection();
                    break;
                case 'Enter':
                    execute(selected);
                    break;
                case 'Escape':
                    send('cp-close');
                    break;
                case 'Backspace':
                    if (input.value.length === 0 && step.key !== "") send('cp-back');
                    else return; // default behavior, stop propagation below
                    break;
                default:
                    e.stopPropagation(); // typing must not reach other key handlers
                    return;
            }
            e.preventDefault();
            e.stopPropagation();
        });

        input.addEventListener('input', function () {
            selected = 0;
            render();
        });

        // Clicking the backdrop closes the palette
        root.addEventListener('mousedown', function (e) {
            if (e.target === root) send('cp-close');
        });

        stepChannel.onmessage = function (json) {
            const next = JSON.parse(json);
            const stepChanged = next.key !== step.key;
            step = next;
            input.placeholder = step.placeholder;

            if (stepChanged) {
                input.value = "";
                selected = 0;
            }

            if (isOpen) {
                render();
                input.focus();
            }
        };

        openChannel.onmessage = function (open) {
            if (open) show(); else hide();
        };
    };
}
