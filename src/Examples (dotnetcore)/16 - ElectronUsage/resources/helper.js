function screenit() {
    debugger;
    var element = document.getElementsByClassName('myRenderControl')[0].id;
    var url3 = window.top.location.href + "rendering/screenshot/" + self.id + "?w=" + self.div.clientWidth + "&h=" + self.div.clientHeight + "&samples=8";
    console.log(url3)
}

function installScreenshotPath(input,renderControlClass)
{
    var element = document.getElementsByClassName('myRenderControl')[0];
    var url = window.top.location.href + "rendering/screenshot/" + element.id + "?w=" + element.clientWidth + "&h=" + element.clientHeight + "&samples=8&fmt=png";
    input.value = url;
}