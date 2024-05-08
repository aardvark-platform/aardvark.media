if (!aardvark.dropdown) {
    /**
     * @param {*} x
     * @param {*} y
     */
    function arraysIdentical(x, y) {
        const a = Array.isArray(x) ? x : [x];
        const b = Array.isArray(y) ? y : [y];
        let i = a.length;
        if (i != b.length) return false;
        while (i--) {
            if (a[i] !== b[i]) return false;
        }
        return true;
    };

    /**
     * @param {HTMLElement[]} $self
     * @param {string} trigger
     * @param {{onmessage: function}} channel
    */
    aardvark.dropdown = function ($self, trigger, channel) {
        $self.dropdown({
            on: trigger,
            onChange: function (value) { aardvark.processEvent($self[0].id, 'data-event', value); }
        });

        channel.onmessage = function(values) {
            const curr = $self.dropdown('get values');

            // Prevent resetting the same values (leads to flickering)
            if (arraysIdentical(curr, values)) {
                return;
            }

            $self.dropdown('clear', true);
            $self.dropdown('set selected', values, true);  // set exactly bugged? clear seems to trigger event
        };
    };
}