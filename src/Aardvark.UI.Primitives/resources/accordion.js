if (!aardvark.accordion) {
    /**
     * @param {HTMLElement[]} $self
     * @param {HTMLElement} item
     * @param {string} event
    */
    const onToggle = function ($self, item, event) {
        const index = $self.children('.content').index(item);
        aardvark.processEvent($self[0].id, event, index);
    };

    /**
     * @param {HTMLElement[]} $self
     * @param {{cnt: int, value: int}} op
     */
    const processMessage = function ($self, op) {
        if (op.value < 0 || op.value >= $self.children('.content').length) {
            return;
        }

        if (op.cnt > 0) {
            $self.accordion('open', op.value);
        } else if (op.cnt < 0) {
            $self.accordion('close', op.value);
        }
    };

    /**
     * @param {HTMLElement[]} $self
     * @param {int} index
     */
    const processExclusiveMessage = function ($self, index) {
        if (index >= 0) {
            if (index < $self.children('.content').length) {
                $self.accordion('open', index);
            }
        } else {
            const $content = $self.children('.content');
            const active = $content.index($content.filter('.active'));

            if (active >= 0) {
                $self.accordion('close', active);
            }
        }
    };

    /**
     * @param {HTMLElement[]} $self
     * @param {boolean} exclusive
     * @param {{onmessage: function}} channel
    */
    aardvark.accordion = function ($self, exclusive, channel) {
        $self.accordion({
            exclusive: exclusive,
            onOpen: function () { onToggle($self, this, 'onopen'); },
            onClose: function () { onToggle($self, this, 'onclose'); }
        });

        if (exclusive) {
            channel.onmessage = (index) => processExclusiveMessage($self, index);
        } else {
            channel.onmessage = (op) => processMessage($self, op);
        }
    };
}