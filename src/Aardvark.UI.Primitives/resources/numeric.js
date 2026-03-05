if (!aardvark.numeric) {
    const Property = Object.freeze({
        SetValue: Symbol(),
        Value:    Symbol(),
        IsHex:    Symbol()
    });

    const Regex = Object.freeze({
        InputFloat:            /^[+-]?(\d*\.\d*|\d*\.?\d*)([eE][+-]?\d*)?$/,
        InputInt:              /^([+-]?0[xX][0-9a-fA-F]*|[+-]?\d*)$/,
        NonDigit:              /\D/,
        TrailingDecimalZero:   /\.([0-9]*?)0+([eE][+\-0-9]*$)?$/,
        TrailingDecimalPoint:  /\.$/,
        TrailingDecimalPointE: /\.([eE])/
    });

    /**
     * @param {Number} value
     * @param {boolean} hexadecimal
     * @returns {string}
     */
    const formatInt = function (value, hexadecimal) {
        if (hexadecimal) {
            const sign = value < 0 ? '-' : '';
            const hex = Math.abs(value).toString(16).toUpperCase();
            return `${sign}0x${hex}`;
        } else {
            return value.toString();
        }
    }

    /**
     * @param {Number} value
     * @returns {string}
     */
    const formatFloat = function (value) {
        return value.toPrecision(15)
            .replace(Regex.TrailingDecimalZero, '.$1$2')
            .replace(Regex.TrailingDecimalPoint, '')
            .replace(Regex.TrailingDecimalPointE, '$1');
    }

    const Config = Object.freeze({
        int:   Object.freeze({ parse: parseInt,   format: formatInt,   inputPattern: Regex.InputInt }),
        float: Object.freeze({ parse: parseFloat, format: formatFloat, inputPattern: Regex.InputFloat })
    });

    /**
     * @param {HTMLElement[]} $self
     * @param {string | undefined} command
     * @param {any} value
     */
    aardvark.numeric = function ($self, command, value) {
        const input = $self.find('input')[0];

        if (!(Property.SetValue in input)) {
            const dataType = input.dataset.type;
            const config = Config[dataType];

            const smallStep = config.parse(input.dataset.smallStep);
            const largeStep = config.parse(input.dataset.largeStep);
            const minValue = config.parse(input.dataset.minValue);
            const maxValue = config.parse(input.dataset.maxValue);

            /**
             * @param {Number|string} value
             * @param {boolean} raiseEvent
             */
            const setValue = function (value, raiseEvent) {
                const isNum = typeof value === 'number';
                let parsed = isNum ? value : config.parse(value);

                if (isNaN(parsed)) {
                    parsed = input[Property.Value];
                } else {
                    // In case we have successfully parsed a string that did not come from a
                    // server message, we check if we have a hex number.
                    if (!isNum && raiseEvent && dataType === 'int') {
                        input[Property.IsHex] = Regex.NonDigit.test(value);
                    }
                    parsed = Math.max(minValue, Math.min(maxValue, parsed));
                }

                input.value = config.format(parsed, input[Property.IsHex]);

                if (parsed !== input[Property.Value]) {
                    input[Property.Value] = parsed;

                    if (raiseEvent) {
                        aardvark.processEvent($self[0].id, 'data-event', parsed);
                    }
                }
            }

            /**
             * @param {number} direction
             * @param {boolean} large
             */
            const stepValue = function (direction, large) {
                const amount = large ? largeStep : smallStep;
                setValue(input[Property.Value] + amount * direction, true);
            }

            /**
             * @param {WheelEvent} event
             */
            const onWheel = function (event) {
                const direction = -Math.sign(event.deltaY);
                stepValue(direction, event.shiftKey);
                event.preventDefault();
            }

            /**
             * @param {Event} event
             */
            const onChange = function (event) {
                setValue(event.target.value, true)
            }

            /**
             * @param {KeyboardEvent} event
             */
            const onKeyDown = function (event) {
                switch (event.key) {
                    case 'ArrowUp':   { stepValue(1, event.shiftKey); event.preventDefault(); break; }
                    case 'ArrowDown': { stepValue(-1, event.shiftKey); event.preventDefault(); break; }
                    case 'PageUp':    { stepValue(1, true); event.preventDefault(); break; }
                    case 'PageDown':  { stepValue(-1, true); event.preventDefault(); break; }
                    case 'Escape':    { setValue(input[Property.Value], true); break; }
                }
            }

            /**
             * @param {InputEvent} event
             */
            const onBeforeInput = function (event) {
                if (!event.data) return;

                const value = event.target.value;
                const start = event.target.selectionStart;
                const end = event.target.selectionEnd;
                const newValue = value.slice(0, start) + event.data + value.slice(end);

                if (!config.inputPattern.test(newValue.trim())) {
                    event.preventDefault();
                }
            }

            input[Property.Value] = 0;
            input[Property.IsHex] = false;
            input[Property.SetValue] = setValue;

            input.addEventListener('wheel', onWheel);
            input.addEventListener('change', onChange);
            input.addEventListener('keydown', onKeyDown);
            input.addEventListener('beforeinput', onBeforeInput);
        }

        if (command === 'set') {
            const setValue = input[Property.SetValue];
            setValue(value, false);
        }
    }
}