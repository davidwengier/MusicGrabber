namespace MusicGrabber;

public static class Theme
{
    // Background colors
    public static readonly Color FormBackground = Color.FromArgb(24, 24, 32);
    public static readonly Color PanelBackground = Color.FromArgb(32, 32, 42);
    public static readonly Color ControlBackground = Color.FromArgb(42, 42, 56);
    public static readonly Color ControlBackgroundAlt = Color.FromArgb(36, 36, 48);

    // Text colors
    public static readonly Color TextPrimary = Color.FromArgb(230, 230, 240);
    public static readonly Color TextSecondary = Color.FromArgb(160, 160, 180);
    public static readonly Color TextDisabled = Color.FromArgb(90, 90, 110);

    // Accent colors
    public static readonly Color Accent = Color.FromArgb(30, 215, 96); // Spotify green
    public static readonly Color AccentHover = Color.FromArgb(40, 235, 116);
    public static readonly Color AccentDark = Color.FromArgb(20, 170, 76);

    // Borders / separators
    public static readonly Color Border = Color.FromArgb(55, 55, 70);

    // Status colors
    public static readonly Color Success = Color.FromArgb(30, 215, 96);
    public static readonly Color Error = Color.FromArgb(230, 70, 70);
    public static readonly Color Warning = Color.FromArgb(255, 180, 50);

    public static readonly Font DefaultFont = new("Segoe UI", 9.5f);
    public static readonly Font SmallFont = new("Segoe UI", 8.5f);
    public static readonly Font HeadingFont = new("Segoe UI Semibold", 11f);
    public static readonly Font MonoFont = new("Cascadia Code", 9f, FontStyle.Regular);

    public static void Apply(Control control)
    {
        control.BackColor = FormBackground;
        control.ForeColor = TextPrimary;
        control.Font = DefaultFont;

        ApplyRecursive(control);
    }

    private static void ApplyRecursive(Control control)
    {
        foreach (Control child in control.Controls)
        {
            child.ForeColor = TextPrimary;
            child.Font = DefaultFont;

            switch (child)
            {
                case Button btn:
                    StyleButton(btn);
                    break;
                case TextBox txt:
                    txt.BackColor = ControlBackground;
                    txt.ForeColor = TextPrimary;
                    txt.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox cmb:
                    cmb.BackColor = ControlBackground;
                    cmb.ForeColor = TextPrimary;
                    cmb.FlatStyle = FlatStyle.Flat;
                    break;
                case CheckedListBox clb:
                    clb.BackColor = ControlBackground;
                    clb.ForeColor = TextPrimary;
                    clb.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ListBox lb:
                    lb.BackColor = ControlBackground;
                    lb.ForeColor = TextPrimary;
                    lb.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case RichTextBox rtb:
                    rtb.BackColor = Color.FromArgb(18, 18, 24);
                    rtb.ForeColor = TextSecondary;
                    rtb.BorderStyle = BorderStyle.None;
                    break;
                case ProgressBar pb:
                    pb.BackColor = ControlBackground;
                    pb.ForeColor = Accent;
                    break;
                case RadioButton rb:
                    rb.FlatStyle = FlatStyle.Flat;
                    rb.ForeColor = TextPrimary;
                    break;
                case Label lbl:
                    lbl.ForeColor = TextSecondary;
                    break;
                case MenuStrip ms:
                    StyleMenuStrip(ms);
                    break;
                case StatusStrip ss:
                    ss.BackColor = PanelBackground;
                    ss.ForeColor = TextSecondary;
                    foreach (ToolStripItem item in ss.Items)
                    {
                        item.ForeColor = TextSecondary;
                        item.BackColor = PanelBackground;
                    }
                    break;
                case SplitContainer sc:
                    sc.BackColor = Border;
                    sc.Panel1.BackColor = FormBackground;
                    sc.Panel2.BackColor = FormBackground;
                    break;
                case FlowLayoutPanel flp:
                    flp.BackColor = FormBackground;
                    break;
                case TableLayoutPanel tlp:
                    tlp.BackColor = FormBackground;
                    break;
                case Panel p:
                    p.BackColor = FormBackground;
                    break;
            }

            ApplyRecursive(child);
        }
    }

    public static void StyleButton(Button btn, bool isPrimary = false)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 1;
        btn.Cursor = Cursors.Hand;
        btn.Font = DefaultFont;

        if (isPrimary)
        {
            btn.BackColor = Accent;
            btn.ForeColor = Color.Black;
            btn.FlatAppearance.BorderColor = Accent;
            btn.FlatAppearance.MouseOverBackColor = AccentHover;
            btn.FlatAppearance.MouseDownBackColor = AccentDark;
        }
        else
        {
            btn.BackColor = ControlBackground;
            btn.ForeColor = TextPrimary;
            btn.FlatAppearance.BorderColor = Border;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 72);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(48, 48, 64);
        }
    }

    private static void StyleMenuStrip(MenuStrip ms)
    {
        ms.BackColor = PanelBackground;
        ms.ForeColor = TextPrimary;
        ms.Renderer = new DarkMenuRenderer();
    }

    private class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = TextPrimary;
            base.OnRenderItemText(e);
        }
    }

    private class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(55, 55, 72);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(55, 55, 72);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(55, 55, 72);
        public override Color MenuItemBorder => Border;
        public override Color MenuBorder => Border;
        public override Color MenuStripGradientBegin => PanelBackground;
        public override Color MenuStripGradientEnd => PanelBackground;
        public override Color ToolStripDropDownBackground => PanelBackground;
        public override Color ImageMarginGradientBegin => PanelBackground;
        public override Color ImageMarginGradientMiddle => PanelBackground;
        public override Color ImageMarginGradientEnd => PanelBackground;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(48, 48, 64);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(48, 48, 64);
    }
}
