if (!aardvark.accordion) {
    /**
     * @param {HTMLElement[]} $self
     * @param {HTMLElement[]} $content
     * @param {HTMLElement} item
     * @param {string} event
    */
    const onToggle = function ($self, $content, item, event) {
        const index = $content.index(item);
        aardvark.processEvent($self[0].id, event, index);
    };

    /**
     * @param {HTMLElement[]} $self
     * @param {HTMLElement[]} $content
     * @param {{cnt: int, value: int}} op
     */
    const processMessage = function ($self, $content, op) {
        var index = op.value;

        // If we close a section in exclusive mode, we won't get
        // the index from Media. Instead we have to figure it out here.
        if (op.cnt < 0 && index < 0) {
            index = $content.index($content.filter('.active')); 
        }

        if (index >= 0 && index < $content.length) {
            if (op.cnt > 0) {
                $self.accordion('open', index);
            } else if (op.cnt < 0) {
                $self.accordion('close', index);
            }
        }
    };

    /**
     * @param {HTMLElement[]} $self
     * @param {boolean} exclusive
     * @param {{onmessage: function} | null} channel
    */
    aardvark.accordion = function ($self, exclusive, channel) {
        const $content = $self.children('.content');

        $self.accordion({
            exclusive: exclusive,
            onOpen: function () { onToggle($self, $content, this, 'onopen'); },
            onClose: function () { onToggle($self, $content, this, 'onclose'); }
        });

        if (channel) {
            channel.onmessage = (op) => processMessage($self, $content, op);
        }
    };
}