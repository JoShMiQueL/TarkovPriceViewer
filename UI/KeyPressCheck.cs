using System.Windows.Forms;

namespace TarkovPriceViewer.UI
{
    public partial class KeyPressCheck : Form
    {
        private const int MOUSE_LEFT = 1001;
        private const int MOUSE_RIGHT = 1002;
        private const int MOUSE_MIDDLE = 1003;
        private const int MOUSE_X1 = 1004;
        private const int MOUSE_X2 = 1005;
        private int button;

        public KeyPressCheck(int button)
        {
            this.button = button;
            InitializeComponent();
            this.MouseUp += KeyPressCheck_MouseUp;
        }

        private void KeyPressCheck_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Owner != null)
            {
                ((MainForm)Owner).ChangePressKeyData(null);
            }
        }

        private void KeyPressCheck_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Menu && e.KeyCode != Keys.ControlKey)
            {
                if (e.KeyCode != Keys.Escape)
                {
                    switch (button)
                    {
                        case 1:
                            Program.AppSettings.ShowOverlayKey = (int)e.KeyCode;
                            break;
                        case 2:
                            Program.AppSettings.HideOverlayKey = (int)e.KeyCode;
                            break;
                        case 3:
                            Program.AppSettings.CompareOverlayKey = (int)e.KeyCode;
                            break;
                        case 4:
                            Program.AppSettings.IncreaseTrackerCountKey = (int)e.KeyCode;
                            break;
                        case 5:
                            Program.AppSettings.DecreaseTrackerCountKey = (int)e.KeyCode;
                            break;
                        case 6:
                            Program.AppSettings.ToggleFavoriteItemKey = (int)e.KeyCode;
                            break;
                    }
                    // Persist key changes immediately so they survive future loads
                    Program.SaveSettings();
                    if (Owner != null)
                    {
                        ((MainForm)Owner).ChangePressKeyData(e.KeyCode);
                    }
                }
                this.Close();
            }
        }

        private void KeyPressCheck_MouseUp(object sender, MouseEventArgs e)
        {
            int mouseCode = 0;

            switch (e.Button)
            {
                case MouseButtons.Left:
                    mouseCode = MOUSE_LEFT;
                    break;
                case MouseButtons.Right:
                    mouseCode = MOUSE_RIGHT;
                    break;
                case MouseButtons.Middle:
                    mouseCode = MOUSE_MIDDLE;
                    break;
                case MouseButtons.XButton1:
                    mouseCode = MOUSE_X1;
                    break;
                case MouseButtons.XButton2:
                    mouseCode = MOUSE_X2;
                    break;
            }

            if (mouseCode != 0)
            {
                switch (button)
                {
                    case 1:
                        Program.AppSettings.ShowOverlayKey = mouseCode;
                        break;
                    case 2:
                        Program.AppSettings.HideOverlayKey = mouseCode;
                        break;
                    case 3:
                        Program.AppSettings.CompareOverlayKey = mouseCode;
                        break;
                    case 4:
                        Program.AppSettings.IncreaseTrackerCountKey = mouseCode;
                        break;
                    case 5:
                        Program.AppSettings.DecreaseTrackerCountKey = mouseCode;
                        break;
                    case 6:
                        Program.AppSettings.ToggleFavoriteItemKey = mouseCode;
                        break;
                }

                // Persist mouse-based key changes as well
                Program.SaveSettings();
                if (Owner != null)
                {
                    ((MainForm)Owner).ChangePressKeyData(null);
                }

                this.Close();
            }
        }
    }
}

