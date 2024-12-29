using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace aimultifool
{
    public partial class CustomMessageBox : Form
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public DialogResult Result { get; private set; }

        private Button buttonOK; // For OK-only dialog

        public CustomMessageBox(string message, string title, bool isOkOnly)
        {
            InitializeComponent();
            this.Text = title; // Set the window title
            this.labelMessage.Text = message; // Set the message text
            this.StartPosition = FormStartPosition.CenterParent; // Center on parent form
            EnableDarkMode(this.Handle); // Apply dark mode

            if (isOkOnly)
            {
                SetupOKButton(); // Create and configure OK button
            }
            else
            {
                SetupYesNoButtons(); // Create and configure Yes/No buttons
            }
        }

        public static void EnableDarkMode(IntPtr handle)
        {
            int attributeValue = 1; // 1 = Enable Dark Mode, 0 = Disable Dark Mode
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref attributeValue, sizeof(int));
        }

        private void SetupYesNoButtons()
        {
            Button buttonYes = new Button
            {
                Text = "Yes",
                DialogResult = DialogResult.Yes,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            Button buttonNo = new Button
            {
                Text = "No",
                DialogResult = DialogResult.No,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            // Assign the "Yes" button as the default button for Enter
            this.AcceptButton = buttonYes;

            // Assign the "No" button for Esc
            this.CancelButton = buttonNo;

            buttonYes.Click += (s, e) => this.Close();
            buttonNo.Click += (s, e) => this.Close();

            this.Controls.Add(buttonYes);
            this.Controls.Add(buttonNo);

            buttonYes.Location = new System.Drawing.Point(this.Width - 180, this.Height - 80);
            buttonNo.Location = new System.Drawing.Point(this.Width - 100, this.Height - 80);

            // Optionally, set focus to the "Yes" button explicitly
            this.Shown += (s, e) => buttonYes.Focus();
        }



        private void SetupOKButton()
        {
            buttonOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            buttonOK.Click += (s, e) => this.Close();

            this.Controls.Add(buttonOK);

            buttonOK.Location = new System.Drawing.Point(this.Width - 100, this.Height - 80);
        }

        public static DialogResult Show(string message, string title, Form parent, bool isOkOnly = false)
        {
            using (var dialog = new CustomMessageBox(message, title, isOkOnly))
            {
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Load += (s, e) =>
                {
                    // Ensure the dialog is always on top, even if another form is topmost
                    SetWindowPos(dialog.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW);
                };
                return dialog.ShowDialog(parent);
            }
        }

        private void CustomMessageBox_Load(object sender, EventArgs e)
        {
            // Any additional initialization logic here
        }
    }
}
