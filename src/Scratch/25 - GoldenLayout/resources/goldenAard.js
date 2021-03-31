function initLayout() {
    var layout = new GoldenLayout({
        settings:{
            hasHeaders: true,
            constrainDragToContainer: true,
            reorderEnabled: true,
            selectionEnabled: false,
            popoutWholeStack: false,
            blockedPopoutsThrowError: true,
            closePopoutsOnUnload: true,
            showPopoutIcon: true,
            showMaximiseIcon: true,
            showCloseIcon: true
        },
        dimensions: {
            borderWidth: 5,
            minItemHeight: 10,
            minItemWidth: 10,
            headerHeight: 20,
            dragProxyWidth: 300,
            dragProxyHeight: 200
        },
        labels: {
            close: 'close',
            maximise: 'maximise',
            minimise: 'minimise',
            popout: 'open in new window'
        },
        content: [{
        type: 'row',
        content:[{
            type: 'component',
            componentName: 'testComponent',
            componentState: { label: 'A' }
        },{
            type: 'column',
            content:[{
                type: 'component',
                componentName: 'testComponent',
                componentState: { label: 'B' }
            },{
                type: 'component',
                componentName: 'testComponent',
                componentState: { label: 'C' }
            }]
        }]
    }]
    });

    layout.registerComponent( 'testComponent', function( container, componentState ){
        container.getElement().html("<iframe src='./?page=render' width='900' height='400' name='SELFHTML_in_a_box'>");
    });

    layout.init();
}

aardvark.initLayout = initLayout;