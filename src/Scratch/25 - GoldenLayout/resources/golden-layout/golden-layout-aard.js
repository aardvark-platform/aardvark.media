var aardvark = document.aardvark;

// Popouts do not load Aardvark itself, they just contain Golden Layout and some iframes.
if (!aardvark) {
    aardvark = {};
    document.aardvark = aardvark;
}

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

    const createInstance = function (layoutElement, isPopout) {
        const components = new Map();   // Currently bound components
        const elements = new Map();     // Elements to keep alive and hide if their component is unbound

        const onBindComponent = function (container, itemConfig) {
            const componentTypeName = goldenLayout.ResolvedComponentItemConfig.resolveComponentTypeName(itemConfig);

            var element = elements.get(componentTypeName);
            if (element === undefined) {
                element = document.createElement("iframe");
                element.src = './?page=' + componentTypeName;
                element.style.border = 'none';
                element.style.position = 'absolute';
                element.style.overflow = 'hidden';
                element.classList.add('gl-aard-component');
                element.dataset.keepAlive = itemConfig.componentState.keepAlive;

                if (itemConfig.componentState.keepAlive) {
                    elements.set(componentTypeName, element);
                }

                layoutElement.appendChild(element);

            } else {
                element.style.display = '';
            }

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

            if (element.dataset.keepAlive === 'true') {
                element.style.display = 'none';
            } else {
                layoutElement.removeChild(element);
            }

            components.delete(container);
        };

        const addLayoutChangedHandler = function (layout) {
            layout.addEventListener('stateChanged', () => {
                aardvark.processEvent(layoutElement.id, 'onLayoutChanged');
            }, { passive: true });
        };

        const layout = new goldenLayout.VirtualLayout(layoutElement, onBindComponent, onUnbindComponent);
        layout.resizeWithContainerAutomatically = true;
        layout.resizeDebounceExtendedWhenPossible = false;
        layout.resizeDebounceInterval = 10;

        // If we are running in Aardium, we want to move the window to the front
        // when dragging over it and focus it on drop.
        if (aardvark.moveWindowTop instanceof Function) {
            layout.moveWindowTop = aardvark.moveWindowTop;
        }

        if (aardvark.focusWindow instanceof Function) {
            layout.focusWindow = aardvark.focusWindow;
        }

        let titleObserver = null;

        const updatePopoutsTitle = function () {
            for (const popout of layout.openPopouts) {
                popout.getWindow().document.title = document.title;
            }
        };

        // Install layout changed event handlers
        // Popouts cannot call aardvark.processEvent so we have to handle that from the main window.
        if (!isPopout) {
            titleObserver = new MutationObserver(updatePopoutsTitle);
            titleObserver.observe(document.querySelector('title'), { subtree: true, characterData: true, childList: true });

            addLayoutChangedHandler(layout);

            layout.addEventListener('windowOpened', popout => {
                const inner = popout.getGlInstance();
                popout.getWindow().document.title = document.title;
                addLayoutChangedHandler(inner);
            });
        }

        const instance = {
            layout: layout,
            components: components,
            elements: elements,
            titleObserver: titleObserver
        };

        return instance;
    }

    aardvark.golden.setLayout = function (layoutElement, config) {
        const instance = aardvark.golden.instances.get(layoutElement.id);
        instance.layout.closeAllOpenPopouts(true);
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
                    instance.layout.closeAllOpenPopouts(true);
                    instance.layout.loadLayout(config);
                } catch (error) {
                    console.error('Failed to load layout: ' + error);
                }
            }
        }
    }

    aardvark.golden.createLayout = function (layoutElement, config) {
        var instance = aardvark.golden.instances.get(layoutElement.id);
        const isPopout = (config === undefined);

        if (instance === undefined) {
            instance = createInstance(layoutElement, isPopout);
            aardvark.golden.instances.set(layoutElement.id, instance);
        }

        if (isPopout) {
            instance.layout.checkAddDefaultPopinButton();
        } else {
            instance.layout.loadLayout(config);
        }
    }

    aardvark.golden.destroyLayout = function (layoutElement) {
        const instance = aardvark.golden.instances.get(layoutElement.id);

        if (instance !== undefined) {
            if (instance.titleObserver !== null) {
                instance.titleObserver.disconnect();
            }
            instance.layout.closeAllOpenPopouts(true);
            instance.layout.destroy();
            aardvark.golden.instances.delete(layoutElement.id);
        }
    }
}