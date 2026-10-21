if (!aardvark.notifications) {
    aardvark.notifications = {
        toasts: new WeakMap()
    };

    /**
     * @param {HTMLElement} container
     * @param {int} id
     */
    const removeToast = function (container, id) {
        aardvark.processEvent(container.id, 'onremove', id);
        aardvark.notifications.toasts.get(container).delete(id);
    }

    /**
     * @param {HTMLElement} container
     * @param {{id: int}} data
     */
    aardvark.notifications.notify = function (container, data) {
        const $container = $(container);
        let toasts = aardvark.notifications.toasts.get(container);
        if (toasts === undefined) {
            toasts = new Map();
            aardvark.notifications.toasts.set(container, toasts);
        }

        if (data.data) {
            const params = Object.assign(data.data, { context: $container, onRemove: () => removeToast(container, data.id) });
            const toast = $.toast(params);
            toasts.set(data.id, toast);

        } else {
            const toast = toasts.get(data.id);
            if (toast !== undefined) {
                toast.toast('close');
                toasts.delete(data.id);
            }
        }
    };
}