

function mkForce(element, graph) {
    console.log(graph);
    console.log(JSON.parse(graph));
    /**
     * Just a simple example to show how to use the sigma.layout.forceAtlas2
     * plugin:
     *
     * A random graph is generated, such that its nodes are separated in some
     * distinct clusters. Each cluster has its own color, and the density of
     * links is stronger inside the clusters. So, we expect the algorithm to
     * regroup the nodes of each cluster.
     */
    var i,
        s,
        o,
        N = 20,
        E = 15,
        C = 5,
        d = 0.5,
        cs = [],
        g = {
            nodes: [],
            edges: []
        };

    // Generate the graph:
    for (i = 0; i < C; i++)
        cs.push({
            id: i,
            nodes: [],
            color: '#' + (
              Math.floor(Math.random() * 16777215).toString(16) + '000000'
            ).substr(0, 6)
        });

    for (i = 0; i < N; i++) {
        o = cs[(Math.random() * C) | 0];
        g.nodes.push({
            id: 'n' + i,
            label: 'Node' + i,
            //x:  1 * Math.cos(2 * i * Math.PI / N),
            //y:  1 * Math.sin(2 * i * Math.PI / N),
            x: Math.random() * 10.0,
            y: Math.random() * 10.0,
            size: Math.random(),
            color: o.color
        });
        o.nodes.push('n' + i);
    }

    for (i = 0; i < E; i++) {
        if (Math.random() < 1 - d)
            g.edges.push({
                id: 'e' + i,
                source: 'n' + ((Math.random() * N) | 0),
                target: 'n' + ((Math.random() * N) | 0),
                color: '#ccc',
                size: 2,
                label: 'asdf',
                hover_color: '#000'
            });
        else {
            o = cs[(Math.random() * C) | 0]
            g.edges.push({
                id: 'e' + i,
                source: o.nodes[(Math.random() * o.nodes.length) | 0],
                target: o.nodes[(Math.random() * o.nodes.length) | 0],
                color: '#ccc',
                size: 2,
                label: 'asdf',
                hover_color: '#000'
            });
        }
    }

    s = new sigma({
        graph: g,
        container: 'graph-container',
        renderer: {
            container: element,
            type: 'canvas'
        },
        settings: {
            doubleClickEnabled: false,
            drawEdges: true,
            minEdgeSize: 0.5,
            maxEdgeSize: 4,
            enableEdgeHovering: true,
            edgeHoverColor: '#FFF',
            defaultEdgeHoverColor: '#000',
            edgeHoverSizeRatio: 1,
            edgeHoverExtremities: true,
            edgeLabelSize: 'fixed',
            defaultEdgeLabelSize: 20
        }
    });
    // Bind the events:
    s.bind('overNode outNode clickNode doubleClickNode rightClickNode', function (e) {
        console.log(e.type, e.data.node.label, e.data.captor);
    });
    s.bind('overEdge outEdge clickEdge doubleClickEdge rightClickEdge', function (e) {
        console.log(e.type, e.data.edge, e.data.captor);
    });
    s.bind('clickStage', function (e) {
        console.log(e.type, e.data.captor);
    });
    s.bind('doubleClickStage rightClickStage', function (e) {
        console.log(e.type, e.data.captor);
    });

    // Start the ForceAtlas2 algorithm:
    s.startForceAtlas2({ worker: true, barnesHutOptimize: true });
    setTimeout(function () { s.stopForceAtlas2(); }, 3000);
}

function demo() {
    /**
 * Just a simple example to show how to use the sigma.layout.forceAtlas2
 * plugin:
 *
 * A random graph is generated, such that its nodes are separated in some
 * distinct clusters. Each cluster has its own color, and the density of
 * links is stronger inside the clusters. So, we expect the algorithm to
 * regroup the nodes of each cluster.
 */
    var i,
        s,
        o,
        N = 20,
        E = 15,
        C = 5,
        d = 0.5,
        cs = [],
        g = {
            nodes: [],
            edges: []
        };

    // Generate the graph:
    for (i = 0; i < C; i++)
        cs.push({
            id: i,
            nodes: [],
            color: '#' + (
              Math.floor(Math.random() * 16777215).toString(16) + '000000'
            ).substr(0, 6)
        });

    for (i = 0; i < N; i++) {
        o = cs[(Math.random() * C) | 0];
        g.nodes.push({
            id: 'n' + i,
            label: 'Node' + i,
            //x:  1 * Math.cos(2 * i * Math.PI / N),
            //y:  1 * Math.sin(2 * i * Math.PI / N),
            x: Math.random() * 10.0,
            y: Math.random() * 10.0,
            size: Math.random(),
            color: o.color
        });
        o.nodes.push('n' + i);
    }

    for (i = 0; i < E; i++) {
        if (Math.random() < 1 - d)
            g.edges.push({
                id: 'e' + i,
                source: 'n' + ((Math.random() * N) | 0),
                target: 'n' + ((Math.random() * N) | 0),
                color: '#ccc',
                size: 2,
                label: 'asdf',
                hover_color: '#000'
            });
        else {
            o = cs[(Math.random() * C) | 0]
            g.edges.push({
                id: 'e' + i,
                source: o.nodes[(Math.random() * o.nodes.length) | 0],
                target: o.nodes[(Math.random() * o.nodes.length) | 0],
                color: '#ccc',
                size: 2,
                label: 'asdf',
                hover_color: '#000'
            });
        }
    }

    s = new sigma({
        graph: g,
        container: 'graph-container',
        renderer: {
            container: element,
            type: 'canvas'
        },
        settings: {
            doubleClickEnabled: false,
            drawEdges: true,
            minEdgeSize: 0.5,
            maxEdgeSize: 4,
            enableEdgeHovering: true,
            edgeHoverColor: '#FFF',
            defaultEdgeHoverColor: '#000',
            edgeHoverSizeRatio: 1,
            edgeHoverExtremities: true,
            edgeLabelSize: 'fixed',
            defaultEdgeLabelSize: 20
        }
    });
    // Bind the events:
    s.bind('overNode outNode clickNode doubleClickNode rightClickNode', function (e) {
        console.log(e.type, e.data.node.label, e.data.captor);
    });
    s.bind('overEdge outEdge clickEdge doubleClickEdge rightClickEdge', function (e) {
        console.log(e.type, e.data.edge, e.data.captor);
    });
    s.bind('clickStage', function (e) {
        console.log(e.type, e.data.captor);
    });
    s.bind('doubleClickStage rightClickStage', function (e) {
        console.log(e.type, e.data.captor);
    });

    // Start the ForceAtlas2 algorithm:
    s.startForceAtlas2({ worker: true, barnesHutOptimize: true });
    setTimeout(function () { s.stopForceAtlas2(); }, 3000);
}