using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodexApps {
    internal static class Native {
        internal delegate bool WindowCallback(IntPtr hwnd, IntPtr param);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(WindowCallback callback, IntPtr param);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr hwnd, int command);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls, string title);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        internal static void ThemeTitleBar(Form form) {
            if (!form.IsHandleCreated) return;
            int dark=ThemePalette.Current().Dark ? 1 : 0;
            try { DwmSetWindowAttribute(form.Handle,20,ref dark,sizeof(int)); }
            catch(DllNotFoundException) { } catch(EntryPointNotFoundException) { }
        }

        internal static IntPtr FindApplicationWindow(int processId, string title) {
            IntPtr result = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hwnd, IntPtr unused) {
                uint owner; GetWindowThreadProcessId(hwnd, out owner);
                if (owner != processId) return true;
                var text = new StringBuilder(512); GetWindowText(hwnd, text, text.Capacity);
                // Includes hidden tray-owned controls; ignores invisible helper windows.
                if (text.ToString().Equals(title, StringComparison.OrdinalIgnoreCase)) {
                    result = hwnd; return false;
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }
        internal static void Reveal(IntPtr hwnd) {
            ShowWindowAsync(hwnd, IsIconic(hwnd) ? 9 : 5);
            SetForegroundWindow(hwnd);
        }
    }

}
