using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
namespace WukongCinema {
public static class Program {
    [STAThread] public static void Main(string[] args) {
        string folder=AppDomain.CurrentDomain.BaseDirectory;
        try {
            if(args.Length>0 && args[0]=="--render-check") { Cinema.RenderCheck(folder); return; }
            if(args.Length>0 && args[0]=="--render-wallpaper") {
                int width=3840,height=2160;
                if(args.Length>=4 && (!int.TryParse(args[2],out width) || !int.TryParse(args[3],out height)))
                    throw new ArgumentException("Wallpaper dimensions must be whole numbers.");
                Cinema.RenderWallpaper(folder,args.Length>1 ? args[1] : Path.Combine(folder,"wallpaper-always-wukong.jpg"),width,height); return;
            }
            if(args.Length>0 && args[0]=="--stop") {
                EventWaitHandle evt; if(EventWaitHandle.TryOpenExisting("Local\\WukongCinemaQuit",out evt)) { evt.Set(); evt.Dispose(); } return;
            }
            // Scheduled launches must never toggle an already running scene.
            bool background=args.Length>0 && args[0]=="--background";
            double minutes=5; if(args.Length>0 && !background) double.TryParse(args[0],out minutes);
            Cinema.Start(folder,minutes,!background);
        } catch(Exception ex) { File.AppendAllText(Path.Combine(folder,"startup-error.log"),ex.ToString()+Environment.NewLine);
            MessageBox.Show(ex.Message,"Wukong Cinema"); }
    }
}
public static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct LastInput { public uint size, time; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput li);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    public static uint Idle() { var li=new LastInput { size=8 }; GetLastInputInfo(ref li); return unchecked((uint)Environment.TickCount-li.time); }
    public static bool TriggerHeld() { return GetAsyncKeyState(0x47)<0 || GetAsyncKeyState(0x11)<0 || GetAsyncKeyState(0x12)<0; }
}
public class Scene : Grid {
    readonly Canvas art;
    readonly Image bleed;
    readonly System.Windows.Shapes.Rectangle bleedShade;
    readonly Brush wideEdgeMask;
    readonly Image backgroundImage;
    readonly BlurEffect backgroundBlur;
    readonly System.Windows.Shapes.Rectangle shade;
    readonly System.Windows.Shapes.Rectangle vignette;
    readonly System.Windows.Shapes.Ellipse aura;
    readonly Image portrait;
    double zoomProgress;
    public Scene(BitmapSource original,BitmapSource newPortrait) {
        Background=Brushes.Black; ClipToBounds=true;
        // Preserve the full 16:9 composition on every monitor. A softly
        // enlarged copy extends it into the spare space on other ratios.
        bleed=new Image { Source=original,Stretch=Stretch.UniformToFill,
            Effect=new BlurEffect { Radius=48 },CacheMode=new BitmapCache() };
        Children.Add(bleed);
        bleedShade=new System.Windows.Shapes.Rectangle { Fill=Brushes.Black };
        Children.Add(bleedShade);
        art=new Canvas { Width=2048,Height=1152 };
        var mask=new LinearGradientBrush { StartPoint=new Point(0,.5),EndPoint=new Point(1,.5) };
        mask.GradientStops.Add(new GradientStop(Colors.Transparent,0));
        mask.GradientStops.Add(new GradientStop(Colors.White,.055));
        mask.GradientStops.Add(new GradientStop(Colors.White,.945));
        mask.GradientStops.Add(new GradientStop(Colors.Transparent,1));
        wideEdgeMask=mask;
        var viewport=new Canvas(); Children.Add(viewport); viewport.Children.Add(art);
        SizeChanged+=(s,e)=>UpdateTransform();
        backgroundBlur=new BlurEffect { Radius=0,RenderingBias=RenderingBias.Performance };
        backgroundImage=new Image { Source=original,Width=2048,Height=1152,Stretch=Stretch.Fill };
        art.Children.Add(backgroundImage);
        shade=new System.Windows.Shapes.Rectangle { Width=2048,Height=1152,Fill=Brushes.Black };
        art.Children.Add(shade);
        var edge=new RadialGradientBrush { Center=new Point(.5,.49),GradientOrigin=new Point(.5,.49),RadiusX=.72,RadiusY=.8 };
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(0,0,0,0),0));
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(0,0,0,0),.42));
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(165,0,0,0),1));
        vignette=new System.Windows.Shapes.Rectangle { Width=2048,Height=1152,Fill=edge };
        art.Children.Add(vignette);
        var light=new RadialGradientBrush { Center=new Point(.5,.45),GradientOrigin=new Point(.5,.45),RadiusX=.56,RadiusY=.62 };
        light.GradientStops.Add(new GradientStop(Color.FromArgb(105,176,89,37),0));
        light.GradientStops.Add(new GradientStop(Color.FromArgb(36,134,62,24),.48));
        light.GradientStops.Add(new GradientStop(Color.FromArgb(0,70,35,12),1));
        aura=new System.Windows.Shapes.Ellipse { Width=1020,Height=1040,Fill=light };
        Canvas.SetLeft(aura,520); Canvas.SetTop(aura,65); art.Children.Add(aura);
        // Match the upscaled cutout to the original character. Feature
        // landmarks on the armour, head and staff put it at 98.8% scale,
        // roughly 9px right and 34px down in this 2048x1152 art space.
        portrait=new Image { Source=newPortrait,Width=2023.4,Height=1138.2,Stretch=Stretch.Fill };
        Canvas.SetLeft(portrait,8.7); Canvas.SetTop(portrait,33.4);
        art.Children.Add(portrait);
    }
    void UpdateTransform() {
        if(ActualWidth<=0 || ActualHeight<=0) return;
        double ratio=ActualWidth/ActualHeight;
        // Narrower screens frame the central hero. Ultrawide screens retain
        // the whole composition with softly extended sides.
        double baseScale=ratio<=1.78
            ? Math.Max(ActualWidth/2048,ActualHeight/1152)
            : Math.Min(ActualWidth/2048,ActualHeight/1152);
        double scale=baseScale*(1+.038*zoomProgress);
        art.RenderTransform=new ScaleTransform(scale,scale);
        Canvas.SetLeft(art,(ActualWidth-2048*scale)/2);
        Canvas.SetTop(art,(ActualHeight-1152*scale)/2);
        art.OpacityMask=ratio>1.95 ? wideEdgeMask : null;
        bleed.Visibility=(2048*scale>=ActualWidth-1 && 1152*scale>=ActualHeight-1)
            ? Visibility.Collapsed : Visibility.Visible;
        bleedShade.Visibility=bleed.Visibility;
    }
    public void SetScene(double p,double z) {
        p=Math.Max(0,Math.Min(1,p));
        zoomProgress=Math.Max(0,Math.Min(1,z)); UpdateTransform();
        backgroundBlur.Radius=12*p;
        backgroundImage.Effect=p<.001 ? null : backgroundBlur;
        bleedShade.Opacity=.16+.42*p;
        shade.Opacity=.10+.58*p;
        vignette.Opacity=.12+.64*p;
        aura.Opacity=.07+.35*p;
        portrait.Opacity=1;
    }
    public void SetDarkness(double p) { SetScene(p,p); }
}
public class Cinema : Application {
    string folder,log; double idleMs; BitmapSource original,newPortrait;
    Window control; HwndSource source; Forms.NotifyIcon tray;
    readonly System.Collections.Generic.List<Window> windows=new System.Collections.Generic.List<Window>();
    readonly System.Collections.Generic.List<Scene> scenes=new System.Collections.Generic.List<Scene>();
    DispatcherTimer poll; EventWaitHandle showEvent,quitEvent; Mutex mutex;
    readonly Stopwatch clock=Stopwatch.StartNew();
    int phase; double started,lastWake,darkness,zoom,alpha,fromDark,fromZoom,fromAlpha;
    bool armed;
    void Log(string msg) { File.AppendAllText(log,DateTime.Now.ToString("s")+" "+msg+Environment.NewLine); }
    static double Ease(double t) { t=Math.Max(0,Math.Min(1,t)); return t*t*t*(t*(t*6-15)+10); }
    // Cubic Bezier motion graph: a measured start and a long, soft landing.
    static double ZoomCurve(double t) {
        t=Math.Max(0,Math.Min(1,t)); if(t==0 || t==1) return t;
        const double x1=.33,x2=.18,y1=.04,y2=1;
        double u=t;
        for(int i=0;i<8;i++) {
            double x=3*(1-u)*(1-u)*u*x1+3*(1-u)*u*u*x2+u*u*u;
            double slope=3*(1-u)*(1-u)*x1+6*(1-u)*u*(x2-x1)+3*u*u*(1-x2);
            if(Math.Abs(slope)<.0001) break;
            u=Math.Max(0,Math.Min(1,u-(x-t)/slope));
        }
        return 3*(1-u)*(1-u)*u*y1+3*(1-u)*u*u*y2+u*u*u;
    }
    void Apply() { for(int i=0;i<windows.Count;i++) { windows[i].Opacity=alpha; scenes[i].SetScene(darkness,zoom); } }
    void ShowScene() {
        if(phase!=0) return;
        Log("Entering"); poll.Interval=TimeSpan.FromMilliseconds(16); phase=1; started=clock.Elapsed.TotalSeconds; armed=false; darkness=0; zoom=0; alpha=0;
        foreach(var w in windows) w.Show();
        if(windows.Count>0) windows[0].Activate(); Apply();
    }
    void Wake() {
        if(phase==0 || phase==3 || !armed) return;
        Log("Leaving"); poll.Interval=TimeSpan.FromMilliseconds(16); fromDark=darkness; fromZoom=zoom; fromAlpha=alpha;
        phase=3; started=clock.Elapsed.TotalSeconds; lastWake=started;
    }
    void Tick(object sender,EventArgs e) {
        if(quitEvent.WaitOne(0)) { Shutdown(); return; }
        if(showEvent.WaitOne(0)) ShowScene();
        double now=clock.Elapsed.TotalSeconds;
        if(phase==0) { if(now-lastWake>3 && Native.Idle()>=idleMs) ShowScene(); return; }
        double t=now-started;
        if(!armed && t>.35 && !Native.TriggerHeld()) armed=true;
        if(phase==1) { alpha=Ease(t/.42); darkness=Ease((t-.12)/2.18); zoom=ZoomCurve((t-.10)/2.32);
            if(t>=2.42) { Apply(); phase=2; poll.Interval=TimeSpan.FromMilliseconds(200); Log("Idle"); } }
        else if(phase==3) {
            darkness=fromDark*(1-Ease(t/1.02)); zoom=fromZoom*(1-ZoomCurve(t/1.05)); alpha=fromAlpha*(1-Ease((t-.68)/.35));
            if(t>=1.05) { foreach(var w in windows) w.Hide(); phase=0; poll.Interval=TimeSpan.FromMilliseconds(200); lastWake=now; Log("Hidden"); }
        }
        if(phase!=2) Apply();
    }
    IntPtr Message(IntPtr h,int msg,IntPtr wp,IntPtr lp,ref bool handled) {
        if(msg==0x0312) { if(wp.ToInt32()==1) { if(phase==0) ShowScene(); else Wake(); } else Shutdown(); handled=true; }
        return IntPtr.Zero;
    }
    bool Init(string path,double minutes,bool showExisting) {
        folder=path; idleMs=Math.Max(1,minutes)*60000; log=Path.Combine(folder,"runtime.log");
        DispatcherUnhandledException+=(s,e)=> { Log(e.Exception.ToString()); e.Handled=true; Shutdown(1); };
        ShutdownMode=ShutdownMode.OnExplicitShutdown;
        bool created; mutex=new Mutex(true,"Local\\WukongCinemaV3",out created);
        showEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\WukongCinemaShow");
        quitEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\WukongCinemaQuit");
        if(!created) { if(showExisting) showEvent.Set(); return false; }
        LoadArt();
        control=new Window { Width=1,Height=1,ShowInTaskbar=false,WindowStyle=WindowStyle.None };
        var hwnd=new WindowInteropHelper(control).EnsureHandle();
        source=HwndSource.FromHwnd(hwnd); source.AddHook(Message);
        if(!Native.RegisterHotKey(hwnd,1,0x4003,0x47)) throw new Exception("Ctrl+Alt+G is already registered by another application.");
        Native.RegisterHotKey(hwnd,2,0x4007,0x47);
        foreach(var screen in Forms.Screen.AllScreens) {
            var b=screen.Bounds;
            var scene=new Scene(original,newPortrait);
            var w=new Window { Title="Wukong Cinema",Content=scene,Background=Brushes.Black,WindowStyle=WindowStyle.None,
                ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,AllowsTransparency=true,Opacity=0,Cursor=Cursors.None };
            var wh=new WindowInteropHelper(w).EnsureHandle();
            var transform=HwndSource.FromHwnd(wh).CompositionTarget.TransformFromDevice;
            var location=transform.Transform(new Point(b.X,b.Y)); var size=transform.Transform(new Point(b.Width,b.Height));
            w.Left=location.X; w.Top=location.Y; w.Width=size.X; w.Height=size.Y;
            w.PreviewKeyDown+=(s,e)=> { Wake(); e.Handled=true; };
            w.PreviewMouseDown+=(s,e)=> { Wake(); e.Handled=true; };
            w.PreviewMouseWheel+=(s,e)=> { Wake(); e.Handled=true; };
            Point? anchor=null;
            w.MouseMove+=(s,e)=> { var pt=e.GetPosition(w); if(!anchor.HasValue) anchor=pt; else if((pt-anchor.Value).Length>5) { Wake(); anchor=pt; } };
            w.IsVisibleChanged+=(s,e)=> { anchor=null; };
            windows.Add(w); scenes.Add(scene); Log("Screen "+b.ToString()+" WPF "+size.ToString());
        }
        tray=new Forms.NotifyIcon { Icon=System.Drawing.SystemIcons.Application,Text="Wukong Cinema - Ctrl+Alt+G",Visible=true };
        var menu=new Forms.ContextMenuStrip(); menu.Items.Add("Preview",null,(s,e)=>ShowScene()); menu.Items.Add("Exit",null,(s,e)=>Shutdown());
        tray.ContextMenuStrip=menu; tray.DoubleClick+=(s,e)=>ShowScene();
        poll=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(200) }; poll.Tick+=Tick; poll.Start();
        Exit+=(s,e)=> { poll.Stop(); Native.UnregisterHotKey(hwnd,1); Native.UnregisterHotKey(hwnd,2); tray.Visible=false; tray.Dispose(); Log("Stopped"); };
        Log("Ready - Ctrl+Alt+G registered; responsive fit mode"); return true;
    }
    void LoadArt() {
        var b=new BitmapImage(); b.BeginInit(); b.CacheOption=BitmapCacheOption.OnLoad;
        // The desktop and idle scene use the same clean background, so the
        // character never appears twice as the blur/darkness animates.
        b.UriSource=new Uri(Path.Combine(folder,"landscape-clean.png")); b.EndInit(); b.Freeze(); original=b;
        var portraitBitmap=new BitmapImage(); portraitBitmap.BeginInit(); portraitBitmap.CacheOption=BitmapCacheOption.OnLoad;
        portraitBitmap.UriSource=new Uri(Path.Combine(folder,"wukong-original-upscaled.png")); portraitBitmap.EndInit(); portraitBitmap.Freeze(); newPortrait=portraitBitmap;
    }
    public static void Start(string folder,double minutes) { Start(folder,minutes,true); }
    public static void Start(string folder,double minutes,bool showExisting) { Native.SetProcessDPIAware(); var app=new Cinema(); if(app.Init(folder,minutes,showExisting)) app.Run(); }
    public static void RenderCheck(string folder) {
        var c=new Cinema { folder=folder }; c.LoadArt();
        foreach(double p in new double[]{0,.5,1}) {
            RenderScene(c,p,1600,1000,Path.Combine(folder,"preview-"+p.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"));
        }
        RenderScene(c,1,1600,900,Path.Combine(folder,"preview-16x9.png"));
        RenderScene(c,1,2100,900,Path.Combine(folder,"preview-ultrawide.png"));
        RenderScene(c,1,900,1600,Path.Combine(folder,"preview-portrait.png"));
    }
    public static void RenderWallpaper(string folder,string path,int width,int height) {
        if(width<320 || height<240 || width>10000 || height>10000)
            throw new ArgumentOutOfRangeException("Wallpaper dimensions are outside the supported range.");
        var c=new Cinema { folder=folder }; c.LoadArt();
        RenderScene(c,0,width,height,path);
    }
    static void RenderScene(Cinema c,double p,int width,int height,string path) {
        var scene=new Scene(c.original,c.newPortrait); scene.SetDarkness(p);
        scene.Measure(new Size(width,height)); scene.Arrange(new Rect(0,0,width,height)); scene.UpdateLayout();
        var target=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); target.Render(scene);
        BitmapEncoder encoder=Path.GetExtension(path).Equals(".jpg",StringComparison.OrdinalIgnoreCase)
            ? (BitmapEncoder)new JpegBitmapEncoder { QualityLevel=96 }
            : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using(var f=File.Create(path)) encoder.Save(f);
    }
}
}
