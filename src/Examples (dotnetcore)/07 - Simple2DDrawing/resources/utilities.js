const findAncestor = function (el, cls) {
    if (el.classList.contains(cls)) return el;
    while ((el = el.parentElement) && !el.classList.contains(cls));
    return el;
}

function getCursor(evt, containerClassName) {
    var source = evt.target || evt.srcElement;
    var svg = findAncestor(source, containerClassName);
    if (svg) {
        var pt = svg.createSVGPoint();
        pt.x = evt.clientX;
        pt.y = evt.clientY;
        return pt.matrixTransform(svg.getScreenCTM().inverse());
    } else {
        return { x: NaN, y: NaN };
    }
}