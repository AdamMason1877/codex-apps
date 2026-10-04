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
    public sealed class AppEntry {
        public string name { get; set; }
        public string description { get; set; }
        public string path { get; set; }
        public string arguments { get; set; }
        public string workingDirectory { get; set; }
        public string windowTitle { get; set; }
        public bool? reuseExisting { get; set; }
        public AppEntry[] items { get; set; }
        [ScriptIgnore] public bool IsCategory { get { return items != null; } }
    }

    internal static class Configuration {
        internal static string Folder { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        internal static string ActiveFile { get { var local = Path.Combine(Folder,"apps.local.json"); return File.Exists(local) ? local : Path.Combine(Folder,"apps.json"); } }
        internal static string Stamp { get { string file=ActiveFile; return file + "|" + File.GetLastWriteTimeUtc(file).Ticks + "|" + (File.Exists(file) ? new FileInfo(file).Length : 0); } }
        internal static AppEntry[] Load(string file) { return Parse(File.ReadAllText(file), Path.GetDirectoryName(Path.GetFullPath(file))); }
        internal static AppEntry[] Parse(string json, string folder) {
            AppEntry[] entries;
            try { entries = new JavaScriptSerializer { MaxJsonLength = 1048576, RecursionLimit = 40 }.Deserialize<AppEntry[]>(json); }
            catch (Exception error) { throw new InvalidDataException("The app configuration is not valid JSON: " + error.Message); }
            if (entries == null) throw new InvalidDataException("The configuration must be an array of apps and categories.");
            int count = 0; Validate(entries, folder, 0, ref count); return entries;
        }
        static void Validate(AppEntry[] entries, string folder, int depth, ref int count) {
            if (depth > 8) throw new InvalidDataException("Categories may be nested at most eight levels deep.");
            foreach (var entry in entries) {
                if (++count > 500) throw new InvalidDataException("The configuration supports up to 500 apps and categories.");
                if (entry == null || String.IsNullOrWhiteSpace(entry.name) || entry.name.Length > 80) throw new InvalidDataException("Every app or category needs a name of 1-80 characters.");
                if (entry.description != null && entry.description.Length > 160) throw new InvalidDataException(entry.name + ": descriptions must be at most 160 characters.");
                if (entry.IsCategory) {
                    if (entry.path != null || entry.arguments != null || entry.workingDirectory != null || entry.windowTitle != null || entry.reuseExisting.HasValue) throw new InvalidDataException(entry.name + ": a category must have items instead of launch settings.");
                    Validate(entry.items, folder, depth + 1, ref count);
                } else {
                    if (String.IsNullOrWhiteSpace(entry.path)) throw new InvalidDataException(entry.name + ": provide a path for an app or items for a category.");
                    entry.path = Resolve(entry.path, folder);
                    if (!String.IsNullOrWhiteSpace(entry.workingDirectory)) entry.workingDirectory = Resolve(entry.workingDirectory, folder);
                }
            }
        }
        internal static string Resolve(string value, string folder) {
            string expanded=Environment.ExpandEnvironmentVariables(value);
            return Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(folder,expanded));
        }
        internal static void Edit() {
            string local=Path.Combine(Folder,"apps.local.json");
            if (!File.Exists(local)) File.Copy(Path.Combine(Folder,"apps.json"),local);
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"notepad.exe"),"\""+local+"\"") { UseShellExecute=true });
        }
    }

}
