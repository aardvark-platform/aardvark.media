function findAncestor(el, cls) {
    if (el.classList.contains(cls)) return el;
    while ((el = el.parentElement) && !el.classList.contains(cls));
    return el;
}


function getCursor(evt) {
    var source = evt.target || evt.srcElement;
    var svg = findAncestor(source, "svgRoot");
    var pt = svg.createSVGPoint();
    pt.x = evt.clientX;
    pt.y = evt.clientY;
    return pt.matrixTransform(svg.getScreenCTM().inverse());
}