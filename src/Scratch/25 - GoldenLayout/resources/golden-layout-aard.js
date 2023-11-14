if (!aardvark.golden) {
    aardvark.golden = {
        instances: new Map()
    };

    const onVirtualRecting = function (layoutElement, container, element, width, height) {
        const containerBoundingClientRect = container.element.getBoundingClientRect();
        const left = containerBoundingClientRect.left - layoutElement.boundingClientRect.left;
        const top = containerBoundingClientRect.top - layoutElement.boundingClientRect.top;
        element.style.left = `${left}px`;
        element.style.top = `${top}px`;
        element.style.width = `${width}px`;
        element.style.height = `${height}px`;
    }

    const onVisibilityChange = function (element, visible) {
        if (visible) {
            element.style.display = '';
        } else {
            element.style.display = 'none';
        }
    }

    const onVirtualZIndexChange = function (element, logicalZIndex, defaultZIndex) {
        element.style.zIndex = defaultZIndex;
    }

    const createInstance = function (layoutElement) {
        const layout = new goldenLayout.VirtualLayout(layoutElement);
        layout.resizeWithContainerAutomatically = true;
        layout.resizeDebounceExtendedWhenPossible = false;
        layout.resizeDebounceInterval = 10;

        const components = new Map();

        layout.beforeVirtualRectingEvent = function () {
            layoutElement.boundingClientRect = layoutElement.getBoundingClientRect();
        };


        layout.bindComponentEvent = function (container, itemConfig) {
            const componentTypeName = goldenLayout.ResolvedComponentItemConfig.resolveComponentTypeName(itemConfig);

            const element = document.createElement("iframe");
            element.src = './?page=' + componentTypeName;
            element.style.border = 'none';
            element.style.position = 'absolute';
            element.style.overflow = 'hidden';

            const component = {
                rootHtmlElement: element
            };

            container.virtualRectingRequiredEvent = function (container, width, height) {
                onVirtualRecting(layoutElement, container, element, width, height)
            };

            container.virtualVisibilityChangeRequiredEvent = function (container, visible) {
                onVisibilityChange(element, visible);
            };

            container.virtualZIndexChangeRequiredEvent = function (container, logicalZIndex, defaultZIndex) {
                onVirtualZIndexChange(element, logicalZIndex, defaultZIndex);
            };

            layoutElement.appendChild(element);
            components.set(container, component);

            return {
                component: component,
                virtual: true
            };
        };

        layout.unbindComponentEvent = function (container) {
            const component = components.get(container);
            if (component === undefined) {
                throw new Error('[GoldenAard] Component not found.');
            }

            const element = component.rootHtmlElement;
            if (element === undefined) {
                throw new Error('[GoldenAard] Component does not have a root HTML element.');
            }

            layoutElement.removeChild(element);
            components.delete(container);
        }

        const instance = {
            layout: layout,
            components: components
        };

        aardvark.golden.instances.set(layoutElement.id, instance);
        return instance;
    }

    aardvark.golden.createLayout = function (layoutElement, config) {
        var instance = aardvark.golden.instances.get(layoutElement.id);

        if (instance === undefined) {
            instance = createInstance(layoutElement);
        }

        instance.layout.loadLayout(config);
    }

    aardvark.golden.destroyLayout = function (layoutElement) {
        const instance = aardvark.golden.instances.get(layoutElement.id);

        if (instance !== undefined) {
            instance.layout.destroy();
            aardvark.golden.instances.delete(layoutElement.id);
        }
    }
}