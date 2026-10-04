using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodexApps {
    // Work on a detached draft. Neither dragging nor Cancel changes the live configuration.
    internal static class OrganizerData {
        internal static AppEntry[] Read(string path) {
            string json = File.ReadAllText(path);
            Configuration.Parse(json, Path.GetDirectoryName(path));
            return new JavaScriptSerializer().Deserialize<AppEntry[]>(json);
        }
        internal static TreeNode Node(AppEntry entry) {
            var node = new TreeNode(entry.name) { Tag = entry };
            if (entry.IsCategory) foreach (var child in entry.items) node.Nodes.Add(Node(child));
            return node;
        }
        internal static AppEntry[] Export(TreeNodeCollection nodes) {
            return nodes.Cast<TreeNode>().Select(node => {
                var e = (AppEntry)node.Tag;
                return new AppEntry { name=node.Text, description=e.description, path=e.path,
                    arguments=e.arguments, workingDirectory=e.workingDirectory,
                    windowTitle=e.windowTitle, reuseExisting=e.reuseExisting,
                    items=e.IsCategory ? Export(node.Nodes) : null };
            }).ToArray();
        }
        internal static bool CanMove(TreeNode source, TreeNode target) {
            if (source == null || source.Parent == null || target == null) return false;
            for (var n=target; n!=null; n=n.Parent) if (n==source) return false;
            return true;
        }
        internal static bool Move(TreeNode source, TreeNode target) {
            if (!CanMove(source,target)) return false;
            bool into = target.Tag == null || ((AppEntry)target.Tag).IsCategory;
            var parent = into ? target : target.Parent;
            source.Remove();
            if (into) parent.Nodes.Add(source); else parent.Nodes.Insert(target.Index,source);
            parent.Expand(); return true;
        }
        internal static AppEntry FromFile(string path) {
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException("Choose an existing file or folder.",path);
            string absolute=Path.GetFullPath(path);
            bool folder=Directory.Exists(absolute);
            string name=folder ? Path.GetFileName(absolute.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)) : Path.GetFileNameWithoutExtension(absolute);
            if(String.IsNullOrWhiteSpace(name)) name=absolute;
            return new AppEntry { name=name, path=absolute };
        }
        // Omit absent properties so the resulting JSON also conforms to the public schema.
        static object[] PublicEntries(AppEntry[] entries) {
            return entries.Select(e => {
                var d=new Dictionary<string,object> { {"name",e.name} };
                if (!String.IsNullOrEmpty(e.description)) d["description"]=e.description;
                if (e.IsCategory) d["items"]=PublicEntries(e.items);
                else {
                    d["path"]=e.path;
                    if (!String.IsNullOrEmpty(e.arguments)) d["arguments"]=e.arguments;
                    if (!String.IsNullOrEmpty(e.workingDirectory)) d["workingDirectory"]=e.workingDirectory;
                    if (!String.IsNullOrEmpty(e.windowTitle)) d["windowTitle"]=e.windowTitle;
                    if (e.reuseExisting.HasValue) d["reuseExisting"]=e.reuseExisting.Value;
                }
                return (object)d;
            }).ToArray();
        }
        internal static void Save(string path, AppEntry[] entries) {
            string json=new JavaScriptSerializer().Serialize(PublicEntries(entries));
            Configuration.Parse(json,Path.GetDirectoryName(path));
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                File.WriteAllText(temp,json+Environment.NewLine,new System.Text.UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    internal sealed class Organizer : Form {
        readonly TreeView tree=new TreeView();
        readonly TreeNode root=new TreeNode("All apps");
        readonly Label status=new Label();
        readonly string openedStamp;
        bool dirty;
        internal Organizer() : this(OrganizerData.Read(Configuration.ActiveFile),Configuration.Stamp) { }
        internal Organizer(AppEntry[] entries, string stamp) {
            openedStamp=stamp;
            Text="Organize apps - Codex Apps";
            Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(760,560); MinimumSize=new Size(650,480);
            StartPosition=FormStartPosition.CenterScreen;
            var heading=new Label { Text="Your apps, your categories", Dock=DockStyle.Top, Height=44, Padding=new Padding(16,12,0,0), Font=new Font(Font,FontStyle.Bold) };
            var help=new Label { Text="Drag an item onto a category. Drop onto an item to place it before that item.\nDrop any file or folder here to add it. Nothing changes until you save.", Dock=DockStyle.Top, Height=58, Padding=new Padding(16,6,16,0) };
            var toolbar=new FlowLayoutPanel { Dock=DockStyle.Top, Height=120, Padding=new Padding(12,4,0,0), WrapContents=true };
            AddButton(toolbar,"New category",NewCategory);
            AddButton(toolbar,"Add files...",AddFiles);
            AddButton(toolbar,"Edit...",EditSelected);
            AddButton(toolbar,"Remove",RemoveSelected);
            AddButton(toolbar,"Move up",()=>Reorder(-1));
            AddButton(toolbar,"Move down",()=>Reorder(1));
            AddButton(toolbar,"Move to...",MoveSelected);
            tree.Dock=DockStyle.Fill; tree.AllowDrop=true; tree.HideSelection=false;
            tree.AccessibleName="Apps and categories"; tree.FullRowSelect=true;
            tree.ItemHeight=30; tree.Indent=24;
            var images=new ImageList { ImageSize=new Size(16,16), ColorDepth=ColorDepth.Depth32Bit };
            var folder=new Bitmap(16,16);
            using(var g=Graphics.FromImage(folder)) { g.FillRectangle(Brushes.Goldenrod,1,3,7,3); g.FillRectangle(Brushes.Goldenrod,1,5,14,10); }
            images.Images.Add(folder); images.Images.Add(SystemIcons.Application.ToBitmap()); tree.ImageList=images;
            root.Tag=null; tree.Nodes.Add(root);
            foreach(var e in entries) root.Nodes.Add(OrganizerData.Node(e));
            SetIcons(root); root.ExpandAll(); tree.SelectedNode=root;
            tree.ItemDrag+=delegate(object sender,ItemDragEventArgs e) { var n=(TreeNode)e.Item; if(n!=root && e.Button==MouseButtons.Left) tree.DoDragDrop(n,DragDropEffects.Move); };
            tree.DragEnter+=OnTreeDragOver; tree.DragOver+=OnTreeDragOver; tree.DragDrop+=Drop;
            tree.NodeMouseDoubleClick+=delegate(object sender,TreeNodeMouseClickEventArgs e) { if(e.Node.Tag!=null && !((AppEntry)e.Node.Tag).IsCategory) EditSelected(); };
            tree.KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.KeyCode==Keys.F2) { EditSelected(); e.Handled=true; } };
            var bottom=new FlowLayoutPanel { Dock=DockStyle.Bottom, Height=48, FlowDirection=FlowDirection.RightToLeft, Padding=new Padding(8) };
            var cancel=AddButton(bottom,"Cancel",Close);
            AddButton(bottom,"Save changes",SaveChanges);
            CancelButton=cancel;
            status.Dock=DockStyle.Bottom; status.Height=32; status.Padding=new Padding(16,6,0,0); status.Text="Select a category, then add apps or create a subcategory.";
            Controls.Add(tree); Controls.Add(status); Controls.Add(bottom); Controls.Add(toolbar); Controls.Add(help); Controls.Add(heading);
            Activated+=delegate { Theme(this); Native.ThemeTitleBar(this); };
            FormClosing+=delegate(object sender,FormClosingEventArgs e) {
                if(dirty && MessageBox.Show(this,"Discard your unsaved organization changes?","Unsaved changes",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) e.Cancel=true;
            };
            FormClosed+=delegate { images.Dispose(); folder.Dispose(); };
            Theme(this);
        }
        internal static Button AddButton(Control parent,string text,Action action) {
            var b=new Button { Text=text, AutoSize=true, Height=30, Padding=new Padding(3,0,3,0) };
            b.Click+=delegate { action(); }; parent.Controls.Add(b); return b;
        }
        internal static void Theme(Control parent) {
            var p=ThemePalette.Current();
            Action<Control> apply=null;
            apply=c=> { c.BackColor=c is Button || c is TextBox || c is TreeView || c is ComboBox ? p.Surface : p.Background; c.ForeColor=p.Text;
                var b=c as Button; if(b!=null) { b.FlatStyle=FlatStyle.Flat; b.FlatAppearance.BorderColor=p.Border; }
                foreach(Control child in c.Controls) apply(child);
            }; apply(parent);
        }
        void SetIcons(TreeNode n) { n.ImageIndex=n.SelectedImageIndex=n.Tag==null || ((AppEntry)n.Tag).IsCategory ? 0 : 1; foreach(TreeNode child in n.Nodes) SetIcons(child); }
        TreeNode Destination { get { var n=tree.SelectedNode ?? root; return n.Tag==null || ((AppEntry)n.Tag).IsCategory ? n : n.Parent; } }
        void Changed(string message) { dirty=true; status.Text=message+" Save changes to apply."; }
        void NewCategory() {
            var parent=Destination;
            var entry=new AppEntry { name="New category", items=new AppEntry[0] };
            using(var editor=new EntryEditor(entry)) if(editor.ShowDialog(this)==DialogResult.OK) {
                var n=OrganizerData.Node(editor.Entry); parent.Nodes.Add(n); SetIcons(n); parent.Expand(); tree.SelectedNode=n; n.EnsureVisible(); Changed("Category created.");
            }
        }
        void AddFiles() {
            using(var dialog=new OpenFileDialog { Title="Add files or shortcuts", Filter="All files (*.*)|*.*", Multiselect=true, DereferenceLinks=false })
                if(dialog.ShowDialog(this)==DialogResult.OK) Import(dialog.FileNames,Destination);
        }
        void Import(string[] files,TreeNode target) {
            try {
                var entries=files.Select(OrganizerData.FromFile).ToArray();
                var parent=target.Tag==null || ((AppEntry)target.Tag).IsCategory ? target : target.Parent;
                foreach(var e in entries) { var n=OrganizerData.Node(e); parent.Nodes.Add(n); SetIcons(n); tree.SelectedNode=n; }
                parent.Expand(); if(tree.SelectedNode!=null) tree.SelectedNode.EnsureVisible(); Changed(entries.Length+" item(s) added.");
            } catch(Exception e) { Error(e); }
        }
        void EditSelected() {
            var n=tree.SelectedNode; if(n==null || n==root) return;
            using(var editor=new EntryEditor((AppEntry)n.Tag)) if(editor.ShowDialog(this)==DialogResult.OK) { n.Tag=editor.Entry; n.Text=editor.Entry.name; Changed("Entry updated."); }
        }
        void RemoveSelected() {
            var n=tree.SelectedNode; if(n==null || n==root) return;
            if(MessageBox.Show(this,"Remove \""+n.Text+"\""+(n.Nodes.Count>0 ? " and everything in this category" : "")+" from the launcher?\nInstalled applications and files will stay on your computer.","Remove entry",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
            var parent=n.Parent; n.Remove(); tree.SelectedNode=parent; Changed("Entry removed.");
        }
        void Reorder(int delta) {
            var n=tree.SelectedNode; if(n==null || n==root) return;
            var parent=n.Parent; int index=n.Index+delta;
            if(index<0 || index>=parent.Nodes.Count) return;
            n.Remove(); parent.Nodes.Insert(index,n); tree.SelectedNode=n; n.EnsureVisible(); Changed("Order updated.");
        }
        void MoveSelected() {
            var source=tree.SelectedNode; if(source==null || source==root) return;
            using(var picker=new Form { Text="Move to category", ClientSize=new Size(400,400), StartPosition=FormStartPosition.CenterParent, Font=Font }) {
                var choices=new TreeView { Dock=DockStyle.Fill, HideSelection=false, AccessibleName="Destination category" };
                Func<TreeNode,TreeNode> copy=null;
                copy=n=> { var c=new TreeNode(n.Text) { Tag=n }; foreach(TreeNode child in n.Nodes) if(child!=source && ((AppEntry)child.Tag).IsCategory) c.Nodes.Add(copy(child)); return c; };
                choices.Nodes.Add(copy(root)); choices.ExpandAll(); choices.SelectedNode=choices.Nodes[0];
                var bar=new FlowLayoutPanel { Dock=DockStyle.Bottom, Height=45 };
                AddButton(bar,"Move here",()=> { if(choices.SelectedNode!=null && OrganizerData.Move(source,(TreeNode)choices.SelectedNode.Tag)) { tree.SelectedNode=source; source.EnsureVisible(); Changed("Entry moved."); picker.Close(); } });
                var cancel=AddButton(bar,"Cancel",picker.Close); picker.CancelButton=cancel;
                picker.Controls.Add(choices); picker.Controls.Add(bar); Theme(picker); picker.ShowDialog(this);
            }
        }
        TreeNode DropTarget(DragEventArgs e) { return tree.GetNodeAt(tree.PointToClient(new Point(e.X,e.Y))) ?? root; }
        void OnTreeDragOver(object sender,DragEventArgs e) {
            var target=DropTarget(e); var source=e.Data.GetData(typeof(TreeNode)) as TreeNode;
            bool file=e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effect=file ? DragDropEffects.Copy : OrganizerData.CanMove(source,target) ? DragDropEffects.Move : DragDropEffects.None;
            if(e.Effect!=DragDropEffects.None) { tree.SelectedNode=target; target.Expand();
                var point=tree.PointToClient(new Point(e.X,e.Y));
                if(point.Y<24 && target.PrevVisibleNode!=null) target.PrevVisibleNode.EnsureVisible();
                if(point.Y>tree.Height-24 && target.NextVisibleNode!=null) target.NextVisibleNode.EnsureVisible();
            }
        }
        void Drop(object sender,DragEventArgs e) {
            var target=DropTarget(e);
            if(e.Data.GetDataPresent(DataFormats.FileDrop)) { Import((string[])e.Data.GetData(DataFormats.FileDrop),target); return; }
            var source=e.Data.GetData(typeof(TreeNode)) as TreeNode;
            if(OrganizerData.Move(source,target)) { tree.SelectedNode=source; source.EnsureVisible(); Changed("Entry moved."); }
        }
        void SaveChanges() {
            try {
                if(Configuration.Stamp!=openedStamp) throw new IOException("The app list changed outside this organizer. Cancel and reopen Organize to load the latest version before saving.");
                OrganizerData.Save(Path.Combine(Configuration.Folder,"apps.local.json"),OrganizerData.Export(root.Nodes));
                dirty=false; DialogResult=DialogResult.OK; Close();
            } catch(Exception e) { Error(e); }
        }
        void Error(Exception error) { MessageBox.Show(this,error.Message,"Cannot update apps",MessageBoxButtons.OK,MessageBoxIcon.Information); }
    }

    internal sealed class EntryEditor : Form {
        internal AppEntry Entry;
        internal EntryEditor(AppEntry entry) {
            Text=entry.IsCategory ? "Category details" : "Resource details"; Font=new Font("Segoe UI",10);
            AutoScaleMode=AutoScaleMode.Dpi; ClientSize=new Size(560,entry.IsCategory ? 195 : 465);
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false; StartPosition=FormStartPosition.CenterParent;
            int y=16;
            Func<string,string,TextBox> field=(label,value)=> { Controls.Add(new Label { Text=label, Bounds=new Rectangle(16,y+4,135,26) }); var box=new TextBox { Text=value ?? "", Bounds=new Rectangle(156,y,385,28), AccessibleName=label }; Controls.Add(box); y+=44; return box; };
            var name=field("Name",entry.name); name.MaxLength=80;
            var description=field("Description",entry.description); description.MaxLength=160;
            TextBox path=null,arguments=null,working=null,title=null; ComboBox reuse=null;
            if(!entry.IsCategory) {
                path=field("File or folder",entry.path); arguments=field("Launch arguments",entry.arguments); working=field("Working folder",entry.workingDirectory); title=field("EXE window title",entry.windowTitle);
                Controls.Add(new Label { Text="Reuse EXE", Bounds=new Rectangle(16,y+4,135,26) });
                reuse=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Bounds=new Rectangle(156,y,385,28), AccessibleName="Running app behavior" };
                reuse.Items.AddRange(new object[] { "Automatic", "Focus existing window", "Always launch" }); reuse.SelectedIndex=!entry.reuseExisting.HasValue ? 0 : entry.reuseExisting.Value ? 1 : 2; Controls.Add(reuse);
            }
            var bar=new FlowLayoutPanel { Dock=DockStyle.Bottom, Height=48, FlowDirection=FlowDirection.RightToLeft, Padding=new Padding(8) };
            var cancel=Organizer.AddButton(bar,"Cancel",()=> { DialogResult=DialogResult.Cancel; Close(); }); CancelButton=cancel;
            var ok=Organizer.AddButton(bar,"Done",()=> {
                var result=new AppEntry { name=name.Text.Trim(), description=description.Text, items=entry.IsCategory ? new AppEntry[0] : null };
                if(!entry.IsCategory) { result.path=path.Text.Trim(); result.arguments=arguments.Text; result.workingDirectory=working.Text; result.windowTitle=title.Text; result.reuseExisting=reuse.SelectedIndex==0 ? (bool?)null : reuse.SelectedIndex==1; }
                try { Configuration.Parse(new JavaScriptSerializer().Serialize(new[] { result }),Configuration.Folder); Entry=result; DialogResult=DialogResult.OK; Close(); }
                catch(Exception e) { MessageBox.Show(this,e.Message,"Check entry",MessageBoxButtons.OK,MessageBoxIcon.Information); }
            }); AcceptButton=ok;
            Controls.Add(bar); Organizer.Theme(this); Shown+=delegate { Native.ThemeTitleBar(this); name.SelectAll(); name.Focus(); };
        }
    }
}
