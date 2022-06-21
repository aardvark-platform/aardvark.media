function takeScreenshot() {
    aardvark.electron.remote.getCurrentWindow().webContents.capturePage().then((e) => console.log(e.toPNG()));

    var bytes = aardvark.electron.remote.getCurrentWindow().webContents.capturePage().then(image => image.toPNG());
    return bytes;
}
