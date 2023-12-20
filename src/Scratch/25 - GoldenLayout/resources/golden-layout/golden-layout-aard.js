if (!aardvark.golden) {
    aardvark.golden = {
        instances: new Map()
    };

    const onVirtualRecting = function (layoutElement, container, element, width, height) {
        if (layoutElement.boundingClientRect === undefined) {
            layoutElement.boundingClientRect = layoutElement.getBoundingClientRect();
        }

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
        const components = new Map();

        const onBindComponent = function (container, itemConfig) {
            const componentTypeName = goldenLayout.ResolvedComponentItemConfig.resolveComponentTypeName(itemConfig);

            const element = document.createElement("iframe");
            element.src = './?page=' + componentTypeName;
            element.style.border = 'none';
            element.style.position = 'absolute';
            element.style.overflow = 'hidden';
            element.classList.add('gl-aard-component');

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

        const onUnbindComponent = function (container) {
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
        };

        const layout = new goldenLayout.VirtualLayout(layoutElement, onBindComponent, onUnbindComponent);
        layout.resizeWithContainerAutomatically = true;
        layout.resizeDebounceExtendedWhenPossible = false;
        layout.resizeDebounceInterval = 10;

        layout.addEventListener('stateChanged', () => {
            aardvark.processEvent(layoutElement.id, 'onLayoutChanged');
        }, { passive: true });

        const instance = {
            layout: layout,
            components: components
        };

        return instance;
    }

    aardvark.golden.setLayout = function (layoutElement, config) {
        const instance = aardvark.golden.instances.get(layoutElement.id);
        instance.layout.closeAllOpenPopouts();
        instance.layout.loadLayout(config);
    }

    aardvark.golden.saveLayout = function (layoutElement, key) {
        const instance = aardvark.golden.instances.get(layoutElement.id);

        if (window.localStorage && key) {
            try {
                const savedLayout = instance.layout.saveLayout();
                window.localStorage.setItem(key, JSON.stringify(savedLayout));
            } catch (error) {
                console.error('Failed to save layout: ' + error);
            }
        }
    }

    aardvark.golden.loadLayout = function (layoutElement, key) {
        const instance = aardvark.golden.instances.get(layoutElement.id);

        if (window.localStorage && key) {
            const savedData = window.localStorage.getItem(key);

            if (savedData !== null) {
                try {
                    const savedLayout = JSON.parse(savedData);
                    const config = goldenLayout.LayoutConfig.fromResolved(savedLayout);
                    instance.layout.closeAllOpenPopouts();
                    instance.layout.loadLayout(config);
                } catch (error) {
                    console.error('Failed to load layout: ' + error);
                }
            }
        }
    }

    aardvark.golden.createLayout = function (layoutElement, config) {
        var instance = aardvark.golden.instances.get(layoutElement.id);

        if (instance === undefined) {
            instance = createInstance(layoutElement);
            aardvark.golden.instances.set(layoutElement.id, instance);
        }

        if (config === undefined) {
            // Add dock button and change title since we have a popout.
            // Since we don't support popping out stacks, we just assume the root is a component.
            // Also there does not seem to be a proper way to get the root...
            // Related: https://github.com/golden-layout/golden-layout/issues/861
            instance.layout.checkAddDefaultPopinButton();
            document.title = instance.layout._constructorOrSubWindowLayoutConfig.root.title;
        } else {
            instance.layout.loadLayout(config);
        }
    }

    aardvark.golden.destroyLayout = function (layoutElement) {
        const instance = aardvark.golden.instances.get(layoutElement.id);

        if (instance !== undefined) {
            instance.layout.destroy();
            aardvark.golden.instances.delete(layoutElement.id);
        }
    }
}