// example mostly copied from here: http://www.codedread.com/blog/archives/2005/12/21/how-to-enable-dragging-in-svg/
var bMouseDragging = false;
var nMouseOffsetX = 0;
var nMouseOffsetY = 0;

function getElementByClazz(clazz)
{
    var o = document.getElementsByClassName(clazz);
    if(o && o.length === 1) return o[0];
    else{
        debugger;
        return undefined;
    }
}

var draggingElement = null;
var nMouseOffsetX = 0;
var nMouseOffsetY = 0;

function mouseDown(documentElement, evt) {
    var target = evt.currentTarget;
    draggingElement = target;

    if (target) {
        var p = documentElement.createSVGPoint();
        p.x = evt.clientX;
        p.y = evt.clientY;

        var m = getScreenCTM(documentElement);

        p = p.matrixTransform(m.inverse());

        //if (!target.getAttribute("dragx")) target.setAttribute("dragx", "0");
        //if (!target.getAttribute("dragy")) target.setAttribute("dragy", "0");

        nMouseOffsetX = p.x;// - parseInt(target.getAttribute("dragx"));
        nMouseOffsetY = p.y;// - parseInt(target.getAttribute("dragy"));
    }
}

// Following is from Holger Will since ASV3 and O9 do not support getScreenTCM()
// See http://groups.yahoo.com/group/svg-developers/message/50789
function getScreenCTM(doc) {
    if (doc.getScreenCTM) { return doc.getScreenCTM(); }

    var root = doc
    var sCTM = root.createSVGMatrix()

    var tr = root.createSVGMatrix()
    var par = root.getAttribute("preserveAspectRatio")
    if (par == null || par == "") par = "xMidYMid meet"//setting to default value
    parX = par.substring(0, 4) //xMin;xMid;xMax
    parY = par.substring(4, 8)//YMin;YMid;YMax;
    ma = par.split(" ")
    mos = ma[1] //meet;slice

    //get dimensions of the viewport
    sCTM.a = 1
    sCTM.d = 1
    sCTM.e = 0
    sCTM.f = 0


    w = root.getAttribute("width")
    if (w == null || w == "") w = innerWidth

    h = root.getAttribute("height")
    if (h == null || h == "") h = innerHeight

    // Jeff Schiller:  Modified to account for percentages - I'm not 
    // absolutely certain this is correct but it works for 100%/100%
    if (w.substr(w.length - 1, 1) == "%") {
        w = (parseFloat(w.substr(0, w.length - 1)) / 100.0) * innerWidth;
    }
    if (h.substr(h.length - 1, 1) == "%") {
        h = (parseFloat(h.substr(0, h.length - 1)) / 100.0) * innerHeight;
    }

    // get the ViewBox
    vba = root.getAttribute("viewBox")
    if (vba == null) vba = "0 0 " + w + " " + h
    var vb = vba.split(" ")//get the viewBox into an array

    //--------------------------------------------------------------------------
    //create a matrix with current user transformation
    tr.a = root.currentScale
    tr.d = root.currentScale
    tr.e = root.currentTranslate.x
    tr.f = root.currentTranslate.y


    //scale factors
    sx = w / vb[2]
    sy = h / vb[3]


    //meetOrSlice
    if (mos == "slice") {
        s = (sx > sy ? sx : sy)
    } else {
        s = (sx < sy ? sx : sy)
    }

    //preserveAspectRatio="none"
    if (par == "none") {
        sCTM.a = sx//scaleX
        sCTM.d = sy//scaleY
        sCTM.e = - vb[0] * sx //translateX
        sCTM.f = - vb[0] * sy //translateY
        sCTM = tr.multiply(sCTM)//taking user transformations into acount

        return sCTM
    }


    sCTM.a = s //scaleX
    sCTM.d = s//scaleY
    //-------------------------------------------------------
    switch (parX) {
        case "xMid":
            sCTM.e = ((w - vb[2] * s) / 2) - vb[0] * s //translateX

            break;
        case "xMin":
            sCTM.e = - vb[0] * s//translateX
            break;
        case "xMax":
            sCTM.e = (w - vb[2] * s) - vb[0] * s //translateX
            break;
    }
    //------------------------------------------------------------
    switch (parY) {
        case "YMid":
            sCTM.f = (h - vb[3] * s) / 2 - vb[1] * s //translateY
            break;
        case "YMin":
            sCTM.f = - vb[1] * s//translateY
            break;
        case "YMax":
            sCTM.f = (h - vb[3] * s) - vb[1] * s //translateY
            break;
    }
    sCTM = tr.multiply(sCTM)//taking user transformations into acount

    return sCTM
}

function mouseUp(documentElement, evt) {
    if (draggingElement) {

        var p = documentElement.createSVGPoint();
        p.x = evt.clientX;
        p.y = evt.clientY;

        var m = getScreenCTM(documentElement);

        p = p.matrixTransform(m.inverse());
        p.x -= nMouseOffsetX;
        p.y -= nMouseOffsetY;
        var arg = { absolutePosition: toFixedV2d(p), delta: toFixedV2d(p) };
        aardvark.processEvent(draggingElement.id, 'onendrag', arg);
    }
    draggingElement = null;
    nMouseOffsetX = 0;
    nMouseOffsetY = 0;
}
function mouseMove(documentElement,evt) {
    if (draggingElement) {

        var p = documentElement.createSVGPoint();
        p.x = evt.clientX;
        p.y = evt.clientY;

        var m = getScreenCTM(documentElement);

        p = p.matrixTransform(m.inverse());
        p.x -= nMouseOffsetX;
        p.y -= nMouseOffsetY;
        var arg = { absolutePosition: toFixedV2d(p), delta: toFixedV2d(p) };
        aardvark.processEvent(draggingElement.id, 'ondrag', arg);
        //draggingElement.setAttribute("dragx", p.x);
        //draggingElement.setAttribute("dragy", p.y);
        //draggingElement.setAttribute("transform", "translate(" + p.x + "," + p.y + ")");
    }
} 

function draggable(containerClazz, svgElement) {
    var parent = getElementByClazz(containerClazz);
    if (svgElement && parent) {
        parent.addEventListener("mousemove", function (ev) { mouseMove(parent, ev); }, false);
        parent.addEventListener("mouseup", function (ev) { mouseUp(parent, ev); }, false);
        svgElement.addEventListener("mousedown", function (ev) { mouseDown(parent, ev); }, false);
        svgElement.addEventListener("mouseup", function (ev) { mouseUp(parent, ev); }, false);
        svgElement.addEventListener("mousemove", function (ev) { mouseMove(parent, ev); }, false);
    }
    else {
        debugger;
        console.warn("could not attach draggable.");
    }
}

