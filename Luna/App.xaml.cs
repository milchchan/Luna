using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Luna
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool sessionEnding = false;
        private System.Windows.Forms.NotifyIcon? notifyIcon = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            var frame = new Frame() { Opacity = 0 };

            contextMenuStrip.Items.Add(Luna.Resources.Switch, null, (sender, args) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();

                dialog.Multiselect = false;
                dialog.Filter = Luna.Resources.Filter;

                if (dialog.ShowDialog() == true)
                {
                    Frame? frame = this.MainWindow as Frame;

                    if (frame != null)
                    {
                        frame.Source = dialog.FileName;
                    }
                }
            });
            contextMenuStrip.Items.Add(Luna.Resources.Refresh, null, (sender, args) =>
            {
                Frame? frame = this.MainWindow as Frame;

                if (frame != null)
                {
                    frame.Refresh();
                }
            });
            contextMenuStrip.Items.Add("-");
            contextMenuStrip.Items.Add(Luna.Resources.Exit, null, (sender, args) =>
            {
                this.MainWindow.Close();
            });

            this.notifyIcon = new System.Windows.Forms.NotifyIcon { Visible = true, Icon = new System.Drawing.Icon(GetResourceStream(new Uri("Luna.ico", UriKind.Relative)).Stream), Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, ContextMenuStrip = contextMenuStrip };

            frame.Show();
            frame.Hide();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            if (!this.sessionEnding)
            {
                this.notifyIcon!.Dispose();
            }
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            base.OnSessionEnding(e);

            this.MainWindow.Close();
            this.notifyIcon!.Dispose();
            this.sessionEnding = true;
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Trace.WriteLine(e.Exception.ToString());
        }
    }

    internal static class NativeMethods
    {
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetWindowRect(IntPtr hWnd, ref RECT rect);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetDCEx(IntPtr hWnd, IntPtr hrgnClip, ulong flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeoue, out IntPtr pdwResult);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public const int SRCCOPY = 0x00CC0020; // BitBlt dwRop parameter

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hDC);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern bool SystemParametersInfo(uint uAction, uint uParam, string lpvParam, uint fuWinIni);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        public static extern int GetCurrentPackageFullName(ref int packageFullNameLength, System.Text.StringBuilder packageFullName);
    }
}
