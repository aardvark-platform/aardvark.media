$(function(){
// using default options
  var f = function (evt, data) {
       if (!event.shiftKey) {
            var nodes = data.tree.getSelectedNodes();
            for (var i = 0; i < nodes.length; i++) {
                 nodes[i].setSelected(false);
            }
            data.node.setSelected(true);
            return true;
       } else {
            if (data.node.isSelected()) {
                 data.node.setSelected(false);
                 data.node.setActive(false, { noEvents: true, noFocus: true });
                 return false;
            }
            else {
                 data.node.setSelected(true);
                 return true;
            }
       }
  }


  $("#tree").fancytree(
       {
            // data.node.setActive(false, { noEvents: true, noFocus: true });
            //autoCollapse: true, // Automatically collapse all siblings, when a node is expanded
            //debugLevel: 2, // 0:quiet, 1:normal, 2:debug
            //focusOnSelect: true, // Set focus when node is checked by a mouse click
            //quicksearch: true, // Navigate to next node by typing the first letters
            //tabindex: "0", // Whole tree behaves as one single control

            extensions: ["dnd","childcounter"],
            dnd: {
                 preventVoidMoves: true,
                 preventRecursiveMoves: true,
                 autoExpandMS: 400,
                 dragStart: function (node, data) {
                      return true;
                 },
                 dragEnter: function (node, data) {
                      // return ["before", "after"];
                      return true;
                 },
                 dragDrop: function (node, data) {
                      data.otherNode.moveTo(node, data.hitMode);
                 }
            },
            childcounter: {
                 deep: true,
                 hideZeros: true,
                 hideExpanded: true
            },
            click: f,
            select: function (event, data) {
                var msg = { node : data.node.key, selected : data.node.isSelected() };
                var event = new CustomEvent("TreeViewSelected", { "detail": msg });
                var chan = aardvark.getChannel("n97", "tree");
                return true;
            }
       }
  );
});

function getNode(tree, ptr) {
    var node = undefined;

    switch (ptr.Case) {
        case "Root":
            node = tree.fancytree("getRootNode");
            break;

        case "Selected":
            var sel = tree.fancytree("getSelectedNodes");
            if (sel.length > 0)
                node = sel[0];
            break;
        case "Node":
            node = tree.fancytree("getTree").getNodeByKey(ptr.key);
            break;

        default:
            break;
    }

    return node;
}

function getMode(mode) {
    switch (mode.Case) {
        case "Before": mode = "before"; break;
        case "After": mode = "after"; break;
        case "FirstChild": mode = "firstChild"; break;
        case "LastChild": mode = "child"; break;
        default:
            console.warn("bad mode: " + m.Case);
            mode = "child";
            break;
    }

    return mode;
}


function emit(o) {
    var tree = $('#tree');

    if (o.Case === "Add") {
        o.node.icon = o.node.customIcon || true;
        delete o.node.customIcon;

        var node = getNode(tree, o.location.pointer);
        var mode = getMode(o.location.mode);

        if (node) {
            node.addNode(o.node, mode);
        }
        else {
            console.warn("non-existing node " + o.location.pointer);
        }
    }
    else if (o.Case === "Remove") {
        var node = getNode(tree, o.location);
        if (node) {
            node.remove();
        }
    }
    else if (o.Case === "Move") {
        var src = getNode(tree, o.source);
        var dst = getNode(tree, o.target.pointer);
        var dstMode = getMode(o.target.mode);
        src.moveTo(dst, dstMode);
    }
    else if (o.Case === "Select") {
        var n = getNode(tree, o.location);
        n.setActive(true);
    }
    else if (o.Case === "Deselect") {
        var n = getNode(tree, o.location);
        n.setActive(false);
        n.setFocus(false);
    }
}