using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Luna
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Frame : Window
    {
        private static IntPtr s_hHook = IntPtr.Zero;
        private readonly object syncObj = new object();
        private uint? taskbarRestart = null;
        private CefSharp.OffScreen.ChromiumWebBrowser? browser = null;
        private System.Drawing.Bitmap? desktopBitmap = null;
        private IntPtr hWnd = IntPtr.Zero;
        private bool isDrawing = false;
        private int frameRate = 15;
        private string? source = null;
        private bool isMuted = true;
        private double scaleX = 1.0;
        private double scaleY = 1.0;
        private NativeMethods.LowLevelMouseProc? lowLevelMouseProc = null;
        public bool IsLocked { get; set; } = false;

        public string? Source
        {
            get
            {
                return this.source;
            }
            set
            {
                string url;

                if (Regex.IsMatch(value!, @"^\w+://", RegexOptions.CultureInvariant))
                {
                    url = value!;
                }
                else if (System.IO.Path.IsPathRooted(value))
                {
                    url = String.Join("file:///", value);
                }
                else
                {
                    url = String.Join("file:///", System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!, value!));
                }

                this.source = value;
                this.browser!.Load(url);
            }
        }

        public bool IsMuted
        {
            get
            {
                return this.isMuted;
            }
            set
            {
                this.isMuted = value;
                this.browser!.GetBrowser().GetHost().SetAudioMuted(this.isMuted);
            }
        }

        public Frame()
        {
            InitializeComponent();

            System.Configuration.Configuration? config1 = null;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), System.Reflection.Assembly.GetExecutingAssembly().GetName().Name!);
            
            if (Directory.Exists(directory))
            {
                string filename = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                foreach (string s in from s in Directory.EnumerateFiles(directory, "*.config", SearchOption.TopDirectoryOnly) where filename.Equals(Path.GetFileNameWithoutExtension(s)) select s)
                {
                    System.Configuration.ExeConfigurationFileMap exeConfigurationFileMap = new System.Configuration.ExeConfigurationFileMap();

                    exeConfigurationFileMap.ExeConfigFilename = s;
                    config1 = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, System.Configuration.ConfigurationUserLevel.None);
                }
            }

            if (config1 == null)
            {
                config1 = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);

                if (config1.AppSettings.Settings["FrameRate"] != null && config1.AppSettings.Settings["FrameRate"].Value.Length > 0)
                {
                    this.frameRate = Int32.Parse(config1.AppSettings.Settings["FrameRate"].Value, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (config1.AppSettings.Settings["Source"] != null && config1.AppSettings.Settings["Source"].Value.Length > 0)
                {
                    this.source = config1.AppSettings.Settings["Source"].Value;
                }

                if (config1.AppSettings.Settings["Lock"] != null && config1.AppSettings.Settings["Lock"].Value.Length > 0)
                {
                    this.IsLocked = Boolean.Parse(config1.AppSettings.Settings["Lock"].Value);
                }

                if (config1.AppSettings.Settings["Mute"] != null && config1.AppSettings.Settings["Mute"].Value.Length > 0)
                {
                    this.isMuted = Boolean.Parse(config1.AppSettings.Settings["Mute"].Value);
                }
            }
            else
            {
                System.Configuration.Configuration config2 = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);

                if (config1.AppSettings.Settings["FrameRate"] == null)
                {
                    if (config2.AppSettings.Settings["FrameRate"] != null && config2.AppSettings.Settings["FrameRate"].Value.Length > 0)
                    {
                        this.frameRate = Int32.Parse(config2.AppSettings.Settings["FrameRate"].Value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                else if (config1.AppSettings.Settings["FrameRate"].Value.Length > 0)
                {
                    this.frameRate = Int32.Parse(config1.AppSettings.Settings["FrameRate"].Value, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (config1.AppSettings.Settings["Source"] == null)
                {
                    if (config2.AppSettings.Settings["Source"] != null && config2.AppSettings.Settings["Source"].Value.Length > 0)
                    {
                        this.source = config2.AppSettings.Settings["Source"].Value;
                    }
                }
                else if (config1.AppSettings.Settings["Source"].Value.Length > 0)
                {
                    this.source = config1.AppSettings.Settings["Source"].Value;
                }

                if (config1.AppSettings.Settings["Lock"] == null)
                {
                    if (config2.AppSettings.Settings["Lock"] != null && config2.AppSettings.Settings["Lock"].Value.Length > 0)
                    {
                        this.IsLocked = Boolean.Parse(config2.AppSettings.Settings["Lock"].Value);
                    }
                }
                else if (config1.AppSettings.Settings["Lock"].Value.Length > 0)
                {
                    this.IsLocked = Boolean.Parse(config1.AppSettings.Settings["Lock"].Value);
                }

                if (config1.AppSettings.Settings["Mute"] == null)
                {
                    if (config2.AppSettings.Settings["Mute"] != null && config2.AppSettings.Settings["Mute"].Value.Length > 0)
                    {
                        this.isMuted = Boolean.Parse(config2.AppSettings.Settings["Mute"].Value);
                    }
                }
                else if (config1.AppSettings.Settings["Mute"].Value.Length > 0)
                {
                    this.isMuted = Boolean.Parse(config1.AppSettings.Settings["Mute"].Value);
                }
            }
        }

        protected async override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var presentationSource = PresentationSource.FromVisual(this);
            System.Windows.Interop.HwndSource? hwndSource = presentationSource as System.Windows.Interop.HwndSource;

            if (hwndSource != null)
            {
                const int WH_MOUSE_LL = 14;

                this.hWnd = hwndSource.Handle;
                this.taskbarRestart = NativeMethods.RegisterWindowMessage("TaskbarCreated");

                if (this == Application.Current.MainWindow)
                {
                    var settings = new CefSharp.OffScreen.CefSettings
                    {
                        WindowlessRenderingEnabled = true,
                        BackgroundColor = 0x00,
                        CachePath = null,
                        Locale = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName,
                        AcceptLanguageList = System.Globalization.CultureInfo.CurrentCulture.Name,
                        LogSeverity = CefSharp.LogSeverity.Disable
                    };
                    string url;

                    //settings.CefCommandLineArgs.Add("disable-gpu", "1");
                    //settings.CefCommandLineArgs.Add("disable-gpu-vsync", "1");
                    settings.CefCommandLineArgs.Add("disable-gpu-shader-disk-cache", "1");
                    //settings.DisableGpuAcceleration();
                    settings.SetOffScreenRenderingBestPerformanceArgs();

                    CefSharp.Cef.EnableWaitForBrowsersToClose();
                    CefSharp.Cef.Initialize(settings);

                    if (Regex.IsMatch(this.source!, @"^\w+://", RegexOptions.CultureInvariant))
                    {
                        url = this.source!;
                    }
                    else if (System.IO.Path.IsPathRooted(this.source))
                    {
                        url = String.Join("file:///", this.source);
                    }
                    else
                    {
                        url = String.Join("file:///", System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!, this.source!));
                    }

                    this.scaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                    this.scaleY = presentationSource.CompositionTarget.TransformToDevice.M22;

                    this.browser = new CefSharp.OffScreen.ChromiumWebBrowser(url, new CefSharp.BrowserSettings() { WindowlessFrameRate = this.frameRate }, new CefSharp.RequestContext(new CefSharp.RequestContextSettings()));
                    this.browser.Size = new System.Drawing.Size((int)Math.Floor(SystemParameters.VirtualScreenWidth * this.scaleX), (int)Math.Floor(SystemParameters.VirtualScreenHeight * this.scaleY));
                    this.browser.BrowserInitialized += (object sender, EventArgs e) =>
                    {
                        this.browser.GetBrowser().GetHost().SetAudioMuted(this.isMuted);
                    }!;
                    this.browser.Paint += this.OnPaint!;

                    Microsoft.Win32.SystemEvents.DisplaySettingsChanged += this.OnDisplaySettingsChanged!;

                    await this.browser.WaitForInitialLoadAsync();

                    using (var curentProcess = System.Diagnostics.Process.GetCurrentProcess())
                    using (var mainModule = curentProcess.MainModule)
                    {
                        this.lowLevelMouseProc = (int nCode, IntPtr wParam, IntPtr lParam) =>
                        {
                            if (!this.IsLocked && nCode >= 0)
                            {
                                const int WM_MOUSEMOVE = 0x0200;
                                const int WM_LBUTTONDOWN = 0x0201;
                                const int WM_LBUTTONUP = 0x0202;
                                int message = wParam.ToInt32();

                                if (message == WM_MOUSEMOVE)
                                {
                                    Frame? frame = Application.Current.MainWindow as Frame;

                                    if (frame != null)
                                    {
                                        NativeMethods.MSLLHOOKSTRUCT hookStruct = (NativeMethods.MSLLHOOKSTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT))!;
                                        var presentationSource = PresentationSource.FromVisual(this);

                                        frame.browser!.GetBrowser().GetHost().SendMouseMoveEvent(new CefSharp.MouseEvent((int)Math.Floor(hookStruct.pt.x * presentationSource.CompositionTarget.TransformToDevice.M11), (int)Math.Floor(hookStruct.pt.y * presentationSource.CompositionTarget.TransformToDevice.M22), CefSharp.CefEventFlags.LeftMouseButton), false);
                                    }
                                }
                                else if (message == WM_LBUTTONDOWN)
                                {
                                    Frame? frame = Application.Current.MainWindow as Frame;

                                    if (frame != null)
                                    {
                                        NativeMethods.MSLLHOOKSTRUCT hookStruct = (NativeMethods.MSLLHOOKSTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT))!;
                                        var presentationSource = PresentationSource.FromVisual(this);

                                        frame.browser!.GetBrowser().GetHost().SendMouseClickEvent(new CefSharp.MouseEvent((int)Math.Floor(hookStruct.pt.x * presentationSource.CompositionTarget.TransformToDevice.M11), (int)Math.Floor(hookStruct.pt.y * presentationSource.CompositionTarget.TransformToDevice.M22), CefSharp.CefEventFlags.LeftMouseButton), CefSharp.MouseButtonType.Left, false, 1);
                                    }
                                }
                                else if (message == WM_LBUTTONUP)
                                {
                                    Frame? frame = Application.Current.MainWindow as Frame;

                                    if (frame != null)
                                    {
                                        NativeMethods.MSLLHOOKSTRUCT hookStruct = (NativeMethods.MSLLHOOKSTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT))!;
                                        var presentationSource = PresentationSource.FromVisual(this);

                                        frame.browser!.GetBrowser().GetHost().SendMouseClickEvent(new CefSharp.MouseEvent((int)Math.Floor(hookStruct.pt.x * presentationSource.CompositionTarget.TransformToDevice.M11), (int)Math.Floor(hookStruct.pt.y * presentationSource.CompositionTarget.TransformToDevice.M22), CefSharp.CefEventFlags.LeftMouseButton), CefSharp.MouseButtonType.Left, true, 1);
                                    }
                                }
                            }

                            return NativeMethods.CallNextHookEx(Frame.s_hHook, nCode, wParam, lParam);
                        };
                        Frame.s_hHook = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, this.lowLevelMouseProc, NativeMethods.GetModuleHandle(mainModule!.ModuleName!), 0);
                    }

                    NativeMethods.SendMessageTimeout(NativeMethods.FindWindow("Progman", null), 0x052C, new IntPtr(0), IntPtr.Zero, 0x0, 1000, out var result);

                    lock (this.syncObj)
                    {
                        //SaveDesktop();

                        this.isDrawing = true;
                    }
                }

                hwndSource.AddHook(new System.Windows.Interop.HwndSourceHook(this.WndProc));
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_THEMECHANGED = 0x031A;
            const int WM_DESTROY = 0x0002;

            switch (msg)
            {
                case WM_THEMECHANGED:
                    System.Threading.Tasks.Task.Delay(1000).ContinueWith((task) =>
                    {
                        this.browser!.GetBrowser().GetHost().Invalidate(CefSharp.PaintElementType.View);
                    }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());

                    handled = true;

                    break;

                case WM_DESTROY:
                    const uint SPI_GETDESKWALLPAPER = 0x73;
                    const uint SPI_SETDESKWALLPAPER = 0x0014;
                    const int MAX_PATH = 260;
                    const uint SPIF_UPDATEINIFILE = 1;
                    //const uint SPIF_SENDWININICHANGE = 2;
                    string path = new String('\0', MAX_PATH);

                    Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= this.OnDisplaySettingsChanged!;

                    this.browser!.Paint -= this.OnPaint!;

                    lock (this.syncObj)
                    {
                        this.isDrawing = false;

                        //RestoreDesktop();
                        //this.desktopBitmap.Dispose();
                    }

                    if (NativeMethods.SystemParametersInfo(SPI_GETDESKWALLPAPER, (UInt32)path.Length, path, 0))
                    {
                        NativeMethods.SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path.Substring(0, path.IndexOf('\0') + 1), SPIF_UPDATEINIFILE);
                    }

                    if (Frame.s_hHook != IntPtr.Zero)
                    {
                        NativeMethods.UnhookWindowsHookEx(Frame.s_hHook);
                    }

                    this.browser.GetBrowser().CloseBrowser(false);

                    CefSharp.Cef.WaitForBrowsersToClose();
                    CefSharp.Cef.Shutdown();

                    handled = true;

                    break;

                default:
                    if (this.taskbarRestart.HasValue && this.taskbarRestart.Value == msg)
                    {
                        App? app = Application.Current as App;

                        if (app != null && app.NotifyIcon != null)
                        {
                            app.NotifyIcon.Visible = false;
                            app.NotifyIcon.Visible = true;

                            lock (this.syncObj)
                            {
                                this.isDrawing = false;
                            }

                            NativeMethods.SendMessageTimeout(NativeMethods.FindWindow("Progman", null), 0x052C, new IntPtr(0), IntPtr.Zero, 0x0, 1000, out var result);

                            lock (this.syncObj)
                            {
                                this.isDrawing = true;
                            }

                            System.Threading.Tasks.Task.Delay(1000).ContinueWith((task) =>
                            {
                                this.browser!.GetBrowser().GetHost().Invalidate(CefSharp.PaintElementType.View);
                            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                        }
                    }
                    
                    break;
            }

            return IntPtr.Zero;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!e.Cancel && this == Application.Current.MainWindow)
            {
                int versionMajor = Environment.OSVersion.Version.Major;
                int versionMinor = Environment.OSVersion.Version.Minor;
                double version = versionMajor + (double)versionMinor / 10;
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), System.Reflection.Assembly.GetExecutingAssembly().GetName().Name!);

                if (version > 6.1)
                {
                    const long APPMODEL_ERROR_NO_PACKAGE = 15700L;
                    int length = 0;
                    StringBuilder sb = new StringBuilder(0);
                    int result = NativeMethods.GetCurrentPackageFullName(ref length, sb);

                    sb = new StringBuilder(length);
                    result = NativeMethods.GetCurrentPackageFullName(ref length, sb);

                    if (result == APPMODEL_ERROR_NO_PACKAGE)
                    {
                        System.Configuration.Configuration? config = null;

                        if (Directory.Exists(directory))
                        {
                            string filename = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                            foreach (string s in from s in Directory.EnumerateFiles(directory, "*.config", SearchOption.TopDirectoryOnly) where filename.Equals(Path.GetFileNameWithoutExtension(s)) select s)
                            {
                                System.Configuration.ExeConfigurationFileMap exeConfigurationFileMap = new System.Configuration.ExeConfigurationFileMap();

                                exeConfigurationFileMap.ExeConfigFilename = s;
                                config = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, System.Configuration.ConfigurationUserLevel.None);
                            }
                        }

                        if (config == null)
                        {
                            config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                        }

                        if (config.AppSettings.Settings["Source"] == null)
                        {
                            config.AppSettings.Settings.Add("Source", this.source);
                        }
                        else
                        {
                            config.AppSettings.Settings["Source"].Value = this.source;
                        }

                        if (config.AppSettings.Settings["Lock"] == null)
                        {
                            config.AppSettings.Settings.Add("Lock", this.IsLocked.ToString());
                        }
                        else
                        {
                            config.AppSettings.Settings["Lock"].Value = this.IsLocked.ToString();
                        }

                        if (config.AppSettings.Settings["Mute"] == null)
                        {
                            config.AppSettings.Settings.Add("Mute", this.isMuted.ToString());
                        }
                        else
                        {
                            config.AppSettings.Settings["Mute"].Value = this.isMuted.ToString();
                        }

                        config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                    }
                    else
                    {
                        string filename = String.Concat(Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location), ".config");
                        string path = Path.Combine(directory, filename);
                        System.Configuration.ExeConfigurationFileMap exeConfigurationFileMap = new System.Configuration.ExeConfigurationFileMap();

                        if (Directory.Exists(directory))
                        {
                            if (File.Exists(path))
                            {
                                exeConfigurationFileMap.ExeConfigFilename = path;

                                System.Configuration.Configuration config = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, System.Configuration.ConfigurationUserLevel.None);

                                if (config.AppSettings.Settings["Source"] == null)
                                {
                                    config.AppSettings.Settings.Add("Source", this.source);
                                }
                                else
                                {
                                    config.AppSettings.Settings["Source"].Value = this.source;
                                }

                                if (config.AppSettings.Settings["Lock"] == null)
                                {
                                    config.AppSettings.Settings.Add("Lock", this.IsLocked.ToString());
                                }
                                else
                                {
                                    config.AppSettings.Settings["Lock"].Value = this.IsLocked.ToString();
                                }

                                if (config.AppSettings.Settings["Mute"] == null)
                                {
                                    config.AppSettings.Settings.Add("Mute", this.isMuted.ToString());
                                }
                                else
                                {
                                    config.AppSettings.Settings["Mute"].Value = this.isMuted.ToString();
                                }

                                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                            }
                            else
                            {
                                exeConfigurationFileMap.ExeConfigFilename = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None).FilePath;

                                System.Configuration.Configuration config = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, System.Configuration.ConfigurationUserLevel.None);

                                if (config.AppSettings.Settings["Source"] == null)
                                {
                                    config.AppSettings.Settings.Add("Source", this.source);
                                }
                                else
                                {
                                    config.AppSettings.Settings["Source"].Value = this.source;
                                }

                                if (config.AppSettings.Settings["Lock"] == null)
                                {
                                    config.AppSettings.Settings.Add("Lock", this.IsLocked.ToString());
                                }
                                else
                                {
                                    config.AppSettings.Settings["Lock"].Value = this.IsLocked.ToString();
                                }

                                if (config.AppSettings.Settings["Mute"] == null)
                                {
                                    config.AppSettings.Settings.Add("Mute", this.isMuted.ToString());
                                }
                                else
                                {
                                    config.AppSettings.Settings["Mute"].Value = this.isMuted.ToString();
                                }

                                foreach (System.Configuration.ConfigurationSection section in (from section in config.Sections.Cast<System.Configuration.ConfigurationSection>() where !config.AppSettings.SectionInformation.Name.Equals(section.SectionInformation.Name) select section).ToArray())
                                {
                                    section.SectionInformation.RevertToParent();
                                }

                                config.SaveAs(path, System.Configuration.ConfigurationSaveMode.Modified);
                            }
                        }
                        else
                        {
                            Directory.CreateDirectory(directory);

                            exeConfigurationFileMap.ExeConfigFilename = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None).FilePath;

                            System.Configuration.Configuration config = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, System.Configuration.ConfigurationUserLevel.None);

                            if (config.AppSettings.Settings["Source"] == null)
                            {
                                config.AppSettings.Settings.Add("Source", this.source);
                            }
                            else
                            {
                                config.AppSettings.Settings["Source"].Value = this.source;
                            }

                            if (config.AppSettings.Settings["Lock"] == null)
                            {
                                config.AppSettings.Settings.Add("Lock", this.IsLocked.ToString());
                            }
                            else
                            {
                                config.AppSettings.Settings["Lock"].Value = this.IsLocked.ToString();
                            }

                            if (config.AppSettings.Settings["Mute"] == null)
                            {
                                config.AppSettings.Settings.Add("Mute", this.isMuted.ToString());
                            }
                            else
                            {
                                config.AppSettings.Settings["Mute"].Value = this.isMuted.ToString();
                            }

                            foreach (System.Configuration.ConfigurationSection section in (from section in config.Sections.Cast<System.Configuration.ConfigurationSection>() where !config.AppSettings.SectionInformation.Name.Equals(section.SectionInformation.Name) select section).ToArray())
                            {
                                section.SectionInformation.RevertToParent();
                            }

                            config.SaveAs(path, System.Configuration.ConfigurationSaveMode.Modified);
                        }
                    }
                }
                else
                {
                    System.Configuration.Configuration? config = null;

                    if (Directory.Exists(directory))
                    {
                        string filename = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                        foreach (string s in from s in Directory.EnumerateFiles(directory, "*.config", SearchOption.TopDirectoryOnly) where filename.Equals(Path.GetFileNameWithoutExtension(s)) select s)
                        {
                            System.Configuration.ExeConfigurationFileMap exeConfigurationFileMap = new System.Configuration.ExeConfigurationFileMap();

                            exeConfigurationFileMap.ExeConfigFilename = s;
                            config = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, System.Configuration.ConfigurationUserLevel.None);
                        }
                    }

                    if (config == null)
                    {
                        config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                    }

                    if (config.AppSettings.Settings["Source"] == null)
                    {
                        config.AppSettings.Settings.Add("Source", this.source);
                    }
                    else
                    {
                        config.AppSettings.Settings["Source"].Value = this.source;
                    }

                    if (config.AppSettings.Settings["Lock"] == null)
                    {
                        config.AppSettings.Settings.Add("Lock", this.IsLocked.ToString());
                    }
                    else
                    {
                        config.AppSettings.Settings["Lock"].Value = this.IsLocked.ToString();
                    }

                    if (config.AppSettings.Settings["Mute"] == null)
                    {
                        config.AppSettings.Settings.Add("Mute", this.isMuted.ToString());
                    }
                    else
                    {
                        config.AppSettings.Settings["Mute"].Value = this.isMuted.ToString();
                    }

                    config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                }
            }

            base.OnClosing(e);
        }

        private void OnPaint(object sender, CefSharp.OffScreen.OnPaintEventArgs e)
        {
            lock (this.syncObj)
            {
                if (this.isDrawing && !this.browser!.GetBrowser().IsLoading && e.Width == this.browser.Size.Width && e.Height == this.browser.Size.Height && e.DirtyRect.Width > 0 && e.DirtyRect.Height > 0)
                {
                    using (var bitmap = new System.Drawing.Bitmap(e.Width, e.Height, 4 * e.Width, System.Drawing.Imaging.PixelFormat.Format32bppArgb, e.BufferHandle))
                    {
                        var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(e.DirtyRect.X, e.DirtyRect.Y, e.DirtyRect.Width, e.DirtyRect.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

                        using (var b = new System.Drawing.Bitmap(bitmapData.Width, bitmapData.Height, bitmapData.Stride, bitmap.PixelFormat, bitmapData.Scan0))
                        {
                            DrawDesktop(this.hWnd, b, e.DirtyRect.X, e.DirtyRect.Y, e.DirtyRect.Width, e.DirtyRect.Height);
                        }

                        bitmap.UnlockBits(bitmapData);
                    }
                }
            }
        }

        private async void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            var presentationSource = PresentationSource.FromVisual(this);

            if (this.scaleX != presentationSource.CompositionTarget.TransformToDevice.M11 || this.scaleY != presentationSource.CompositionTarget.TransformToDevice.M22)
            {
                this.scaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                this.scaleY = presentationSource.CompositionTarget.TransformToDevice.M22;

                await System.Threading.Tasks.Task.Delay(1000);

                this.browser!.GetBrowser().GetHost().Invalidate(CefSharp.PaintElementType.View);
            }
            else
            {
                var width = (int)Math.Floor(SystemParameters.VirtualScreenWidth * this.scaleX);
                var height = (int)Math.Floor(SystemParameters.VirtualScreenHeight * this.scaleY);

                if (this.browser!.Size.Width != width || this.browser.Size.Height != height)
                {
                    lock (this.syncObj)
                    {
                        this.isDrawing = false;
                    }

                    await this.browser.ResizeAsync(width, height);

                    lock (this.syncObj)
                    {
                        this.isDrawing = true;
                    }

                    await System.Threading.Tasks.Task.Delay(1000);

                    this.browser!.GetBrowser().GetHost().Invalidate(CefSharp.PaintElementType.View);
                }
            }
        }

        public void Refresh()
        {
            this.browser!.GetBrowser().Reload();
        }

        private void SaveDesktop()
        {
            var workerw = IntPtr.Zero;

            NativeMethods.SendMessageTimeout(NativeMethods.FindWindow("Progman", null), 0x052C, new IntPtr(0), IntPtr.Zero, 0x0, 1000, out var result);
            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                var shell = NativeMethods.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);

                if (shell != IntPtr.Zero)
                {
                    workerw = NativeMethods.FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                }

                return true;
            }, IntPtr.Zero);

            var hWorkerwDc = NativeMethods.GetDCEx(workerw, IntPtr.Zero, 0x403);
            var hCompatibleDc = NativeMethods.CreateCompatibleDC(hWorkerwDc);
            var rect = new NativeMethods.RECT();

            NativeMethods.GetWindowRect(workerw, ref rect);

            var width = rect.right - rect.left;
            var height = rect.bottom - rect.top;
            var hBitmap = NativeMethods.CreateCompatibleBitmap(hWorkerwDc, width, height);
            var hGdiObj = NativeMethods.SelectObject(hCompatibleDc, hBitmap);

            NativeMethods.BitBlt(hCompatibleDc, 0, 0, width, height, hWorkerwDc, 0, 0, NativeMethods.SRCCOPY);
            NativeMethods.SelectObject(hCompatibleDc, hGdiObj);
            NativeMethods.DeleteDC(hCompatibleDc);
            NativeMethods.ReleaseDC(workerw, hWorkerwDc);

            this.desktopBitmap = System.Drawing.Image.FromHbitmap(hBitmap);

            NativeMethods.DeleteObject(hBitmap);
        }

        private void RestoreDesktop()
        {
            var workerw = IntPtr.Zero;

            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                var shell = NativeMethods.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);

                if (shell != IntPtr.Zero)
                {
                    workerw = NativeMethods.FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                }

                return true;
            }, IntPtr.Zero);

            var hDc = NativeMethods.GetDCEx(workerw, IntPtr.Zero, 0x403);

            using (var g = System.Drawing.Graphics.FromHdc(hDc))
            {
                g.DrawImage(this.desktopBitmap!, new System.Drawing.PointF(0, 0));
            }

            NativeMethods.ReleaseDC(workerw, hDc);
        }

        private void DrawDesktop(IntPtr hWndSource, System.Drawing.Bitmap bitmap, int x, int y, int width, int height, bool force = false)
        {
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(hWndSource))
            {
                var hDc = g.GetHdc();
                var workerw = IntPtr.Zero;

                NativeMethods.EnumWindows((hwnd, lParam) =>
                {
                    var shell = NativeMethods.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);

                    if (shell != IntPtr.Zero)
                    {
                        workerw = NativeMethods.FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                    }

                    return true;
                }, IntPtr.Zero);

                if (workerw != IntPtr.Zero)
                {
                    var hCompatibleDc = NativeMethods.CreateCompatibleDC(hDc);
                    var hWorkerwDc = NativeMethods.GetDCEx(workerw, IntPtr.Zero, 0x403);
                    var hBitmap = bitmap.GetHbitmap();
                    var hGdiObj = NativeMethods.SelectObject(hCompatibleDc, hBitmap);

                    if (force)
                    {
                        NativeMethods.BitBlt(hWorkerwDc, 0, 0, bitmap.Width, bitmap.Height, hCompatibleDc, 0, 0, NativeMethods.SRCCOPY);
                    }
                    else
                    {
                        NativeMethods.BitBlt(hWorkerwDc, x, y, width, height, hCompatibleDc, 0, 0, NativeMethods.SRCCOPY);
                    }

                    NativeMethods.SelectObject(hCompatibleDc, hGdiObj);

                    NativeMethods.DeleteObject(hBitmap);
                    NativeMethods.DeleteDC(hCompatibleDc);
                    g.ReleaseHdc(hDc);
                    NativeMethods.ReleaseDC(workerw, hWorkerwDc);
                }
            }
        }
    }
}
