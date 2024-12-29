using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace aimultifool
{
    public partial class CardImage : Form
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public PictureBox PictureBox { get; private set; }

        public CardImage()
        {
            InitializeComponent();
            PictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.StretchImage // Ensure image fills the PictureBox
            };
            this.Controls.Add(PictureBox);

            // Subscribe to the Resize event of the form
            this.Resize += CardImage_Resize;
        }

        public static void EnableDarkMode(IntPtr handle)
        {
            int attributeValue = 1; // 1 = Enable Dark Mode, 0 = Disable Dark Mode
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref attributeValue, sizeof(int));
        }

        private void CardImage_Load(object sender, EventArgs e)
        {
            EnableDarkMode(this.Handle); // Apply dark mode
        }

        private void CardImage_Resize(object sender, EventArgs e)
        {
            // Update PictureBox to fill the resized form (handled by Dock = DockStyle.Fill)
            PictureBox.Invalidate(); // Ensures the PictureBox refreshes properly when resized
        }

        // Override OnFormClosing to dispose of the image
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // Dispose of the image if it exists
            if (PictureBox.Image != null)
            {
                PictureBox.Image.Dispose();
                PictureBox.Image = null;
            }
        }

        // Ensure PictureBox is properly disposed when the form is disposed
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (PictureBox != null)
                {
                    if (PictureBox.Image != null)
                    {
                        PictureBox.Image.Dispose();
                        PictureBox.Image = null;
                    }
                    PictureBox.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
