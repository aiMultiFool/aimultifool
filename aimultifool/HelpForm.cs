using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace aimultifool
{
    public partial class HelpForm : Form
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private RichTextBox richTextBox; // Use RichTextBox control to display the RTF file

        public HelpForm()
        {
            InitializeComponent();
        }

        public static void EnableDarkMode(IntPtr handle)
        {
            int attributeValue = 1; // 1 = Enable Dark Mode, 0 = Disable Dark Mode
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref attributeValue, sizeof(int));
        }

        private void HelpForm_Load(object sender, EventArgs e)
        {
            EnableDarkMode(this.Handle); // Apply dark mode
            this.Text = "aiMultiFool Quickstart Guide & Help";

            // Create and configure the RichTextBox control
            richTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true, // Make it read-only to prevent editing
                DetectUrls = true, // Detect and link URLs automatically
                BackColor = System.Drawing.Color.White, // Set the background color to black for dark mode
                //ForeColor = System.Drawing.Color.White, // Set text color to white for dark mode
            };



            // Load the RTF file
            try
            {
                string rtfFilePath = Path.Combine(Directory.GetCurrentDirectory(), "aiMultiFool-Help.rtf");

                if (File.Exists(rtfFilePath))
                {
                    richTextBox.LoadFile(rtfFilePath); // Load the RTF file into the RichTextBox
                }
                else
                {
                    MessageBox.Show("Help file not found: aiMultiFool-Help.rtf", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load help file. Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Add the RichTextBox to the form
            this.Controls.Add(richTextBox);

            // Adjust RichTextBox size dynamically on form resize
            this.Resize += (s, args) =>
            {
                richTextBox.Size = new System.Drawing.Size(ClientSize.Width, ClientSize.Height);
            };
        }
    }
}
