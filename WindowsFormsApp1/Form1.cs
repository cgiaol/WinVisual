using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Timers;
using System.IO;

namespace WindowMonitorDemo
{
    public class Form1 : Form
    {
        // 状态与目标句柄
        IntPtr targetHwnd = IntPtr.Zero;
        bool isHidden = false;

        // 计时器（监控目标窗口）
        System.Timers.Timer monitorTimer = new System.Timers.Timer(100);

        // UI 控件（保留为字段便于事件处理）
        TextBox txtWindowKeyword;
        Button btnLockWindow;
        Label lblLockedWindow;

        TextBox txtHotkey;
        Button btnRegisterHotkey;
        Button btnUnregisterHotkey;

        Label lblStatus;
        NotifyIcon trayIcon;

        // 热键状态
        int currentHotkeyId = 0;
        uint currentModifiers = 0;
        uint currentVk = 0;
        int nextHotkeyId = 1;

        // WinAPI
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x80000;
        const int LWA_ALPHA = 0x2;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        const int SW_RESTORE = 9;

        const uint MOD_ALT = 0x0001;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const uint MOD_WIN = 0x0008;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        public Form1()
        {
            InitializeComponent();
            monitorTimer.Elapsed += MonitorTargetWindow;
            monitorTimer.Start();
        }

