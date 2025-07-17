using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Timers;

namespace WindowMonitorDemo
{
    public class Form1 : Form
    {
        IntPtr targetHwnd = IntPtr.Zero;
        IntPtr previousHwnd = IntPtr.Zero;
        System.Timers.Timer monitorTimer = new System.Timers.Timer(100);
        TextBox textBox1 = new TextBox();
        Button btnFind = new Button();
        NotifyIcon trayIcon = new NotifyIcon();
        ContextMenuStrip trayMenu = new ContextMenuStrip();
        Label statusLabel = new Label();

        // WinAPI
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x80000;
        const int LWA_ALPHA = 0x2;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        public Form1()
        {
            Text = "窗口控制器";
            Size = new Size(440, 180);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 248, 250);
            Font = new Font("Segoe UI", 10);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            Label titleLabel = new Label
            {
                Text = "请输入窗口标题关键字：",
                AutoSize = true,
                Location = new Point(20, 25),
                ForeColor = Color.FromArgb(50, 60, 80)
            };

            textBox1.Location = new Point(22, 50);
            textBox1.Width = 280;
            textBox1.BorderStyle = BorderStyle.FixedSingle;
            textBox1.BackColor = Color.White;
            textBox1.Font = new Font("Segoe UI", 10);

            btnFind.Location = new Point(315, 48);
            btnFind.Size = new Size(100, 30);
            btnFind.Text = "锁定窗口";
            btnFind.BackColor = Color.FromArgb(0, 120, 215);
            btnFind.ForeColor = Color.White;
            btnFind.FlatStyle = FlatStyle.Flat;
            btnFind.FlatAppearance.BorderSize = 0;
            btnFind.Font = new Font("Segoe UI", 10, FontStyle.Bold);

            statusLabel.Text = "当前未锁定任何窗口";
            statusLabel.AutoSize = true;
            statusLabel.ForeColor = Color.DarkGray;
            statusLabel.Location = new Point(22, 95);

            Controls.Add(titleLabel);
            Controls.Add(textBox1);
            Controls.Add(btnFind);
            Controls.Add(statusLabel);

            btnFind.Click += (s, e) =>
            {
                btnFind_Click(s, e);
                if (targetHwnd != IntPtr.Zero)
                {
                    statusLabel.Text = "已锁定窗口：✔️";
                    WindowState = FormWindowState.Minimized; // 自动最小化控制器
                }
                else
                {
                    statusLabel.Text = "未找到匹配窗口 ❌";
                }
            };

            monitorTimer.Elapsed += MonitorTargetWindow;
            monitorTimer.Start();

            trayIcon.Icon = new Icon("WinVisual.ico"); // 替换为你项目根目录的图标
            trayIcon.Text = "WinVisual";
            trayIcon.Visible = true;

            trayMenu.Items.Add("显示主窗体(Show)", null, (s, e) =>
            {
                Show();
                WindowState = FormWindowState.Normal;
            });
            trayMenu.Items.Add("退出程序(Exit)", null, (s, e) => Close());
            trayIcon.ContextMenuStrip = trayMenu;

            Resize += (s, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                    Hide();
            };
        }

        private void btnFind_Click(object sender, EventArgs e)
        {
            if (targetHwnd != IntPtr.Zero)
                RestoreWindow(targetHwnd);

            string partialTitle = textBox1.Text;
            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.ToString().IndexOf(partialTitle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    previousHwnd = targetHwnd;
                    targetHwnd = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }

        private void MonitorTargetWindow(object sender, ElapsedEventArgs e)
        {
            if (targetHwnd == IntPtr.Zero) return;

            IntPtr foreground = GetForegroundWindow();

            if (foreground != targetHwnd)
            {
                MakeWindowInvisible(targetHwnd);
            }
            else
            {
                GetWindowRect(targetHwnd, out RECT rect);
                Point cursor = Cursor.Position;
                bool inside =
                    cursor.X >= rect.Left && cursor.X <= rect.Right &&
                    cursor.Y >= rect.Top && cursor.Y <= rect.Bottom;

                if (inside)
                    MakeWindowVisible(targetHwnd);
                else
                    MakeWindowInvisible(targetHwnd);
            }
        }

        private void MakeWindowVisible(IntPtr hwnd)
        {
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            style |= WS_EX_LAYERED;
            SetWindowLong(hwnd, GWL_EXSTYLE, style);
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA); // 恢复不透明
            ShowWindow(hwnd, 5); // 显示窗口
        }

        private void MakeWindowInvisible(IntPtr hwnd)
        {
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            style |= WS_EX_LAYERED;
            SetWindowLong(hwnd, GWL_EXSTYLE, style);
            SetLayeredWindowAttributes(hwnd, 0, 0, LWA_ALPHA); // 完全透明
            
        }

        private void RestoreWindow(IntPtr hwnd)
        {
            ShowWindow(hwnd, 9);
            ShowWindow(hwnd, 5);

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (targetHwnd != IntPtr.Zero)
                RestoreWindow(targetHwnd);
            trayIcon.Visible = false;
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new Form1());
        }
    }
}
