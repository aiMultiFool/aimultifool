using System.Runtime.InteropServices;

namespace aimultifool
{
    public partial class ModifierForm : Form
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private MainForm _mainForm; // Reference to MainForm

        public ModifierForm(MainForm mainForm)
        {
            InitializeComponent();
            _mainForm = mainForm; // Store the MainForm instance
                                  // Enable dark mode for the window
            EnableDarkMode(this.Handle);


        }

        public static void EnableDarkMode(IntPtr handle)
        {
            int attributeValue = 1; // 1 = Enable Dark Mode, 0 = Disable Dark Mode
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref attributeValue, sizeof(int));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // Cancel the close operation
                this.Hide();     // Hide the form instead
            }
            else
            {
                base.OnFormClosing(e);
            }
        }

        private void ModifierForm_Load(object sender, EventArgs e)
        {


            // Adjust starting position for sliders to account for the label
            int spacing = 45; // Vertical spacing between controls
            int yStart = 20;  // Adjust Y start to place sliders below the label
            int labelWidth = 80; // Label width
            int trackBarWidth = 150; // TrackBar width
            int formPadding = 10; // Padding around controls

            // Ensure menu items are loaded
            var modifiersMenu = _mainForm.GetModifiersMenu();

            if (modifiersMenu == null || modifiersMenu.DropDownItems.Count == 0)
            {
                MessageBox.Show("No modifiers found in the Modifiers menu.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Extract the names of all sub-items (values) under "Modifiers"
            var labels = modifiersMenu.DropDownItems
                .OfType<ToolStripMenuItem>()
                .Select(item => item.Text)
                .ToArray();

            int formWidth = labelWidth + trackBarWidth + (formPadding * 3); // Form width
            int formHeight = yStart + (labels.Length * spacing) + (formPadding * 2) - 20; // Form height

            for (int i = 0; i < labels.Length; i++)
            {
                string modifier = labels[i];

                // Create label
                Label lbl = new Label
                {
                    Text = modifier,
                    Location = new Point(formPadding, yStart + i * spacing),
                    AutoSize = true
                };

                // Create trackbar
                TrackBar trackBar = new TrackBar
                {
                    Minimum = 1,
                    Maximum = 10,
                    Value = 1,
                    TickFrequency = 1,
                    TabStop = false,
                    SmallChange = 1,
                    LargeChange = 1,
                    Location = new Point(formPadding + labelWidth, yStart - 5 + i * spacing),
                    Width = trackBarWidth
                };
                trackBar.GotFocus += (s, e) => { ((TrackBar)s).Parent.Focus(); };


                int initialValue = trackBar.Value; // Store the initial value for comparison

                // MouseUp event handler
                trackBar.MouseUp += async (senderObj, eventArgs) =>
                {
                    if (trackBar.Value != initialValue) // Only act if value has changed
                    {
                        initialValue = trackBar.Value; // Update the initial value
                        await UpdateModifier(trackBar.Value, modifier);
                    }
                };

                // Add controls to the form
                this.Controls.Add(lbl);
                this.Controls.Add(trackBar);
            }

            // Set the form size and style
            this.ClientSize = new Size(formWidth, formHeight); // Client area size
            this.FormBorderStyle = FormBorderStyle.FixedSingle; // Fixed size
            this.MaximizeBox = false; // Disable maximize button
        }

        public void ReloadSliders()
        {
            // Clear all existing controls
            this.Controls.Clear();

            // Adjust starting position for sliders to account for the label
            int spacing = 45; // Vertical spacing between controls
            int yStart = 20;  // Adjust Y start to place sliders below the label
            int labelWidth = 80; // Label width
            int trackBarWidth = 150; // TrackBar width
            int formPadding = 10; // Padding around controls

            // Ensure menu items are loaded
            var modifiersMenu = _mainForm.GetModifiersMenu();

            if (modifiersMenu == null || modifiersMenu.DropDownItems.Count == 0)
            {
                MessageBox.Show("No modifiers found in the Modifiers menu.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Extract the names of all sub-items (values) under "Modifiers"
            var labels = modifiersMenu.DropDownItems
                .OfType<ToolStripMenuItem>()
                .Select(item => item.Text)
                .ToArray();

            int formWidth = labelWidth + trackBarWidth + (formPadding * 3); // Form width
            int formHeight = yStart + (labels.Length * spacing) + (formPadding * 2)-20; // Form height

            for (int i = 0; i < labels.Length; i++)
            {
                string modifier = labels[i];

                // Create label
                Label lbl = new Label
                {
                    Text = modifier,
                    Location = new Point(formPadding, yStart + i * spacing),
                    AutoSize = true
                };

                // Create trackbar
                TrackBar trackBar = new TrackBar
                {
                    Minimum = 1,
                    Maximum = 10,
                    Value = 1,
                    TickFrequency = 1,
                    TabStop = false,
                    SmallChange = 1,
                    LargeChange = 1,
                    Location = new Point(formPadding + labelWidth, yStart - 5 + i * spacing),
                    Width = trackBarWidth
                };
                trackBar.GotFocus += (s, e) => { ((TrackBar)s).Parent.Focus(); };

                int initialValue = trackBar.Value; // Store the initial value for comparison

                // MouseUp event handler
                trackBar.MouseUp += async (senderObj, eventArgs) =>
                {
                    if (trackBar.Value != initialValue) // Only act if value has changed
                    {
                        initialValue = trackBar.Value; // Update the initial value
                        await UpdateModifier(trackBar.Value, modifier);
                    }
                };

                // Add controls to the form
                this.Controls.Add(lbl);
                this.Controls.Add(trackBar);
            }

            // Set the form size and style
            this.ClientSize = new Size(formWidth, formHeight); // Client area size
        }

        private async Task UpdateModifier(int level, string modifier)
        {
            _mainForm.ReloadInferenceParams();
            _mainForm.AppendToConsole($"[Updated {modifier} level to {level}]");
            await _mainForm.HandleInput($"[System: Adjust your {modifier} level to {level} on a scale of 1 to 10, where 1 is minimal and 10 is extreme. This setting applies exclusively to you and is layered on top of all existing parameters.]", true);
        }
    }
}
