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
    internal sealed class Launcher : Form {
        readonly List<ContextMenuStrip> menus = new List<ContextMenuStrip>();
        readonly System.Windows.Forms.Timer dismiss = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer desktopRefresh = new System.Windows.Forms.Timer();
        readonly bool stayOpen;
        readonly bool watchConfiguration;
        readonly Action<AppEntry> launchApplication;
        string configStamp;
        Panel listPanel;
        bool rebuilding;
        internal IList<ContextMenuStrip> Menus { get { return menus.AsReadOnly(); } }
        Size menuSize;
        ThemePalette palette;
        TaskbarButton anchor;
        bool locating;
        Point invocation;
        bool acting;
        internal Launcher(AppEntry[] apps, bool preview, bool watch = false, Action<AppEntry> launch = null) {
            stayOpen = preview; watchConfiguration = watch;
            launchApplication = launch ?? Applications.Open;
            if (watch) configStamp = Configuration.Stamp;
            Text = "Codex Apps";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            palette = ThemePalette.Current();
            BackColor = palette.Background; ForeColor = palette.Text;
            Font = new Font("Segoe UI", 10); DoubleBuffered = true; KeyPreview = true;
            ShowInTaskbar = true;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            BuildMenu(apps);
            dismiss.Interval = 180;
            dismiss.Tick += delegate {
                dismiss.Stop();
                if (!stayOpen && !acting && !ContainsFocus && !menus.Any(m => m.Visible) && Native.GetForegroundWindow() != Handle) DismissMenu();
            };
            Deactivate += delegate { dismiss.Start(); };
            Activated += delegate {
                dismiss.Stop();
                if (WindowState == FormWindowState.Normal) { invocation = Cursor.Position; ReloadConfiguration(); ApplyTheme(); PlaceMenu(); RefreshAnchor(); }
            };
            Resize += delegate {
                if (!rebuilding && WindowState == FormWindowState.Normal && menuSize != Size.Empty) PlaceMenu();
            };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape && !menus.Any(m => m.Visible)) { DismissMenu(); e.Handled = true; } };
            Shown += delegate {
                invocation = Cursor.Position;
                PlaceMenu();
                RefreshAnchor();
                desktopRefresh.Start();
                Activate();
                if (listPanel.Controls.Count > 0) listPanel.Controls[0].Focus();
            };
            desktopRefresh.Interval = 1000;
            desktopRefresh.Tick += delegate { if (WindowState == FormWindowState.Normal) { ApplyTheme(); RefreshAnchor(); } };
            FormClosed += delegate { dismiss.Dispose(); desktopRefresh.Dispose(); foreach (var menu in menus) menu.Dispose(); };
        }
        void BuildMenu(AppEntry[] entries) {
            rebuilding = true;
            try {
                foreach (var menu in menus) menu.Dispose(); menus.Clear();
                foreach (Control control in Controls.Cast<Control>().ToArray()) control.Dispose();
                Controls.Clear();
                int rows = Math.Max(1, Math.Min(entries.Length, 7));
                ClientSize = new Size(410, 106 + rows * 76);
                Controls.Add(new Label { Text="CODEX APPS", Location=new Point(20,18), Size=new Size(300,27), Font=new Font("Segoe UI",12,FontStyle.Bold) });
                var close=MakeButton("\u00d7",new Rectangle(365,12,30,32));
                close.AccessibleName="Close menu"; close.TabIndex=entries.Length*2+1; close.Click+=delegate { Close(); }; Controls.Add(close);
                listPanel=new Panel { Name="AppList", Location=new Point(12,55), Size=new Size(386,rows*76), Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom, AutoScroll=true };
                Controls.Add(listPanel);
                if(entries.Length==0) listPanel.Controls.Add(new Label { Text="No apps yet. Choose Organize to add some.", Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleCenter });
                for(int i=0;i<entries.Length;i++) {
                    AppEntry entry=entries[i];
                    string description=entry.description ?? (entry.IsCategory ? entry.items.Length+" items" : "Open resource");
                    var main=MakeButton(entry.name+"\n"+description,new Rectangle(0,i*76,342,68));
                    main.TextAlign=ContentAlignment.MiddleLeft; main.Padding=new Padding(12,0,2,0); main.TabIndex=i*2; main.AccessibleName=entry.name; main.AutoEllipsis=true; main.UseMnemonic=false;
                    var arrow=MakeButton("\u203a",new Rectangle(342,i*76,44,68));
                    arrow.Font=new Font("Segoe UI",19); arrow.TabIndex=i*2+1; arrow.AccessibleName=entry.name+(entry.IsCategory ? " category" : " actions");
                    var menu=new ContextMenuStrip { Font=new Font("Segoe UI",10), ShowImageMargin=false };
                    if(entry.IsCategory) {
                        foreach(var child in entry.items) menu.Items.Add(CreateNode(child));
                        if(entry.items.Length==0) menu.Items.Add(new ToolStripMenuItem("Empty category - add apps in Organize") { Enabled=false });
                    }
                    else {
                        menu.Items.Add("Open",null,delegate { Act(delegate { launchApplication(entry); }); });
                        menu.Items.Add("Show in File Explorer",null,delegate { Act(delegate { Applications.RequireTarget(entry); Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+entry.path+"\"") { UseShellExecute=true }); }); });
                    }
                    StyleMenu(menu);
                    menu.Closed+=delegate { dismiss.Start(); }; menus.Add(menu);
                    main.Click+=delegate { if(entry.IsCategory) ShowActions(menu,arrow); else Act(delegate { launchApplication(entry); }); };
                    arrow.Click+=delegate { ShowActions(menu,arrow); };
                    main.KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.KeyCode==Keys.Right) { ShowActions(menu,arrow); e.Handled=true; } };
                    listPanel.Controls.Add(main); listPanel.Controls.Add(arrow);
                }
                var footer=new Label { Name="Footer", Text="\u203a for categories and actions", Location=new Point(20,ClientSize.Height-31), Size=new Size(255,24), Anchor=AnchorStyles.Left|AnchorStyles.Bottom, ForeColor=palette.Muted, Font=new Font("Segoe UI",9) };
                Controls.Add(footer);
                var edit=MakeButton("Organize",new Rectangle(302,ClientSize.Height-37,92,30));
                edit.Anchor=AnchorStyles.Right|AnchorStyles.Bottom; edit.TabIndex=entries.Length*2; edit.Click+=delegate { OpenOrganizer(); }; Controls.Add(edit);
                float scale=DeviceScale(); Scale(new SizeF(scale,scale)); menuSize=Size;
                int arrowWidth=(int)(44*scale);
                listPanel.Layout+=delegate {
                    int width=listPanel.ClientSize.Width;
                    for(int i=0;i+1<listPanel.Controls.Count;i+=2) {
                        var main=listPanel.Controls[i]; var arrow=listPanel.Controls[i+1];
                        main.Width=Math.Max(40,width-arrowWidth); arrow.Left=main.Right; arrow.Width=arrowWidth;
                    }
                };
                listPanel.PerformLayout();
            } finally { rebuilding=false; }
        }
        ToolStripMenuItem CreateNode(AppEntry entry) {
            var item=new ToolStripMenuItem(entry.name.Replace("&","&&")) { AccessibleName=entry.name, ToolTipText=entry.description ?? "", Tag=entry };
            if(entry.IsCategory) {
                foreach(var child in entry.items) item.DropDownItems.Add(CreateNode(child));
                if(entry.items.Length==0) item.DropDownItems.Add(new ToolStripMenuItem("Empty category") { Enabled=false });
            }
            else item.Click+=delegate { Act(delegate { launchApplication(entry); }); };
            return item;
        }
        void StyleMenu(ToolStripDropDown menu) {
            menu.BackColor=palette.Background; menu.ForeColor=palette.Text; menu.Renderer=new ThemeRenderer(palette);
            menu.MaximumSize=new Size(600,Math.Max(100,Screen.FromControl(this).WorkingArea.Height-16));
            var dropdown=menu as ToolStripDropDownMenu; if(dropdown!=null) dropdown.ShowImageMargin=false;
            foreach(ToolStripItem child in menu.Items) {
                child.Padding=new Padding(10,8,10,8); child.ForeColor=palette.Text;
                var branch=child as ToolStripMenuItem; if(branch!=null && branch.HasDropDownItems) StyleMenu(branch.DropDown);
            }
        }
        void ReloadConfiguration() {
            if(!watchConfiguration || rebuilding || acting) return;
            string stamp=Configuration.Stamp;
            if(stamp==configStamp) return;
            configStamp=stamp; // Show a parse error once per edit, retaining the last valid menu.
            try { var entries=Configuration.Load(Configuration.ActiveFile); BuildMenu(entries); }
            catch(Exception error) { acting=true; try { MessageBox.Show(this,error.Message+"\n\nYour previous menu is still available.","Cannot reload apps",MessageBoxButtons.OK,MessageBoxIcon.Information); } finally { acting=false; } }
        }
        void OpenOrganizer() {
            acting=true; dismiss.Stop();
            try { DismissMenu(); using(var organizer=new Organizer()) organizer.ShowDialog(this); }
            catch(Exception error) { MessageBox.Show(error.Message,"Cannot open organizer",MessageBoxButtons.OK,MessageBoxIcon.Information); }
            finally { acting=false; WindowState=FormWindowState.Normal; ReloadConfiguration(); PlaceMenu(); Activate(); }
        }
        void PlaceMenu() {
            if (rebuilding || menuSize == Size.Empty) return;
            var clickedScreen = Screen.FromPoint(invocation);
            var anchorScreen = anchor == null ? null : Screen.FromRectangle(anchor.Button);
            bool useClick = TaskbarAnchor.PreferInvokedScreen(clickedScreen.Bounds, clickedScreen.WorkingArea,
                invocation, anchorScreen == null ? (Rectangle?)null : anchorScreen.Bounds);
            var screen = useClick ? clickedScreen : anchorScreen ?? Screen.PrimaryScreen;
            var work = screen.WorkingArea;
            var center = useClick ? new Point(invocation.X, work.Bottom)
                : anchor == null ? new Point(work.Left + work.Width / 2, work.Bottom)
                : new Point(anchor.Button.Left + anchor.Button.Width / 2, anchor.Button.Top);
            var bounds = Placement.AboveTaskbar(work, center, menuSize, Math.Max(4, (int)(6 * DeviceScale())));
            if (Bounds != bounds) Bounds = bounds;
        }
        async void RefreshAnchor() {
            if (locating || IsDisposed || !IsHandleCreated) return;
            locating = true;
            var handle = Handle;
            var click = invocation;
            bool retry = false;
            try {
                var matches = await Task.Run(() => TaskbarAnchor.Find(handle));
                if (IsDisposed) return;
                // A later activation may have happened while this lookup was
                // in flight. Never let its old click move the menu back.
                if (click != invocation) { retry = true; return; }
                var found = TaskbarAnchor.Select(matches, click, anchor == null ? (Rectangle?)null : anchor.Taskbar, Screen.PrimaryScreen.Bounds);
                if (found != null) anchor = found;
                if (WindowState == FormWindowState.Normal) PlaceMenu();
                WriteDiagnostics(found != null);
            } catch (Exception) { /* Explorer can restart while its accessibility tree is being read. */ }
            finally { locating = false; if (retry && !IsDisposed) RefreshAnchor(); }
        }
        void WriteDiagnostics(bool resolved) {
            try {
                var json = new JavaScriptSerializer().Serialize(new { theme = palette.HighContrast ? "High contrast" : palette.Dark ? "Dark" : "Light", accent = palette.Accent.ToArgb(), anchorResolved = resolved,
                    button = anchor == null ? Rectangle.Empty : anchor.Button, taskbar = anchor == null ? Rectangle.Empty : anchor.Taskbar, menu = Bounds });
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop-state.json");
                if (!File.Exists(file) || File.ReadAllText(file) != json) File.WriteAllText(file, json);
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        void ApplyTheme() {
            var current = ThemePalette.Current();
            if (current.Signature == palette.Signature) return;
            palette = current; BackColor = palette.Background; ForeColor = palette.Text;
            ApplyControls(this);
            foreach (var menu in menus) StyleMenu(menu);
            Invalidate(true);
        }
        void ApplyControls(Control parent) {
            foreach (Control control in parent.Controls) {
                control.BackColor = palette.Background;
                control.ForeColor = control.Name == "Footer" ? palette.Muted : palette.Text;
                var button = control as Button;
                if (button != null) { button.BackColor = palette.Surface; button.FlatAppearance.MouseOverBackColor = palette.Hover; button.FlatAppearance.MouseDownBackColor = palette.Pressed; }
                ApplyControls(control);
            }
        }
        protected override void WndProc(ref Message message) {
            base.WndProc(ref message);
            if (palette != null && IsHandleCreated && !IsDisposed && (message.Msg == 0x001A || message.Msg == 0x031A || message.Msg == 0x0320 || message.Msg == 0x0015)) ApplyTheme();
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            using (var pen = new Pen(palette.Border)) e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
        internal void DismissMenu() {
            dismiss.Stop();
            // Keep the taskbar window alive: destroying it while Explorer is
            // opening its jump list also destroys the user's right-click menu.
            foreach (var menu in menus) menu.Close();
            WindowState = FormWindowState.Minimized;
        }
        float DeviceScale() { using (var graphics = CreateGraphics()) return graphics.DpiX / 96f; }
        Button MakeButton(string text, Rectangle bounds) {
            var button = new Button { Text = text, Bounds = bounds, FlatStyle = FlatStyle.Flat, BackColor = palette.Surface, ForeColor = palette.Text, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = palette.Hover;
            button.FlatAppearance.MouseDownBackColor = palette.Pressed;
            button.MouseEnter += delegate { if (palette.HighContrast) button.ForeColor = palette.SelectedText; };
            button.MouseLeave += delegate { button.ForeColor = palette.Text; };
            return button;
        }
        void ShowActions(ContextMenuStrip menu, Control arrow) {
            foreach (var other in menus) if (other != menu) other.Close();
            StyleMenu(menu);
            var origin = arrow.PointToScreen(new Point(arrow.Width + 3, 0));
            var work = Screen.FromControl(this).WorkingArea;
            var size = menu.GetPreferredSize(Size.Empty);
            if (origin.X + size.Width > work.Right) origin.X = Left - size.Width - 3;
            origin.X = Math.Max(work.Left, Math.Min(origin.X, work.Right - size.Width));
            origin.Y = Math.Max(work.Top, Math.Min(origin.Y, work.Bottom - size.Height));
            menu.Show(origin); menu.Items[0].Select();
        }
        internal void Act(Action action) {
            acting = true; dismiss.Stop();
            try { DismissMenu(); action(); }
            catch (Exception error) { WindowState = FormWindowState.Normal; MessageBox.Show(this, error.Message, "Codex Apps", MessageBoxButtons.OK, MessageBoxIcon.Information); Activate(); }
            finally { acting = false; }
        }
    }

}
