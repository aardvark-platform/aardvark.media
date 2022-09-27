﻿
const myLayout = {
    root: {
        type: 'row',
        content: [
            {
                title: 'My Component 1',
                type: 'component',
                componentType: 'MyComponent',
                componentState: { id: "aaa" },
            },
            {
                title: 'My Component 2',
                type: 'component',
                componentType: 'MyComponent2',
                componentState: { id: "bbb" }
            }
        ]
    }
};

function initLayout() {
    //var layoutElement = document.querySelector('.layoutContainer');
    //var layout = new goldenLayout.GoldenLayout(layoutElement); // assign element by query
    var layout = new goldenLayout.GoldenLayout();   // default element body

    layout.registerComponentConstructor( 'MyComponent', function( container, componentState ){
        this.rootHtmlElement = document.getElementsByClassName("test")[0];
    }, true);

    layout.registerComponentConstructor('MyComponent2', function (container, componentState) {
        this.rootHtmlElement = document.getElementsByClassName("test2")[0];
    }, true);

    layout.loadLayout(myLayout);
}

aardvark.initLayout = initLayout;

//function oldLayout() {
    //var layout = new GoldenLayout({
    //    settings:{
    //        hasHeaders: true,
    //        constrainDragToContainer: true,
    //        reorderEnabled: true,
    //        selectionEnabled: false,
    //        popoutWholeStack: false,
    //        blockedPopoutsThrowError: true,
    //        closePopoutsOnUnload: true,
    //        showPopoutIcon: true,
    //        showMaximiseIcon: true,
    //        showCloseIcon: true
    //    },
    //    dimensions: {
    //        borderWidth: 5,
    //        minItemHeight: 10,
    //        minItemWidth: 10,
    //        headerHeight: 20,
    //        dragProxyWidth: 300,
    //        dragProxyHeight: 200
    //    },
    //    labels: {
    //        close: 'close',
    //        maximise: 'maximise',
    //        minimise: 'minimise',
    //        popout: 'open in new window'
    //    },
    //    content: [{
    //    type: 'row',
    //    content:[{
    //        type: 'component',
    //        componentName: 'testComponent',
    //        componentState: { label: 'A' }
    //    },{
    //        type: 'column',
    //        content:[{
    //            type: 'component',
    //            componentName: 'testComponent',
    //            componentState: { label: 'B' }
    //        },{
    //            type: 'component',
    //            componentName: 'testComponent',
    //            componentState: { label: 'C' }
    //        }]
    //    }]
    //}]
    //});

    //layout.registerComponent( 'testComponent', function( container, componentState ){
    //    container.getElement().html("<iframe src='./?page=render' name='SELFHTML_in_a_box' style='border:0;width:100%;height:100%'>");
    //});

    //layout.init();
//}