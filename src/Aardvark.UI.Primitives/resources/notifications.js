if (!aardvark.notifications) {
    aardvark.notifications = {
        toasts: new Map()
    };

    /**
     * @param {HTMLElement} container
     * @param {int} id
     */
    const removeToast = function (container, id) {
        aardvark.processEvent(container.id, 'onremove', id);
        aardvark.notifications.toasts.delete(id);
    }

    /**
     * @param {HTMLElement} container
     * @param {{id: int}} data
     */
    aardvark.notifications.notify = function (container, data) {
        const $container = $(container);

        if (data.data) {
            const params = Object.assign(data.data, { context: $container, onRemove: () => removeToast(container, data.id) });
            const toast = $.toast(params);
            aardvark.notifications.toasts.set(data.id, toast);

        } else {
            const toast = aardvark.notifications.toasts.get(data.id);
            if (toast !== undefined) {
                toast.toast('close');
                aardvark.notifications.toasts.delete(data.id);
            }
        }
    };
}