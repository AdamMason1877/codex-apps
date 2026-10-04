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
    internal static class Applications {
        internal static ProcessStartInfo ShellStartInfo(AppEntry entry) {
            return new ProcessStartInfo(entry.path) {
                UseShellExecute = true, Arguments = entry.arguments ?? "",
                WorkingDirectory = entry.workingDirectory ?? (Directory.Exists(entry.path) ? entry.path : Path.GetDirectoryName(entry.path))
            };
        }
        internal static void RequireTarget(AppEntry entry) {
            if (!File.Exists(entry.path) && !Directory.Exists(entry.path)) throw new FileNotFoundException("The file or folder could not be found. Use Organize to update its path in apps.local.json (or apps.json).\n\n" + entry.path);
        }
        internal static Process Running(AppEntry entry) {
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(entry.path))) {
                try {
                    if (String.Equals(process.MainModule.FileName, entry.path, StringComparison.OrdinalIgnoreCase)) return process;
                } catch (System.ComponentModel.Win32Exception) { }
                  catch (InvalidOperationException) { }
                process.Dispose();
            }
            return null;
        }
        internal static void Open(AppEntry entry) {
            RequireTarget(entry);
            // Windows resolves documents, HTML files, shortcuts, and folders through
            // their registered shell association. Process reuse only applies to EXEs.
            if (!Path.GetExtension(entry.path).Equals(".exe",StringComparison.OrdinalIgnoreCase) || Directory.Exists(entry.path)) {
                using (var opened = Process.Start(ShellStartInfo(entry))) { }
                return;
            }
            bool reuse = entry.reuseExisting ?? String.IsNullOrWhiteSpace(entry.arguments);
            using (var process = reuse && Path.GetExtension(entry.path).Equals(".exe",StringComparison.OrdinalIgnoreCase) ? Running(entry) : null) {
                if (process != null) {
                    var window = Native.FindApplicationWindow(process.Id, entry.windowTitle ?? entry.name);
                    if (window == IntPtr.Zero) window = process.MainWindowHandle;
                    if (window == IntPtr.Zero) throw new InvalidOperationException(entry.name + " is running but has no available controls yet. Try again in a moment, or use its system tray icon.");
                    Native.Reveal(window); return;
                }
            }
            using (var process = Process.Start(new ProcessStartInfo(entry.path) {
                UseShellExecute = true, Arguments = entry.arguments ?? "", WorkingDirectory = entry.workingDirectory ?? Path.GetDirectoryName(entry.path)
            })) {
                // Tray applications may create their controls hidden. Bring those forward too.
                if (process == null) return;
                try { process.WaitForInputIdle(2500); } catch (InvalidOperationException) { }
                var window = Native.FindApplicationWindow(process.Id, entry.windowTitle ?? entry.name);
                if (window != IntPtr.Zero) Native.Reveal(window);
            }
        }
    }

}
