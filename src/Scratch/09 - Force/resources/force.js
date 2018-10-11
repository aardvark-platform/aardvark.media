// taken from: https://github.com/anvaka/VivaGraphJS/blob/master/demos/other/customLinkLength.html

function demo(element, graph) {
    console.log(graph);
    console.log(JSON.parse(graph));
    /*global Viva*/

    // Let's construct simple graph:
    // 0 -> 1 -> 2 -> 3 -> 4 -> 5
    var graph = Viva.Graph.graph();
    graph.addLink(0, 1, { connectionStrength: 0.1 });
    graph.addLink(1, 2, { connectionStrength: 0.4 });
    graph.addLink(2, 3, { connectionStrength: 0.9 });
    graph.addLink(3, 4, { connectionStrength: 0.9 });
    graph.addLink(4, 5, { connectionStrength: 0.4 });
    graph.addLink(5, 6, { connectionStrength: 0.1 });
    // let's pin middle node:
    var middle = graph.getNode(3);
    middle.isPinned = true;
    var idealLength = 90;
    var layout = Viva.Graph.Layout.forceDirected(graph, {
        springLength: idealLength,
        springCoeff: 0.0008,
        gravity: -10,
        // This is the main part of this example. We are telling force directed
        // layout, that we want to change length of each physical spring
        // by overriding `springTransform` method:
        springTransform: function (link, spring) {
            spring.length = idealLength * (1 - link.data.connectionStrength);
        }
    });
    // let's pin middle node:
    var middle = graph.getNode(3);
    layout.pinNode(middle, true);
    var renderer = Viva.Graph.View.renderer(graph, {
        layout: layout,
        container: element
    });
    renderer.run();

}

function mkForce(element, graphJSON) {
    var inGraph = JSON.parse(graphJSON);

    debugger;
    var graph = Viva.Graph.graph();
    inGraph.edges.forEach(function (entry) {
        graph.addLink(entry.fromId, entry.toId, { connectionStrength: entry.weight })
    });

    var idealLength = 90;
    var layout = Viva.Graph.Layout.forceDirected(graph, {
        springLength: idealLength,
        springCoeff: 0.0008,
        gravity: -10,
        // This is the main part of this example. We are telling force directed
        // layout, that we want to change length of each physical spring
        // by overriding `springTransform` method:
        springTransform: function (link, spring) {
            spring.length = idealLength * (1 - link.data.connectionStrength);
        }
    });
    // let's pin middle node:
    //var middle = graph.getNode(0);
    //layout.pinNode(middle, true);
    var renderer = Viva.Graph.View.renderer(graph, {
        layout: layout,
        container: element
    });
    renderer.run();

}

