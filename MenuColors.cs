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
    internal sealed class MenuColors : ProfessionalColorTable {
        readonly ThemePalette palette;
        internal MenuColors(ThemePalette palette) { this.palette = palette; UseSystemColors = false; }
        public override Color ToolStripDropDownBackground { get { return palette.Background; } }
        public override Color ImageMarginGradientBegin { get { return ToolStripDropDownBackground; } }
        public override Color ImageMarginGradientMiddle { get { return ToolStripDropDownBackground; } }
        public override Color ImageMarginGradientEnd { get { return ToolStripDropDownBackground; } }
        public override Color MenuItemSelected { get { return palette.Hover; } }
        public override Color MenuItemBorder { get { return palette.Accent; } }
        public override Color MenuBorder { get { return palette.Border; } }
    }

}
