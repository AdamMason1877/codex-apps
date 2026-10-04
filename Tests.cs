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
    internal static class Tests {
        internal static int Run() {
            var apps = new[] { new AppEntry { name="Example one", path=Application.ExecutablePath }, new AppEntry { name="Example two", path=Application.ExecutablePath } };
            var results = new List<string>();
            foreach (var app in apps) Applications.RequireTarget(app);
            string folder=AppDomain.CurrentDomain.BaseDirectory;
            Configuration.Load(Path.Combine(folder,"apps.json"));
            var nested=Configuration.Parse("[{\"name\":\"Audio\",\"items\":[{\"name\":\"Utilities\",\"items\":[{\"name\":\"Test player\",\"path\":\"tools/player.exe\",\"arguments\":\"--open demo\",\"workingDirectory\":\"tools\"}]}]}]",folder);
            var leaf=nested[0].items[0].items[0];
            if(leaf.path!=Path.GetFullPath(Path.Combine(folder,"tools/player.exe")) || leaf.workingDirectory!=Path.Combine(folder,"tools") || leaf.arguments!="--open demo") throw new Exception("Relative paths or arguments were not preserved.");
            var expanded=Configuration.Parse("[{\"name\":\"Editor\",\"path\":\"%WINDIR%/notepad.exe\"}]",folder);
            if(expanded[0].path!=Path.Combine(Environment.GetEnvironmentVariable("WINDIR"),"notepad.exe")) throw new Exception("Environment path expansion failed.");
            foreach(string invalid in new[] { "null", "[{}]", "[{\"name\":\"Mixed\",\"items\":[{\"name\":\"Child\",\"path\":\"a.exe\"}],\"path\":\"b.exe\"}]", "[{\"name\":\"Missing\"}]", "not json" }) {
                bool rejected=false; try { Configuration.Parse(invalid,folder); } catch(InvalidDataException) { rejected=true; }
                if(!rejected) throw new Exception("Invalid configuration was accepted: "+invalid);
            }
            OrganizerTests.Run();
            results.Add("PASS: Organizer draft isolation, moves/reordering, cycle prevention, portable settings round-trip, empty categories, file imports, and atomic save validation.");
            string dispatched=null;
            using(var categories=new Launcher(nested,true,false,entry=>dispatched=entry.name)) {
                if(categories.Controls.Find("Accent",true).Length!=0) throw new Exception("Decorative accent bar still exists.");
                var submenu=(ToolStripMenuItem)categories.Menus[0].Items[0];
                if(submenu.Text!="Utilities" || submenu.DropDownItems[0].AccessibleName!="Test player") throw new Exception("Nested category topology was lost.");
                submenu.DropDownItems[0].PerformClick();
                if(dispatched!="Test player") throw new Exception("Nested leaf action did not launch the selected app.");
            }
            var longList=Enumerable.Range(1,30).Select(i=>new AppEntry { name="App "+i, path=Application.ExecutablePath }).ToArray();
            using(var large=new Launcher(longList,true)) {
                large.Show(); Application.DoEvents();
                var list=large.Controls.Find("AppList",true).Single() as Panel;
                if(!list.AutoScroll || !list.VerticalScroll.Visible) throw new Exception("Long root lists must scroll.");
                if(large.Bounds.Height>Screen.FromControl(large).WorkingArea.Height) throw new Exception("Long list escaped the working area.");
                large.Close();
            }
            using(var largeCategory=new Launcher(new[] { new AppEntry { name="Many apps",items=longList } },true)) {
                largeCategory.Show(); largeCategory.Menus[0].Show(largeCategory,new Point(0,0)); Application.DoEvents();
                if(largeCategory.Menus[0].Height>Screen.FromControl(largeCategory).WorkingArea.Height) throw new Exception("Large category menu escaped the screen.");
                if(largeCategory.Menus[0].Items.Count!=30) throw new Exception("Large category dropped entries.");
                largeCategory.Menus[0].Close(); largeCategory.Close();
            }
            results.Add("PASS: Portable paths, environment expansion, arguments, invalid config rejection, nested categories, nested launch dispatch, no accent bar, and scrolling long menus.");
            int count = 0;
            foreach (var work in new[] { new Rectangle(0, 0, 1920, 1032), new Rectangle(-1920, -240, 1920, 1032), new Rectangle(1920, 0, 3840, 2080), new Rectangle(0, 0, 800, 550) }) {
                foreach (var x in new[] { work.Left, work.Left + work.Width / 2, work.Right }) {
                    foreach (var size in new[] { new Size(410, 258), new Size(615, 387), new Size(820, 516), new Size(410, 2000) }) {
                        var bounds = Placement.AboveTaskbar(work, new Point(x, work.Bottom + 20), size, 6);
                        if (!work.Contains(bounds) || bounds.Bottom != work.Bottom - 6) throw new Exception("Drop-up placement escaped the working area: " + bounds);
                        count++;
                    }
                }
            }
            results.Add("PASS: " + count + " geometry cases cover multiple monitors, screen edges, DPI sizes, and overflow.");
            var primaryBar = new Rectangle(0, 1032, 1920, 48);
            var primaryButton = new TaskbarButton { Name = "Codex Apps", Taskbar = primaryBar, Button = new Rectangle(820,1032,48,48) };
            var secondaryButton = new TaskbarButton { Name = "Codex Apps", Taskbar = new Rectangle(-1920,1032,1920,48), Button = new Rectangle(-500,1032,48,48) };
            var buttonsFound = new List<TaskbarButton> { primaryButton, secondaryButton };
            var selected = TaskbarAnchor.Select(buttonsFound, new Point(20,20), null, new Rectangle(0,0,1920,1080));
            if (selected != primaryButton) throw new Exception("Pointer away from taskbar changed the primary icon anchor.");
            selected = TaskbarAnchor.Select(buttonsFound, new Point(-480,1050), null, new Rectangle(0,0,1920,1080));
            if (selected != secondaryButton) throw new Exception("Click on secondary taskbar chose the wrong icon.");
            if (TaskbarAnchor.Select(buttonsFound, new Point(20,20), secondaryButton.Taskbar, new Rectangle(0,0,1920,1080)) != secondaryButton) throw new Exception("Keyboard restore did not retain the last taskbar.");
            var anchored = Placement.AboveTaskbar(new Rectangle(0,0,1920,1032), new Point(primaryButton.Button.Left + primaryButton.Button.Width/2,1032), new Size(410,258),6);
            if (anchored.Left + anchored.Width/2 != primaryButton.Button.Left + primaryButton.Button.Width/2) throw new Exception("Menu is not centered above the actual button.");
            if (!TaskbarAnchor.Matches("Codex Apps, 1 running window", "") || TaskbarAnchor.Matches("Codex", "") || TaskbarAnchor.Matches("Other Codex Apps", "")) throw new Exception("Taskbar matching could confuse the launcher with another app.");
            results.Add("PASS: Exact taskbar icon matching, icon-centered placement, multi-monitor selection, and stable keyboard restore.");
            foreach (bool light in new[] { true, false }) foreach (var accent in new[] { Color.Red, Color.Blue, Color.White, Color.Black }) {
                var theme = ThemePalette.Create(light, false, accent, true);
                foreach (var background in new[] { theme.Background, theme.Surface, theme.Hover, theme.Pressed }) {
                    if (Contrast(theme.Text, background) < 4.5) throw new Exception("Theme text contrast is below 4.5:1.");
                }
                if (theme.Accent != accent || theme.Dark == light) throw new Exception("Theme does not follow the requested mode/accent.");
            }
            var accessible = ThemePalette.Create(false,true,Color.Red,true);
            if (accessible.Background != SystemColors.Window || accessible.Text != SystemColors.WindowText || accessible.SelectedText != SystemColors.HighlightText) throw new Exception("High-contrast palette ignored Windows system colors.");
            results.Add("PASS: Light/dark palettes across four accents meet 4.5:1 text contrast; high contrast uses system colors.");
            bool missing = false;
            try { Applications.RequireTarget(new AppEntry { path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "missing-test-app.exe") }); }
            catch (FileNotFoundException error) { missing = error.Message.Contains("apps.json"); }
            if (!missing) throw new Exception("Missing executable did not produce an actionable error.");
            results.Add("PASS: Missing executable produces an actionable path/configuration error.");
            using (var launcher = new Launcher(apps, true)) {
                var buttons = launcher.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<Button>()).ToArray();
                if (buttons.Length != 4 || buttons.Count(b => apps.Any(a => a.name == b.AccessibleName)) != 2) throw new Exception("Required application buttons missing.");
            }
            results.Add("PASS: Real root form contains both application buttons and both cascade buttons.");
            using (var launcher = new Launcher(apps, false)) {
                launcher.Show(); Application.DoEvents();
                if (launcher.Text != "Codex Apps" || !launcher.Controls.OfType<Label>().Any(l => l.Text == "CODEX APPS")) throw new Exception("Launcher branding does not match Codex Apps.");
                launcher.DismissMenu(); Application.DoEvents();
                if (launcher.IsDisposed || !launcher.ShowInTaskbar || launcher.WindowState != FormWindowState.Minimized) throw new Exception("Dismissal destroyed the taskbar window.");
                launcher.WindowState = FormWindowState.Normal; launcher.Activate(); Application.DoEvents();
                if (!launcher.Visible || launcher.WindowState != FormWindowState.Normal) throw new Exception("Dismissed launcher cannot be restored.");
                bool invoked = false;
                launcher.Act(delegate { invoked = true; }); Application.DoEvents();
                if (!invoked || launcher.IsDisposed || launcher.WindowState != FormWindowState.Minimized) throw new Exception("Application launch destroyed the taskbar window.");
                launcher.WindowState = FormWindowState.Normal; launcher.Activate(); Application.DoEvents();
                // Exercise the same real focus loss caused by Explorer opening
                // a taskbar menu, then let the actual deactivation timer run.
                using (var other = new Form()) {
                    other.Show(); other.Activate();
                    var elapsed = Stopwatch.StartNew();
                    while (elapsed.ElapsedMilliseconds < 500) { Application.DoEvents(); Thread.Sleep(10); }
                    if (launcher.IsDisposed || !launcher.ShowInTaskbar || launcher.WindowState != FormWindowState.Minimized) throw new Exception("Focus loss must retain a minimized taskbar window.");
                }
                launcher.Close();
                if (!launcher.IsDisposed) throw new Exception("Explicit Close must still exit the launcher.");
            }
            results.Add("PASS: Codex Apps branding, minimize-on-dismiss, restore, launch action, real focus loss, and explicit Close.");
            results.Add("NOTE: UI pinning, launch/focus, submenu direction and dismissal need live Windows acceptance checks.");
            File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results.txt"), results);
            return 0;
        }
        static double Luminance(Color color) {
            Func<byte, double> linear = value => { double c = value / 255.0; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); };
            return 0.2126 * linear(color.R) + 0.7152 * linear(color.G) + 0.0722 * linear(color.B);
        }
        static double Contrast(Color a, Color b) { double x=Luminance(a), y=Luminance(b); return (Math.Max(x,y)+0.05)/(Math.Min(x,y)+0.05); }
    }
}
