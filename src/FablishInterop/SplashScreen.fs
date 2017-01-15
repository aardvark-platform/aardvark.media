module SplashScreen

open System
open System.Drawing
open System.Windows.Forms


let spawn () =
        
    let logo =
        let stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream "logo.png"
        if isNull stream then
            failwithf "logo not found"
        else
            Bitmap.FromStream stream

    let load = new Form()
    load.FormBorderStyle <- FormBorderStyle.None
    let pic = new System.Windows.Forms.PictureBox()
    let img = Bitmap.FromFile("logo.png")
    pic.Image <- img
    load.Name <- "Splash";
    pic.Dock <- DockStyle.Fill
    load.Controls.Add(pic)
    load.ShowIcon <- false;
    load.ShowInTaskbar <- false;
    load.Size <- System.Drawing.Size(538,189)
    load.StartPosition <- System.Windows.Forms.FormStartPosition.CenterScreen
    load.ResumeLayout(false);
    load.PerformLayout();
    load.Show()
    load

