relativeTo = function (event, container) {
    var container = document.getElementsByClassName(container)[0];
    var bounds = container.getBoundingClientRect();
    var x = event.clientX - bounds.left;
    var y = event.clientY - bounds.top;
    return { x: x, y: y }; 
}   