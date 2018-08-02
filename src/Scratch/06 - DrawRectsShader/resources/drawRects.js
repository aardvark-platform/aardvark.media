
function relativeCoords2(event, containerClass) {
	var source = event.target || event.srcElement;
	var containers = document.getElementsByClassName(containerClass);
	if (containers && containers.length > 0) {
		var container = containers[0];
		var bounds = container.getBoundingClientRect();
		var x = event.clientX - bounds.left;
		var y = event.clientY - bounds.top;
		var r = { X: (x / container.clientWidth).toFixed(10), Y: (y / container.clientHeight).toFixed(10) };
		return { Some: r };
	} else
		return null;
}