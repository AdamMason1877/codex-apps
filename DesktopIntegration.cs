using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexApps {
    internal sealed class TaskbarButton {
        internal Rectangle Button, Taskbar;
        internal string Name;
    }

    internal static class TaskbarAnchor {
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder text, int size);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out RECT rect);
        [DllImport("user32.dll")] static extern bool PhysicalToLogicalPointForPerMonitorDPI(IntPtr window, ref Point point);

        // Runs on an MTA worker, never on the WinForms message thread. Read only:
        // no shell input, menu invocation, Explorer injection, or taskbar changes.
        internal static List<TaskbarButton> Find(IntPtr owner) {
            var handles = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr window, IntPtr unused) {
                var name = new StringBuilder(100); GetClassName(window, name, name.Capacity);
                if (name.ToString() == "Shell_TrayWnd" || name.ToString() == "Shell_SecondaryTrayWnd") handles.Add(window);
                return true;
            }, IntPtr.Zero);
            var matches = new List<TaskbarButton>();
            foreach (var handle in handles) {
                try {
                    RECT tray; if (!GetWindowRect(handle, out tray)) continue;
                    var root = AutomationElement.FromHandle(handle);
                    var elements = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                    foreach (AutomationElement element in elements) {
                        string name = element.Current.Name ?? "";
                        string id = element.Current.AutomationId ?? "";
                        if (!Matches(name, id) || element.Current.IsOffscreen) continue;
                        var physical = element.Current.BoundingRectangle;
                        if (physical.IsEmpty || physical.Width < 1 || physical.Height < 1) continue;
                        var top = new Point((int)Math.Round(physical.Left), (int)Math.Round(physical.Top));
                        var bottom = new Point((int)Math.Round(physical.Right), (int)Math.Round(physical.Bottom));
                        // UIA rectangles are physical; Screen/Bounds use the caller's
                        // DPI coordinate space. Convert instead of guessing a scale.
                        PhysicalToLogicalPointForPerMonitorDPI(owner, ref top);
                        PhysicalToLogicalPointForPerMonitorDPI(owner, ref bottom);
                        matches.Add(new TaskbarButton { Name = name, Button = Rectangle.FromLTRB(top.X, top.Y, bottom.X, bottom.Y), Taskbar = Rectangle.FromLTRB(tray.Left, tray.Top, tray.Right, tray.Bottom) });
                    }
                } catch (ElementNotAvailableException) { }
                  catch (System.Windows.Automation.ElementNotEnabledException) { }
                  catch (COMException) { }
                  catch (InvalidOperationException) { }
            }
            return matches;
        }
        internal static bool Matches(string name, string id) {
            return name.Equals("Codex Apps", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Codex Apps,", StringComparison.OrdinalIgnoreCase)
                || id.EndsWith("\\codex apps.exe", StringComparison.OrdinalIgnoreCase);
        }
        internal static TaskbarButton Select(List<TaskbarButton> buttons, Point invocation, Rectangle? previous, Rectangle primary) {
            return buttons.FirstOrDefault(b => b.Taskbar.Contains(invocation))
                ?? (previous.HasValue ? buttons.FirstOrDefault(b => b.Taskbar.IntersectsWith(previous.Value)) : null)
                ?? buttons.FirstOrDefault(b => b.Taskbar.IntersectsWith(primary))
                ?? buttons.FirstOrDefault();
        }
        internal static bool PreferInvokedScreen(Rectangle screen, Rectangle work, Point invocation, Rectangle? anchorScreen) {
            // A click in the taskbar area must take precedence over an anchor
            // retained from the last monitor while UI Automation catches up.
            return screen.Contains(invocation) && !work.Contains(invocation)
                && (!anchorScreen.HasValue || anchorScreen.Value != screen);
        }
    }

    internal sealed class ThemePalette {
        internal Color Background, Surface, Text, Muted, Hover, Pressed, Border, Accent, SelectedText;
        internal bool Dark, HighContrast;
        internal static Color Blend(Color a, Color b, double weight) {
            return Color.FromArgb((int)(a.R * (1-weight) + b.R * weight), (int)(a.G * (1-weight) + b.G * weight), (int)(a.B * (1-weight) + b.B * weight));
        }
        internal static ThemePalette Create(bool light, bool highContrast, Color accent, bool tinted) {
            if (highContrast) return new ThemePalette { HighContrast = true, Background = SystemColors.Window, Surface = SystemColors.Window,
                Text = SystemColors.WindowText, Muted = SystemColors.WindowText, Hover = SystemColors.Highlight, Pressed = SystemColors.Highlight,
                Border = SystemColors.WindowText, Accent = SystemColors.Highlight, SelectedText = SystemColors.HighlightText };
            Color background = light ? Color.FromArgb(243,243,243) : Color.FromArgb(32,32,32);
            return new ThemePalette { Dark = !light, Background = tinted ? Blend(background, accent, 0.12) : background,
                Surface = light ? Color.FromArgb(251,251,251) : Color.FromArgb(43,43,43), Text = light ? Color.FromArgb(26,26,26) : Color.FromArgb(245,245,245),
                Muted = light ? Color.FromArgb(85,85,85) : Color.FromArgb(190,190,190), Hover = Blend(background, accent, light ? 0.14 : 0.22),
                Pressed = Blend(background, accent, light ? 0.23 : 0.32), Border = light ? Color.FromArgb(207,207,207) : Color.FromArgb(76,76,76),
                Accent = accent, SelectedText = light ? Color.FromArgb(26,26,26) : Color.FromArgb(245,245,245) };
        }
        [DllImport("dwmapi.dll")] static extern int DwmGetColorizationColor(out uint color, out bool opaque);
        internal static ThemePalette Current() {
            bool light = true, tinted = false;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                if (key != null) {
                    light = Convert.ToInt32(key.GetValue("SystemUsesLightTheme", key.GetValue("AppsUseLightTheme", 1))) != 0;
                    tinted = Convert.ToInt32(key.GetValue("ColorPrevalence", 0)) != 0;
                }
            }
            Color accent = SystemColors.Highlight;
            uint argb; bool opaque;
            if (DwmGetColorizationColor(out argb, out opaque) == 0) accent = Color.FromArgb((int)((argb >> 16) & 255), (int)((argb >> 8) & 255), (int)(argb & 255));
            return Create(light, SystemInformation.HighContrast, accent, tinted);
        }
        internal string Signature { get { return String.Join(",", new[] { Background, Surface, Text, Muted, Hover, Pressed, Border, Accent }.Select(c => c.ToArgb().ToString()).ToArray()); } }
    }

    internal sealed class ThemeRenderer : ToolStripProfessionalRenderer {
        readonly ThemePalette palette;
        internal ThemeRenderer(ThemePalette palette) : base(new MenuColors(palette)) { this.palette = palette; RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Selected ? palette.SelectedText : palette.Text; base.OnRenderItemText(e); }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = palette.Text; base.OnRenderArrow(e); }
    }
}
