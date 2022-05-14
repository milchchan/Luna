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

            var frame = new Frame() { Opacity = 0 };
            var contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            var sourceMenuStrip = new System.Windows.Forms.ToolStripMenuItem(Luna.Resources.Source);
            var muteMenuStrip = new System.Windows.Forms.ToolStripMenuItem(Luna.Resources.Mute, null, (sender, args) =>
            {
                System.Windows.Forms.ToolStripMenuItem? menuItem = sender as System.Windows.Forms.ToolStripMenuItem;
                Frame? frame = this.MainWindow as Frame;

                if (menuItem != null && frame != null)
                {
                    frame.IsMuted = menuItem.Checked = !menuItem.Checked;
                }
            }) { Checked = frame.IsMuted };
            var lockMenuStrip = new System.Windows.Forms.ToolStripMenuItem(Luna.Resources.Lock, null, (sender, args) =>
            {
                System.Windows.Forms.ToolStripMenuItem? menuItem = sender as System.Windows.Forms.ToolStripMenuItem;
                Frame? frame = this.MainWindow as Frame;

                if (menuItem != null && frame != null)
                {
                    frame.IsLocked = menuItem.Checked = !menuItem.Checked;
                }
            }) { Checked = frame.IsLocked };

            sourceMenuStrip.DropDownItems.Add(Luna.Resources.Browse, null, (sender, args) =>
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
            sourceMenuStrip.DropDownItems.Add(Luna.Resources.Clipboard, null, (sender, args) =>
            {
                System.Windows.Forms.ToolStripMenuItem? menuItem = sender as System.Windows.Forms.ToolStripMenuItem;
                
                if (menuItem != null)
                {
                    string? source = menuItem.Tag as string;
                    Frame? frame = this.MainWindow as Frame;

                    if (source != null && frame != null)
                    {
                        frame.Source = source;
                    }
                }
            });
            contextMenuStrip.Items.Add(sourceMenuStrip);
            contextMenuStrip.Items.Add(muteMenuStrip);
            contextMenuStrip.Items.Add(lockMenuStrip);
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
            this.notifyIcon.Click += this.Click;

            frame.Show();
            frame.Hide();
        }

        private void Click(object? sender, EventArgs e)
        {
            const uint CF_UNICODETEXT = 13;
            string? source;
            System.Windows.Forms.ToolStripMenuItem? menuItem = this.notifyIcon!.ContextMenuStrip.Items[0] as System.Windows.Forms.ToolStripMenuItem;

            if (NativeMethods.IsClipboardFormatAvailable(CF_UNICODETEXT) && NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                IntPtr handle = NativeMethods.GetClipboardData(CF_UNICODETEXT);

                if (handle == IntPtr.Zero)
                {
                    source = null;
                }
                else
                {
                    IntPtr lpwstr = NativeMethods.GlobalLock(handle);

                    if (lpwstr == IntPtr.Zero)
                    {
                        source = null;
                    }
                    else
                    {
                        Uri? uri;

                        if (Uri.TryCreate(System.Runtime.InteropServices.Marshal.PtrToStringUni(lpwstr)!.Trim(), UriKind.Absolute, out uri))
                        {
                            source = uri.ToString();
                        }
                        else
                        {
                            source = null;
                        }
                    }

                    NativeMethods.GlobalUnlock(handle);
                }

                NativeMethods.CloseClipboard();
            }
            else
            {
                source = null;
            }

            if (menuItem != null)
            {
                if (source == null)
                {
                    menuItem.DropDownItems[1].Enabled = false;
                }
                else
                {
                    menuItem.DropDownItems[1].Enabled = true;
                }

                menuItem.DropDownItems[1].Tag = source;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            if (!this.sessionEnding)
            {
                this.notifyIcon!.Click -= this.Click;
                this.notifyIcon.Dispose();
            }
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            base.OnSessionEnding(e);

            this.MainWindow.Close();
            this.notifyIcon!.Click -= this.Click;
            this.notifyIcon.Dispose();
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

        public const uint SRCCOPY = 0x00CC0020;

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, uint dwRop);

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

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern IntPtr GlobalLock(IntPtr hMem);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool GlobalUnlock(IntPtr hMem);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool CloseClipboard();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetClipboardData(uint uFormat);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool IsClipboardFormatAvailable(uint format);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

    }
}
