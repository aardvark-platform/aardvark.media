var bMouseDragging = false;
var nMouseOffsetX = 0;
var nMouseOffsetY = 0;


function getElementByClazz(clazz)
{
    var o = document.getElementsByClassName(clazz);
    if(o && o.length == 1) return o[0];
    else{
        debugger;
        return undefined;
    }
}

function mouseDown(evt) {
    bMouseDragging = true;

    var ball = document.getElementById("ball");
    if (ball) {
        var p = document.documentElement.createSVGPoint();
        p.x = evt.clientX;
        p.y = evt.clientY;

        var m = ball.getScreenCTM();
        p = p.matrixTransform(m.inverse());
        nMouseOffsetX = p.x - parseInt(ball.getAttribute("cx"));
        nMouseOffsetY = p.y - parseInt(ball.getAttribute("cy"));
    }
}
function mouseUp(evt) {
    bMouseDragging = false;
}
function mouseMove(evt) {

    var p = document.documentElement.createSVGPoint();
    p.x = evt.clientX;
    p.y = evt.clientY;
    var bClient = true;

    if (bMouseDragging) {
        var ball = document.getElementById("ball");
        if (ball) {

            var m = ball.getScreenCTM();
            p = p.matrixTransform(m.inverse());

            ball.setAttribute("cx", p.x - nMouseOffsetX);
            ball.setAttribute("cy", p.y - nMouseOffsetY);
            bClient = false;
        }
    }
}function draggable(id) {
    var ball = document.getElementById(id);
    if (ball) {
        ball.addEventListener(id, mouseDown, false);
        ball.addEventListener(id, mouseUp, false);
        ball.addEventListener(id, mouseMove, false);
    }
}