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
    internal static class Program {
        [STAThread] static int Main(string[] args) {
            try {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                if (args.Length > 0 && args[0] == "--self-test") return Tests.Run();
                var apps = Configuration.Load(Configuration.ActiveFile);
                bool created;
                using (var mutex = new Mutex(true, "Local\\CodexAppsLauncher_20261004", out created)) {
                    if (!created) { var existing = Native.FindWindow(null, "Codex Apps"); if (existing != IntPtr.Zero) Native.Reveal(existing); return 0; }
                    Application.Run(new Launcher(apps, args.Contains("--preview"), true));
                }
                return 0;
            } catch (Exception error) {
                if (args.Contains("--self-test")) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results.txt"), "FAIL: " + error); return 1; }
                MessageBox.Show(error.Message, "Codex Apps could not start", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1;
            }
        }
    }

}
