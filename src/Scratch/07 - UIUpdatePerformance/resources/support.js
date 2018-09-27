
var doIt = function (elem) {
    var repeat = function () {
        var currentdate = new Date();
        var ms = currentdate.getMilliseconds();
        elem.innerHTML = ms + " ms.";
    };

    setInterval(repeat, 0);
};