function initTouchStick(id) {

var clicky = document.getElementById(id);
var globalid = id;
var maxr = 100;

var dragging = false;

var startleft = 0;
var starttop = 0;
var pointerangle = -999.0;
var pointerdistance = -999.0;
var cr = 0;

function pointerleft() {
 return Math.round(pointerdistance * Math.cos(pointerangle*0.0174533));
}

function pointertop() {
	return Math.round(pointerdistance * Math.sin(pointerangle*0.0174533));
}

function getPosition(element) {
  var xPosition = 0,
    yPosition = 0;

  var style = element.currentStyle || window.getComputedStyle(element);

  while (element) {
    xPosition += (element.clientLeft);
    yPosition += (element.clientTop);
    element = element.offsetParent;
  }
  return {
    x: xPosition,
    y: yPosition
  };
}

function getScroll() {
  return {
    x: document.documentElement.scrollLeft || document.body.scrollLeft,
    y: document.documentElement.scrollTop || document.body.scrollTop
  };
}

function updatecircle(id) {
  var el = document.getElementById("touch_circle_" + id);
  var pos = getPosition(clicky),
    scroll = getScroll(),
    diff = {
      x: (pos.x),
      y: (pos.y)
    };

  var realx = startleft + pointerleft() - cr + diff.x;
  var realy = starttop + pointertop() - cr + diff.y;

  el.style.left = realx;
  el.style.top = realy;
  el.style.position = "fixed";
  el.style.zIndex = 99999;
}

function createcircle(id, r) {
  var el = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  el.setAttributeNS(null, "id", "touch_circle_" + id);
  el.setAttributeNS(null, "viewBox", "" + 0 + " " + 0 + " " + 2 * r + " " + 2 * r);
  el.setAttributeNS(null, "width", 2 * r);
  el.setAttributeNS(null, "height", 2 * r);
  var c = document.createElementNS("http://www.w3.org/2000/svg", "circle");
  c.setAttributeNS(null, "cx", r);
  c.setAttributeNS(null, "cy", r);
  c.setAttributeNS(null, "r", r);
  cr = r;
  c.setAttributeNS(null, "fill", "black");
  c.setAttributeNS(null, "fill-opacity", "0.2");
  c.setAttributeNS(null, "stroke", "white");
  c.setAttributeNS(null, "stroke-width", "1.5");
  c.setAttributeNS(null, "stroke-opacity", "0.8");

  el.appendChild(c);
  document.body.appendChild(el);
  updatecircle(id);
}

function createline(id) {
  var el = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  var leftest = Math.min(startleft, startleft + pointerleft());
  var toppest = Math.min(starttop, starttop + pointertop());
  var rightest = Math.max(startleft, startleft + pointerleft());
  var bottomest = Math.max(starttop, starttop + pointertop());
  var width = rightest - leftest;
  var height = bottomest - toppest;
  el.setAttributeNS(null, "id", "touch_line_" + id);
  el.setAttributeNS(null, "viewBox", 0 + " " + 0 + " " + width + " " + height);
  el.setAttributeNS(null, "width", width);
  el.setAttributeNS(null, "height", height);
  el.style.zIndex = 99999;
  el.style.position = "fixed";
  el.style.top = toppest;
  el.style.left = leftest;
  var l = document.createElementNS("http://www.w3.org/2000/svg", "line");
  l.setAttributeNS(null, "x1", startleft - leftest);
  l.setAttributeNS(null, "y1", starttop - toppest);
  l.setAttributeNS(null, "x2", startleft - leftest + pointerleft());
  l.setAttributeNS(null, "y2", starttop - toppest + pointertop());

  l.setAttributeNS(null, "fill", "black");
  l.setAttributeNS(null, "fill-opacity", "0.2");
  l.setAttributeNS(null, "stroke", "black");
  l.setAttributeNS(null, "stroke-width", "1.5");
  l.setAttributeNS(null, "stroke-opacity", "0.4");
  el.appendChild(l);
  document.body.appendChild(el);
}

function destroyline(id) {
  var e = document.getElementById("touch_line_" + id);
  if (e) {
    e.parentNode.removeChild(e);
  }
}

function updateline(id) {
  destroyline(id);
  createline(id);
}

function destroycircle(id) {
  var e = document.getElementById("touch_circle_" + id);
  e.parentNode.removeChild(e);
}

function panstart(e) {
  if (!(e.pointerType == "mouse")) {
    clicky.classList.add("notouch");
    dragging = true;
    var left = e.center.x;
    var top = e.center.y;
    startleft = left;
    starttop = top;
    pointerdistance = Math.min(e.distance,maxr);
    pointerangle = e.angle;
    createcircle(globalid, 35);
    createline(globalid);
  }
};

function panmove(e) {
  if (!(e.pointerType == "mouse")) {
    if (dragging) {
    	pointerdistance = Math.min(e.distance,maxr);
      //output.innerHTML = "distance:" + (pointerdistance/maxr) + " angle:" + e.angle;
      aardvark.processEvent(id,"touchstickstop", 0);
    	pointerangle = e.angle;
      updatecircle(globalid);
      updateline(globalid);
    } else {
      output.innerHTML = "text";
    }
  }
};


function panstop(e) {
  if (!(e.pointerType == "mouse")) {
    clicky.classList.remove("notouch");
    aardvark.processEvent(id,"touchstickstop", 0);
    dragging = false;
    pointerangle = -999.0;
    pointerdistance = -999.0;
    destroycircle(globalid);
    destroyline(globalid);
  }
};

var h = new Hammer(clicky, {
  direction: Hammer.DIRECTION_ALL,
  threshold: 1
});
h.get('pan').set({
  direction: Hammer.DIRECTION_ALL
});
h.get('pan').set({
  threshold: 1
});
h.on("panstart", panstart);
h.on("panmove", panmove);
h.on("panend", panstop);

};