        void InitializeComponent()
        {
            // 窗体基础
            Text = "窗口控制器";
            Size = new Size(580, 410);
            MinimumSize = new Size(520, 320);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            BackColor = Color.White;

            // --- 尝试加载项目根目录的 WinVisual.ico 作为窗体与托盘图标（如果存在）
            string exeDir = AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory;
            string icoPath = Path.Combine(exeDir, "WinVisual.ico");
            try
            {
                if (File.Exists(icoPath))
                {
                    this.Icon = new Icon(icoPath);
                }
                else
                {
                    // 若项目根（发布文件夹）中未找到，尝试上级目录再 fallback
                    string parentIco = Path.Combine(Path.GetDirectoryName(exeDir) ?? exeDir, "WinVisual.ico");
                    if (File.Exists(parentIco)) this.Icon = new Icon(parentIco);
                }
            }
            catch
            {
                // 若加载失败，保持默认系统图标，避免抛出异常
                this.Icon = SystemIcons.Application;
            }

            // 主布局表格
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12),
                BackColor = Color.White
            };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); // 目标窗口区域
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // 热键区域
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 占位与底部
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // 状态条
            Controls.Add(main);

            // 1) 目标窗口区 (GroupBox)
            var gbTarget = new GroupBox
            {
                Text = "目标窗口",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            main.Controls.Add(gbTarget, 0, 0);

            var tblTarget = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3
            };
            tblTarget.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            tblTarget.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            tblTarget.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            tblTarget.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            tblTarget.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            tblTarget.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            gbTarget.Controls.Add(tblTarget);

            var lblKeyword = new Label { Text = "窗口标题关键字", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
            txtWindowKeyword = new TextBox { Dock = DockStyle.Fill };
            // 简单 placeholder 效果（兼容旧框架）
            txtWindowKeyword.Enter += (s, e) =>
            {
                if (txtWindowKeyword.Text == "例如：Chrome 或 Visual Studio")
                {
                    txtWindowKeyword.Text = "";
                    txtWindowKeyword.ForeColor = Color.Black;
                }
            };
            txtWindowKeyword.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtWindowKeyword.Text))
                {
                    txtWindowKeyword.Text = "例如：Chrome 或 Visual Studio";
                    txtWindowKeyword.ForeColor = Color.Gray;
                }
            };
            txtWindowKeyword.Text = "例如：Chrome 或 Visual Studio";
            txtWindowKeyword.ForeColor = Color.Gray;

            btnLockWindow = new Button { Text = "锁定窗口", Dock = DockStyle.Fill, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnLockWindow.FlatAppearance.BorderSize = 0;

            tblTarget.Controls.Add(lblKeyword, 0, 0);
            tblTarget.Controls.Add(txtWindowKeyword, 0, 1);
            tblTarget.SetColumnSpan(txtWindowKeyword, 2);
            tblTarget.Controls.Add(btnLockWindow, 2, 1);

            lblLockedWindow = new Label { Text = "未锁定", Dock = DockStyle.Fill, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft };
            tblTarget.Controls.Add(lblLockedWindow, 0, 2);
            tblTarget.SetColumnSpan(lblLockedWindow, 3);

            // 2) 热键区 (GroupBox)
            var gbHotkey = new GroupBox
            {
                Text = "热键（隐藏 / 恢复 切换）",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            main.Controls.Add(gbHotkey, 0, 1);

            var tblHotkey = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3
            };
            tblHotkey.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            tblHotkey.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22.5f));
            tblHotkey.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22.5f));
            tblHotkey.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            tblHotkey.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            tblHotkey.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            gbHotkey.Controls.Add(tblHotkey);

            var lblHotkey = new Label { Text = "录入热键", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            txtHotkey = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.WhiteSmoke };
            // 简单 placeholder 效果
            txtHotkey.Enter += (s, e) =>
            {
                if (txtHotkey.Text == "点击此框，然后按下组合键")
                {
                    txtHotkey.Text = "";
                    txtHotkey.ForeColor = Color.Black;
                }
            };
            txtHotkey.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtHotkey.Text))
                {
                    txtHotkey.Text = "点击此框，然后按下组合键";
                    txtHotkey.ForeColor = Color.Gray;
                }
            };
            txtHotkey.Text = "点击此框，然后按下组合键";
            txtHotkey.ForeColor = Color.Gray;
            txtHotkey.GotFocus += (s, e) => txtHotkey.Text = "按下组合键…";
            txtHotkey.KeyDown += HotkeyBox_KeyDown;
            txtHotkey.MouseDown += (s, e) => txtHotkey.Focus();

            btnRegisterHotkey = new Button { Text = "注册", Dock = DockStyle.Fill, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnRegisterHotkey.FlatAppearance.BorderSize = 0;
            btnUnregisterHotkey = new Button { Text = "注销", Dock = DockStyle.Fill, BackColor = Color.FromArgb(150, 150, 150), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnUnregisterHotkey.FlatAppearance.BorderSize = 0;

            tblHotkey.Controls.Add(lblHotkey, 0, 0);
            tblHotkey.Controls.Add(txtHotkey, 0, 1);
            tblHotkey.Controls.Add(btnRegisterHotkey, 1, 1);
            tblHotkey.Controls.Add(btnUnregisterHotkey, 2, 1);

            // 右侧放一些说明
            var lblHotkeyHint = new Label
            {
                Text = "说明：\r\n- 单击录入框并按下你想要的组合键\r\n- 建议使用 Ctrl / Alt / Shift 作为修饰键\r\n- 注册后按下热键进行隐藏/恢复",
                Dock = DockStyle.Fill,
                ForeColor = Color.Gray
            };
            tblHotkey.Controls.Add(lblHotkeyHint, 0, 2);
            tblHotkey.SetColumnSpan(lblHotkeyHint, 3);

            // 3) 操作区（占位，可扩展）
            var panelActions = new Panel { Dock = DockStyle.Fill };
            main.Controls.Add(panelActions, 0, 2);
            // 在中间放一些快速操作按钮（恢复显示、暂停监控）
            var quick = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            panelActions.Controls.Add(quick);

            var btnRestoreNow = new Button { Text = "立即恢复窗口", AutoSize = true, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnRestoreNow.FlatAppearance.BorderSize = 0;
            var btnPauseMonitor = new Button { Text = "暂停监控", AutoSize = true, BackColor = Color.FromArgb(100, 100, 100), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnPauseMonitor.FlatAppearance.BorderSize = 0;
            quick.Controls.Add(btnRestoreNow);
            quick.Controls.Add(btnPauseMonitor);

            // 底部状态栏
            var statusBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(250, 250, 250) };
            main.Controls.Add(statusBar, 0, 3);
            lblStatus = new Label { Text = "就绪", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray };
            statusBar.Controls.Add(lblStatus);

            // 托盘图标
            // 使用同一个 WinVisual.ico，如果加载失败回退到 SystemIcons.Application
            try
            {
                if (File.Exists(icoPath))
                {
                    trayIcon = new NotifyIcon { Icon = new Icon(icoPath), Visible = true, Text = "窗口控制器" };
                }
                else
                {
                    trayIcon = new NotifyIcon { Icon = this.Icon ?? SystemIcons.Application, Visible = true, Text = "窗口控制器" };
                }
            }
            catch
            {
                trayIcon = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = "窗口控制器" };
            }

            var cm = new ContextMenuStrip();
            cm.Items.Add("显示主窗体", null, (s, e) => { Show(); WindowState = FormWindowState.Normal; });
            cm.Items.Add("退出", null, (s, e) => Close());
            trayIcon.ContextMenuStrip = cm;

            // 事件绑定
            btnLockWindow.Click += BtnLockWindow_Click;
            btnRegisterHotkey.Click += BtnRegisterHotkey_Click;
            btnUnregisterHotkey.Click += BtnUnregisterHotkey_Click;
            btnRestoreNow.Click += (s, e) => { if (targetHwnd != IntPtr.Zero) RestoreCompletely(targetHwnd); UpdateStatus("已恢复窗口显示"); };
            btnPauseMonitor.Click += (s, e) => { if (monitorTimer.Enabled) { monitorTimer.Stop(); btnPauseMonitor.Text = "恢复监控"; UpdateStatus("监控已暂停"); } else { monitorTimer.Start(); btnPauseMonitor.Text = "暂停监控"; UpdateStatus("监控已恢复"); } };

            Resize += (s, e) => { if (WindowState == FormWindowState.Minimized) Hide(); };

            // 视觉微调
            foreach (Control c in new Control[] { btnLockWindow, btnRegisterHotkey, btnUnregisterHotkey, btnRestoreNow, btnPauseMonitor })
            {
                c.Padding = new Padding(6, 3, 6, 3);
                c.Cursor = Cursors.Hand;
                if (c is Button b) b.FlatStyle = FlatStyle.Flat;
            }

            // 初始显示提示
            UpdateStatus("请先锁定目标窗口并注册热键");
        }

        // UI 辅助
        void UpdateStatus(string text)
        {
            if (lblStatus.IsHandleCreated)
                lblStatus.Invoke((Action)(() => lblStatus.Text = text));
        }

        // 锁定窗口按钮
        private void BtnLockWindow_Click(object sender, EventArgs e)
        {
            string keyword = txtWindowKeyword.Text?.Trim();
            if (string.IsNullOrEmpty(keyword) || keyword == "例如：Chrome 或 Visual Studio")
            {
                MessageBox.Show("请输入窗口标题关键字再锁定", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (targetHwnd != IntPtr.Zero)
                RestoreCompletely(targetHwnd);

            // 查找窗口
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.ToString().IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (found == IntPtr.Zero)
            {
                targetHwnd = IntPtr.Zero;
                lblLockedWindow.Text = "未找到匹配窗口";
                UpdateStatus("未找到匹配窗口");
            }
            else
            {
                targetHwnd = found;
                lblLockedWindow.Text = "已锁定: " + (GetWindowTitle(found) ?? "句柄 " + found.ToInt64());
                UpdateStatus("已锁定目标窗口");
                WindowState = FormWindowState.Minimized; // 自动最小化界面，便于热键使用
            }
        }

        string GetWindowTitle(IntPtr hWnd)
        {
            var sb = new StringBuilder(512);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        // 热键录入
        private void HotkeyBox_KeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            uint mods = 0;
            if (e.Control) mods |= MOD_CONTROL;
            if (e.Alt) mods |= MOD_ALT;
            if (e.Shift) mods |= MOD_SHIFT;
            if ((Control.ModifierKeys & Keys.LWin) == Keys.LWin || (Control.ModifierKeys & Keys.RWin) == Keys.RWin)
                mods |= MOD_WIN;

            Keys key = e.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
            {
                txtHotkey.Text = BuildHotkeyString(mods, 0);
                currentModifiers = mods;
                currentVk = 0;
                return;
            }

            uint vk = (uint)key;
            txtHotkey.Text = BuildHotkeyString(mods, vk);
            currentModifiers = mods;
            currentVk = vk;
        }

        string BuildHotkeyString(uint mods, uint vk)
        {
            var sb = new StringBuilder();
            if ((mods & MOD_CONTROL) != 0) sb.Append("Ctrl+");
            if ((mods & MOD_ALT) != 0) sb.Append("Alt+");
            if ((mods & MOD_SHIFT) != 0) sb.Append("Shift+");
            if ((mods & MOD_WIN) != 0) sb.Append("Win+");
            if (vk != 0) sb.Append(((Keys)vk).ToString());
            else if (sb.Length > 0) sb.Append("（仅修饰键）");
            else sb.Append("无");
            return sb.ToString();
        }

        // 注册/注销热键
        private void BtnRegisterHotkey_Click(object sender, EventArgs e)
        {
            if (currentVk == 0 && currentModifiers == 0)
            {
                MessageBox.Show("请在录入框按下你想要的组合键后再注册", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (currentHotkeyId != 0)
            {
                UnregisterHotKey(Handle, currentHotkeyId);
                currentHotkeyId = 0;
            }

            int id = nextHotkeyId++;
            bool ok = RegisterHotKey(Handle, id, currentModifiers, currentVk);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                MessageBox.Show($"注册热键失败（错误码 {err}）。可能被占用或权限不足。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("热键注册失败");
                return;
            }

            currentHotkeyId = id;
            UpdateStatus("热键已注册：" + BuildHotkeyString(currentModifiers, currentVk));
        }

        private void BtnUnregisterHotkey_Click(object sender, EventArgs e)
        {
            if (currentHotkeyId != 0)
            {
                UnregisterHotKey(Handle, currentHotkeyId);
                currentHotkeyId = 0;
                UpdateStatus("热键已注销");
            }
            else
            {
                MessageBox.Show("当前没有注册的热键", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // 监控回调（在线程池线程）
        private void MonitorTargetWindow(object sender, ElapsedEventArgs e)
        {
            if (targetHwnd == IntPtr.Zero || isHidden) return;

            IntPtr fg = GetForegroundWindow();
            if (fg != targetHwnd)
            {
                MakeWindowInvisible(targetHwnd);
            }
            else
            {
                GetWindowRect(targetHwnd, out RECT rect);
                Point cursor = Cursor.Position;
                bool inside = cursor.X >= rect.Left && cursor.X <= rect.Right && cursor.Y >= rect.Top && cursor.Y <= rect.Bottom;
                if (inside) MakeWindowVisible(targetHwnd); else MakeWindowInvisible(targetHwnd);
            }
        }

        private void MakeWindowVisible(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            style |= WS_EX_LAYERED;
            SetWindowLong(hwnd, GWL_EXSTYLE, style);
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
            ShowWindow(hwnd, SW_SHOW);
        }

        private void MakeWindowInvisible(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            style |= WS_EX_LAYERED;
            SetWindowLong(hwnd, GWL_EXSTYLE, style);
            SetLayeredWindowAttributes(hwnd, 0, 0, LWA_ALPHA);
        }

        private void RestoreCompletely(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_APPWINDOW;
            exStyle &= ~WS_EX_TOOLWINDOW;
            exStyle |= WS_EX_LAYERED;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
            ShowWindow(hwnd, SW_RESTORE);
            ShowWindow(hwnd, SW_SHOW);
        }

        private void HideCompletely(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle &= ~WS_EX_APPWINDOW;
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            ShowWindow(hwnd, SW_HIDE);
        }

        // 处理热键消息
        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY && currentHotkeyId != 0 && m.WParam.ToInt32() == currentHotkeyId)
            {
                if (targetHwnd == IntPtr.Zero)
                {
                    base.WndProc(ref m);
                    return;
                }

                if (!isHidden)
                {
                    monitorTimer.Stop();
                    HideCompletely(targetHwnd);
                    isHidden = true;
                    UpdateStatus("窗口已隐藏");
                }
                else
                {
                    RestoreCompletely(targetHwnd);
                    monitorTimer.Start();
                    isHidden = false;
                    UpdateStatus("窗口已恢复");
                }
            }
            base.WndProc(ref m);
        }

        // 退出时清理
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (currentHotkeyId != 0) UnregisterHotKey(Handle, currentHotkeyId);
            if (targetHwnd != IntPtr.Zero) RestoreCompletely(targetHwnd);
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
