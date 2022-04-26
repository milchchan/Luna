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
        private CefSharp.OffScreen.ChromiumWebBrowser? browser = null;
        private System.Drawing.Bitmap? desktopBitmap = null;
        private IntPtr hWnd = IntPtr.Zero;
        private bool isDrawing = false;
        private int forceRedraws = 0;
        private int frameRate = 15;
        private string? source = null;
        private Rect[]? screens = null;

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

        public bool IsDuplicate
        {
            get
            {
                if (screens == null)
                {
                    return false;
                }

                return true;
            }
            set
            {
                if (value)
                {
                    this.screens = UpdateScreens();
                }
                else
                {
                    this.screens = null;
                }
            }
        }

        public Frame()
        {
            InitializeComponent();

            System.Configuration.Configuration? config1 = null;
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), System.Reflection.Assembly.GetExecutingAssembly().GetName().Name!);
            string url;

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

                if (config1.AppSettings.Settings["Duplicate"] != null && config1.AppSettings.Settings["Duplicate"].Value.Length > 0)
                {
                    this.IsDuplicate = Boolean.Parse(config1.AppSettings.Settings["Duplicate"].Value);
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
                        this.source = config2.AppSettings.Settings["Scale"].Value;
                    }
                }
                else if (config1.AppSettings.Settings["Source"].Value.Length > 0)
                {
                    this.source = config1.AppSettings.Settings["Source"].Value;
                }

                if (config1.AppSettings.Settings["Duplicate"] == null)
                {
                    if (config2.AppSettings.Settings["Duplicate"] != null && config2.AppSettings.Settings["Duplicate"].Value.Length > 0)
                    {
                        this.IsDuplicate = Boolean.Parse(config2.AppSettings.Settings["Duplicate"].Value);
                    }
                }
                else if (config1.AppSettings.Settings["Duplicate"].Value.Length > 0)
                {
                    this.IsDuplicate = Boolean.Parse(config1.AppSettings.Settings["Duplicate"].Value);
                }
            }

            //CefSharp.Cef.EnableHighDPISupport();
            CefSharp.Cef.Initialize(new CefSharp.OffScreen.CefSettings
            {
                WindowlessRenderingEnabled = true,
                CachePath = null,
                Locale = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName,
                AcceptLanguageList = System.Globalization.CultureInfo.CurrentCulture.Name
            });

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

            this.browser = new CefSharp.OffScreen.ChromiumWebBrowser(url, new CefSharp.BrowserSettings() { WindowlessFrameRate = this.frameRate }, new CefSharp.RequestContext(new CefSharp.RequestContextSettings()));

            if (this.IsDuplicate)
            {
                this.browser.Size = new System.Drawing.Size((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
            }
            else
            {
                this.browser.Size = new System.Drawing.Size((int)SystemParameters.VirtualScreenWidth, (int)SystemParameters.VirtualScreenHeight);
            }

            this.browser.Paint += (object sender, CefSharp.OffScreen.OnPaintEventArgs e) =>
            {
                lock (this.syncObj)
                {
                    if (this.isDrawing)
                    {
                        using (var bitmap = new System.Drawing.Bitmap(e.Width, e.Height, 4 * e.Width, System.Drawing.Imaging.PixelFormat.Format32bppArgb, e.BufferHandle))
                        {
                            this.forceRedraws = 1;

                            if (this.forceRedraws > 0)
                            {
                                DrawDesktop(this.hWnd, bitmap, 0, 0, (int)SystemParameters.VirtualScreenWidth, (int)SystemParameters.VirtualScreenHeight, true);
                                this.forceRedraws--;
                            }
                            else
                            {
                                var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(e.DirtyRect.X, e.DirtyRect.Y, e.DirtyRect.Width, e.DirtyRect.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

                                using (var b = new System.Drawing.Bitmap(bitmapData.Width, bitmapData.Height, bitmapData.Stride, bitmap.PixelFormat, bitmapData.Scan0))
                                {
                                    DrawDesktop(this.hWnd, b, e.DirtyRect.X, e.DirtyRect.Y, e.DirtyRect.Width, e.DirtyRect.Height, false);
                                }

                                bitmap.UnlockBits(bitmapData);
                            }
                        }
                    }
                }
            }
            !;

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged!;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            System.Windows.Interop.HwndSource? hwndSource = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;

            if (hwndSource != null)
            {
                const int WH_MOUSE_LL = 14;

                this.hWnd = hwndSource.Handle;

                if (this == Application.Current.MainWindow)
                {
                    using (var curentProcess = System.Diagnostics.Process.GetCurrentProcess())
                    using (var mainModule = curentProcess.MainModule)
                    {
                        Frame.s_hHook = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, (int nCode, IntPtr wParam, IntPtr lParam) =>
                        {
                            if (nCode >= 0)
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

                                        if (this.screens == null)
                                        {
                                            frame.browser!.GetBrowser().GetHost().SendMouseMoveEvent(new CefSharp.MouseEvent(hookStruct.pt.x, hookStruct.pt.y, CefSharp.CefEventFlags.None), false);
                                        }
                                        else
                                        {
                                            foreach (var screen in this.screens)
                                            {
                                                if (screen.X <= hookStruct.pt.x && hookStruct.pt.x < screen.X + screen.Width && screen.Y <= hookStruct.pt.y && hookStruct.pt.y < screen.Y + screen.Height)
                                                {
                                                    frame.browser!.GetBrowser().GetHost().SendMouseMoveEvent(new CefSharp.MouseEvent((int)(SystemParameters.PrimaryScreenWidth / screen.Width * (hookStruct.pt.x - screen.X)), (int)(SystemParameters.PrimaryScreenHeight / screen.Height * (hookStruct.pt.y - screen.Y)), CefSharp.CefEventFlags.None), false);

                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (message == WM_LBUTTONDOWN)
                                {
                                    Frame? frame = Application.Current.MainWindow as Frame;

                                    if (frame != null)
                                    {
                                        NativeMethods.MSLLHOOKSTRUCT hookStruct = (NativeMethods.MSLLHOOKSTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT))!;

                                        if (this.screens == null)
                                        {
                                            frame.browser!.GetBrowser().GetHost().SendMouseClickEvent(new CefSharp.MouseEvent(hookStruct.pt.x, hookStruct.pt.y, CefSharp.CefEventFlags.None), CefSharp.MouseButtonType.Left, false, 1);
                                        }
                                        else
                                        {
                                            foreach (var screen in this.screens)
                                            {
                                                if (screen.X <= hookStruct.pt.x && hookStruct.pt.x < screen.X + screen.Width && screen.Y <= hookStruct.pt.y && hookStruct.pt.y < screen.Y + screen.Height)
                                                {
                                                    frame.browser!.GetBrowser().GetHost().SendMouseClickEvent(new CefSharp.MouseEvent((int)(SystemParameters.PrimaryScreenWidth / screen.Width * (hookStruct.pt.x - screen.X)), (int)(SystemParameters.PrimaryScreenHeight / screen.Height * (hookStruct.pt.y - screen.Y)), CefSharp.CefEventFlags.None), CefSharp.MouseButtonType.Left, false, 1);

                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (message == WM_LBUTTONUP)
                                {
                                    Frame? frame = Application.Current.MainWindow as Frame;

                                    if (frame != null)
                                    {
                                        NativeMethods.MSLLHOOKSTRUCT hookStruct = (NativeMethods.MSLLHOOKSTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT))!;

                                        if (this.screens == null)
                                        {
                                            frame.browser!.GetBrowser().GetHost().SendMouseClickEvent(new CefSharp.MouseEvent(hookStruct.pt.x, hookStruct.pt.y, CefSharp.CefEventFlags.None), CefSharp.MouseButtonType.Left, true, 1);
                                        }
                                        else
                                        {
                                            foreach (var screen in this.screens)
                                            {
                                                if (screen.X <= hookStruct.pt.x && hookStruct.pt.x < screen.X + screen.Width && screen.Y <= hookStruct.pt.y && hookStruct.pt.y < screen.Y + screen.Height)
                                                {
                                                    frame.browser!.GetBrowser().GetHost().SendMouseClickEvent(new CefSharp.MouseEvent((int)(SystemParameters.PrimaryScreenWidth / screen.Width * (hookStruct.pt.x - screen.X)), (int)(SystemParameters.PrimaryScreenHeight / screen.Height * (hookStruct.pt.y - screen.Y)), CefSharp.CefEventFlags.None), CefSharp.MouseButtonType.Left, true, 1);

                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            return NativeMethods.CallNextHookEx(Frame.s_hHook, nCode, wParam, lParam);
                        }, NativeMethods.GetModuleHandle(mainModule!.ModuleName!), 0);
                    }
                }

                hwndSource.AddHook(new System.Windows.Interop.HwndSourceHook(this.WndProc));
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            NativeMethods.SendMessageTimeout(NativeMethods.FindWindow("Progman", null), 0x052C, new IntPtr(0), IntPtr.Zero, 0x0, 1000, out var result);

            lock (this.syncObj)
            {
                //SaveDesktop();

                this.isDrawing = true;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DESTROY = 0x0002;

            switch (msg)
            {
                case WM_DESTROY:
                    const uint SPI_GETDESKWALLPAPER = 0x73;
                    const uint SPI_SETDESKWALLPAPER = 0x0014;
                    const int MAX_PATH = 260;
                    const uint SPIF_UPDATEINIFILE = 1;
                    const uint SPIF_SENDWININICHANGE = 2;
                    string path = new String('\0', MAX_PATH);

                    lock (this.syncObj)
                    {
                        this.isDrawing = false;

                        NativeMethods.SystemParametersInfo(SPI_GETDESKWALLPAPER, (UInt32)path.Length, path, 0);
                        NativeMethods.SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path.Substring(0, path.IndexOf('\0')), SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);

                        //RestoreDesktop();
                        //this.desktopBitmap.Dispose();
                    }

                    if (Frame.s_hHook != IntPtr.Zero)
                    {
                        NativeMethods.UnhookWindowsHookEx(Frame.s_hHook);
                    }

                    Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged!;

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
                        
                        if (config.AppSettings.Settings["Duplicate"] == null)
                        {
                            config.AppSettings.Settings.Add("Duplicate", this.IsDuplicate.ToString());
                        }
                        else
                        {
                            config.AppSettings.Settings["Duplicate"].Value = this.IsDuplicate.ToString();
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

                                if (config.AppSettings.Settings["Duplicate"] == null)
                                {
                                    config.AppSettings.Settings.Add("Duplicate", this.IsDuplicate.ToString());
                                }
                                else
                                {
                                    config.AppSettings.Settings["Duplicate"].Value = this.IsDuplicate.ToString();
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

                                if (config.AppSettings.Settings["Duplicate"] == null)
                                {
                                    config.AppSettings.Settings.Add("Duplicate", this.IsDuplicate.ToString());
                                }
                                else
                                {
                                    config.AppSettings.Settings["Duplicate"].Value = this.IsDuplicate.ToString();
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

                            if (config.AppSettings.Settings["Duplicate"] == null)
                            {
                                config.AppSettings.Settings.Add("Duplicate", this.IsDuplicate.ToString());
                            }
                            else
                            {
                                config.AppSettings.Settings["Duplicate"].Value = this.IsDuplicate.ToString();
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

                    if (config.AppSettings.Settings["Duplicate"] == null)
                    {
                        config.AppSettings.Settings.Add("Duplicate", this.IsDuplicate.ToString());
                    }
                    else
                    {
                        config.AppSettings.Settings["Duplicate"].Value = this.IsDuplicate.ToString();
                    }

                    config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                }
            }

            base.OnClosing(e);
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (this.IsDuplicate)
            {
                this.screens = UpdateScreens();
                this.browser!.Size = new System.Drawing.Size((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
            }
            else
            {
                this.browser!.Size = new System.Drawing.Size((int)SystemParameters.VirtualScreenWidth, (int)SystemParameters.VirtualScreenHeight);
            }
            
            this.forceRedraws = this.frameRate;
        }

        public void Refresh()
        {
            this.browser!.GetBrowser().Reload();
        }

        private Rect[] UpdateScreens()
        {
            var columns = (int)Math.Ceiling(SystemParameters.VirtualScreenWidth / SystemParameters.PrimaryScreenWidth);
            var rows = (int)Math.Ceiling(SystemParameters.VirtualScreenHeight / SystemParameters.PrimaryScreenHeight);
            var screens = new Rect[columns * rows];
            var height = SystemParameters.VirtualScreenHeight;

            for (var y = 0; y < rows; y++)
            {
                var width = SystemParameters.VirtualScreenWidth;

                for (var x = 0; x < columns; x++)
                {
                    screens[x + y * columns] = new Rect(x * SystemParameters.PrimaryScreenWidth, y * SystemParameters.PrimaryScreenHeight, width >= SystemParameters.PrimaryScreenWidth ? SystemParameters.PrimaryScreenWidth : width, height >= SystemParameters.PrimaryScreenHeight ? SystemParameters.PrimaryScreenHeight : height);
                    width -= SystemParameters.PrimaryScreenWidth;
                }

                height -= SystemParameters.PrimaryScreenHeight;
            }
            
            return screens;
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

        private void DrawDesktop(IntPtr hWndSource, System.Drawing.Bitmap bitmap, int x, int y, int width, int height, bool force)
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

                var hCompatibleDc = NativeMethods.CreateCompatibleDC(hDc);
                var hWorkerwDc = NativeMethods.GetDCEx(workerw, IntPtr.Zero, 0x403);
                var hBitmap = bitmap.GetHbitmap();
                var hGdiObj = NativeMethods.SelectObject(hCompatibleDc, hBitmap);

                if (force)
                {
                    if (this.screens == null)
                    {
                        NativeMethods.BitBlt(hWorkerwDc, 0, 0, bitmap.Width, bitmap.Height, hCompatibleDc, 0, 0, NativeMethods.SRCCOPY);
                    }
                    else
                    {
                        NativeMethods.SetStretchBltMode(hWorkerwDc, NativeMethods.StretchBltMode.STRETCH_DELETESCANS);
                        
                        foreach (var screen in this.screens)
                        {
                            NativeMethods.StretchBlt(hWorkerwDc, (int)screen.X, (int)screen.Y, (int)(width * screen.Width / SystemParameters.PrimaryScreenWidth), (int)(height * screen.Height / SystemParameters.PrimaryScreenHeight), hCompatibleDc, 0, 0, bitmap.Width, bitmap.Height, NativeMethods.SRCCOPY);
                        }
                    }
                }
                else if (this.screens == null)
                {
                    NativeMethods.BitBlt(hWorkerwDc, x, y, width, height, hCompatibleDc, 0, 0, NativeMethods.SRCCOPY);
                }
                else
                {
                    NativeMethods.SetStretchBltMode(hWorkerwDc, NativeMethods.StretchBltMode.STRETCH_DELETESCANS);

                    foreach (var screen in this.screens)
                    {
                        NativeMethods.StretchBlt(hWorkerwDc, (int)(screen.X + screen.Width / SystemParameters.PrimaryScreenWidth * x), (int)(screen.Y + screen.Height / SystemParameters.PrimaryScreenHeight * y), (int)(width * screen.Width / SystemParameters.PrimaryScreenWidth), (int)(height * screen.Height / SystemParameters.PrimaryScreenHeight), hCompatibleDc, 0, 0, bitmap.Width, bitmap.Height, NativeMethods.SRCCOPY);
                    }
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
