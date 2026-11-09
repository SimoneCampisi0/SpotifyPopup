using System.Drawing;
using System.Windows.Forms;

namespace SpotifyPopup;

internal sealed class TrayColors : ProfessionalColorTable
{
    private static Color Panel => Color.FromArgb(32, 36, 42);
    private static Color Hover => Color.FromArgb(54, 63, 73);
    private static Color Line => Color.FromArgb(69, 77, 87);
    public override Color ToolStripDropDownBackground => Panel;
    public override Color ImageMarginGradientBegin => Panel;
    public override Color ImageMarginGradientMiddle => Panel;
    public override Color ImageMarginGradientEnd => Panel;
    public override Color MenuItemSelected => Hover;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
    public override Color MenuItemBorder => Hover;
    public override Color MenuBorder => Line;
    public override Color SeparatorDark => Line;
    public override Color SeparatorLight => Line;
}
