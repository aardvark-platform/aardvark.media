function initTouchSticks(id, sticks) {
  var clicky = document.getElementById(id);

  function initTouchStick(name, minx, maxx, miny, maxy, maxr) {

    var dragging = false;

    var startleft = 0;
    var starttop = 0;
    var pointerangle = -999.0;
    var pointerdistance = -999.0;
    var cr = 0;

    var basex = -999;
    var basey = -999;

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

    function pointerleft() {
      return Math.round(pointerdistance * Math.cos(pointerangle*0.0174533));
    }

    function pointertop() {
      return Math.round(pointerdistance * Math.sin(pointerangle*0.0174533));
    }

    function getScroll() {
      return {
        x: document.documentElement.scrollLeft || document.body.scrollLeft,
        y: document.documentElement.scrollTop || document.body.scrollTop
      };
    }

    function updatecircle() {
      var el = document.getElementById("touch_circle_" + name + "_" + id);
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

    function createcircle(r) {
      var el = document.createElementNS("http://www.w3.org/2000/svg", "svg");
      el.setAttributeNS(null, "id", "touch_circle_" + name + "_" + id);
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

    function createline() {
      var el = document.createElementNS("http://www.w3.org/2000/svg", "svg");
      var leftest = Math.min(startleft, startleft + pointerleft());
      var toppest = Math.min(starttop, starttop + pointertop());
      var rightest = Math.max(startleft, startleft + pointerleft());
      var bottomest = Math.max(starttop, starttop + pointertop());
      var width = rightest - leftest;
      var height = bottomest - toppest;
      el.setAttributeNS(null, "id", "touch_line_" + name + "_" + id);
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
      var e = document.getElementById("touch_line_" + name + "_" + id);
      if (e) {
        e.parentNode.removeChild(e);
      }
    }

    function updateline(id) {
      destroyline(id);
      createline(id);
    }

    function destroycircle(id) {
      var e = document.getElementById("touch_circle_" + name + "_" + id);
      e.parentNode.removeChild(e);
    }

    function panstart(te) {
      var rect = clicky.getBoundingClientRect();
      var pos = getPosition(clicky);
      var xp = te.clientX - pos.x - rect.x;
      var yp = te.clientY - pos.y - rect.y;
      var x = xp / rect.width * 2.0 - 1.0;
      var y = (yp / rect.height * 2.0 - 1.0) * -1.0;
      //basex = x;
      //basey = y;
      //if(basex >= minx && basex < maxx && basey >= miny && basey < maxy) {
        dragging = true;
        startleft = te.clientX;
        starttop = te.clientY;

        var distanceX = te.clientX - startleft;
        var distanceY = te.clientY - starttop;
        var distance = Math.sqrt(distanceX * distanceX + distanceY * distanceY);
        pointerdistance = Math.min(distance,maxr);
        
        pointerangle = Math.atan2(distanceY,distanceX) * (180 / Math.PI);;

        aardvark.processEvent(id, "touchstickstart_" + name, x, y);
        createcircle(35);
        createline();
      //}
    };

    function panmove(te) {
      //if(basex >= minx && basex < maxx && basey >= miny && basey < maxy) {
        if (dragging) {

          var distanceX = te.clientX - startleft;
          var distanceY = te.clientY - starttop;
          var distance = Math.sqrt(distanceX * distanceX + distanceY * distanceY);
          pointerdistance = Math.min(distance,maxr);
          
          pointerangle = Math.atan2(distanceY,distanceX) * (180 / Math.PI);;

          var d = (pointerdistance / maxr);
          var a = pointerangle;
          aardvark.processEvent(id,"touchstickmove_" + name, d,a);
          updatecircle(id);
          updateline(id);
        }
      //}
    };

    function panstop(te) {
      //if(basex >= minx && basex < maxx && basey >= miny && basey < maxy) {
          aardvark.processEvent(id,"touchstickstop_" + name, 0);
          dragging = false;
          pointerangle = -999.0;
          pointerdistance = -999.0;
          //basex = -999;
          //basey = -999;
          destroycircle(id);
          destroyline(id);
      //}
    };

    return {
      name:name,
      start:panstart,
      move:panmove,
      stop:panstop
    }

  };

  var cbs = sticks.map(function(stick) {
    var res = {
      minx:stick.minx,
      maxx:stick.maxx,
      miny:stick.miny,
      maxy:stick.maxy,
      cb:initTouchStick(stick.name,stick.minx,stick.maxx,stick.miny,stick.maxy,stick.maxr)
    };
    return res;
  });

  function getCb(clientX,clientY) {
    
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

    var rect = clicky.getBoundingClientRect();
    var pos = getPosition(clicky);
    var xp = clientX - pos.x - rect.x;
    var yp = clientY - pos.y - rect.y;
    var x = xp / rect.width * 2.0 - 1.0;
    var y = (yp / rect.height * 2.0 - 1.0) * -1.0;
    for(let cb of cbs) {
      if(x >= cb.minx && x < cb.maxx && y >= cb.miny && y < cb.maxy) {
        return cb.cb;
      }
    }
    return "";
  }

  var activeTouches = {};
  var activeIds = {};

  function touchstart(tes) {
    for(let te of tes.changedTouches) {
      let cb = getCb(te.clientX,te.clientY);
      if(!(cb.name in activeTouches)){
        activeTouches[cb.name] = cb;
        activeIds[te.identifier] = cb.name;
        cb.start(te);
      }
    }
    tes.preventDefault();
  }

  function touchmove(tes) {
    for(let te of tes.changedTouches) {
      if(te.identifier in activeIds) {
        let cb = activeTouches[activeIds[te.identifier]];
        cb.move(te);
      }
    }
    tes.preventDefault();
  }

  function touchend(tes) {
    for(let te of tes.changedTouches) {
      if(te.identifier in activeIds) {
        let name = activeIds[te.identifier];
        let cb = activeTouches[name];
        cb.stop(te);
        delete activeIds[te.identifier];
        delete activeTouches[name];
      }
    }
    tes.preventDefault();
  }

  function touchcancel(tes) { touchend(tes); }

  clicky.addEventListener("touchstart", touchstart, false);
  clicky.addEventListener("touchmove", touchmove, false);
  clicky.addEventListener("touchend", touchend, false);
  clicky.addEventListener("touchcancel", touchcancel, false);

};