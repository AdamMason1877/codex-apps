using System;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace CodexApps {
    internal static class OrganizerTests {
        internal static void Run() {
            var app=new AppEntry { name="Player", path="%WINDIR%\\notepad.exe", arguments="--example", workingDirectory=".", windowTitle="Player window", reuseExisting=false };
            var category=new AppEntry { name="Audio", items=new[] { app } };
            var root=new TreeNode("All apps");
            var audio=OrganizerData.Node(category); root.Nodes.Add(audio);
            var empty=OrganizerData.Node(new AppEntry { name="Empty",items=new AppEntry[0] }); root.Nodes.Add(empty);
            var player=audio.Nodes[0];
            if(OrganizerData.Move(audio,player) || OrganizerData.Move(audio,audio) || OrganizerData.Move(root,empty)) throw new Exception("Organizer allowed a cyclic move or moved root.");
            if(!OrganizerData.Move(player,empty) || player.Parent!=empty || audio.Nodes.Count!=0) throw new Exception("App did not move into category.");
            if(category.items.Length!=1 || category.items[0]!=app) throw new Exception("Draft changed original config before saving.");
            if(!OrganizerData.Move(empty,audio) || empty.Parent!=audio) throw new Exception("Category nesting failed.");
            if(OrganizerData.Move(audio,empty)) throw new Exception("Category moved into descendant.");
            OrganizerData.Move(player,root);
            var second=OrganizerData.Node(new AppEntry { name="Second",path="relative.exe" }); root.Nodes.Add(second);
            OrganizerData.Move(second,player);
            if(second.Index+1!=player.Index) throw new Exception("Drop before app did not preserve order.");
            string path=Path.Combine(Path.GetTempPath(),"codex-organizer-"+Guid.NewGuid().ToString("N")+".json");
            try {
                OrganizerData.Save(path,OrganizerData.Export(root.Nodes));
                var entries=OrganizerData.Read(path);
                var saved=entries[2];
                if(saved.path!=app.path || saved.arguments!=app.arguments || saved.workingDirectory!="." || saved.windowTitle!=app.windowTitle || saved.reuseExisting!=false) throw new Exception("Organizer lost portable launch settings.");
                if(entries[0].items[0].items.Length!=0) throw new Exception("Empty nested category lost.");
                string before=File.ReadAllText(path); bool rejected=false;
                try { OrganizerData.Save(path,new[] { new AppEntry { name="Broken" } }); } catch(InvalidDataException) { rejected=true; }
                if(!rejected || File.ReadAllText(path)!=before) throw new Exception("Invalid save damaged existing config.");
                OrganizerData.Save(path,new AppEntry[0]);
                if(OrganizerData.Read(path).Length!=0) throw new Exception("Empty library did not save.");
            } finally { if(File.Exists(path)) File.Delete(path); }
            if(OrganizerData.FromFile(Application.ExecutablePath).path!=Application.ExecutablePath) throw new Exception("Executable import failed.");
            string html=Path.Combine(Path.GetTempPath(),"codex-workflow-"+Guid.NewGuid().ToString("N")+".html");
            string plain=Path.ChangeExtension(html,".txt");
            try {
                File.WriteAllText(html,"<!doctype html><title>Workflow</title>");
                File.WriteAllText(plain,"Workflow notes");
                var imported=OrganizerData.FromFile(html);
                if(imported.path!=html || imported.name!=Path.GetFileNameWithoutExtension(html)) throw new Exception("HTML import failed.");
                if(OrganizerData.FromFile(plain).path!=plain) throw new Exception("Document import failed.");
                if(OrganizerData.FromFile(Path.GetTempPath()).path!=Path.GetFullPath(Path.GetTempPath())) throw new Exception("Folder import failed.");
                var fromJson=Configuration.Parse(new JavaScriptSerializer().Serialize(new[] { imported }),Path.GetTempPath());
                var start=Applications.ShellStartInfo(fromJson[0]);
                if(!start.UseShellExecute || start.FileName!=html || start.WorkingDirectory!=Path.GetDirectoryName(html)) throw new Exception("HTML file did not use Windows file association.");
                bool missingFile=false;
                try { Applications.RequireTarget(new AppEntry { path=html+".missing" }); } catch(FileNotFoundException) { missingFile=true; }
                if(!missingFile) throw new Exception("Missing resource was accepted.");
            } finally { File.Delete(html); File.Delete(plain); }
            using(var launcher=new Launcher(new[] { new AppEntry { name="Empty",items=new AppEntry[0] } },true)) {
                if(launcher.Menus[0].Items.Count!=1 || launcher.Menus[0].Items[0].Enabled) throw new Exception("Empty category should display a disabled hint.");
            }
            using(var organizer=new Organizer(new[] { category },"fixture")) {
                if(organizer.Controls.Count==0) throw new Exception("Organizer form is empty.");
            }
        }
    }
}
