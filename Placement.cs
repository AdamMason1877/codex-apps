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
    internal static class Placement {
        internal static Rectangle AboveTaskbar(Rectangle work, Point pointer, Size desired, int gap) {
            int width = Math.Min(desired.Width, work.Width);
            int height = Math.Min(desired.Height, Math.Max(1, work.Height - gap));
            int left = Math.Max(work.Left, Math.Min(pointer.X - width / 2, work.Right - width));
            return new Rectangle(left, work.Bottom - height - gap, width, height);
        }
    }

}
