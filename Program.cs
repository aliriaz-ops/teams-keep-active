using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TeamsKeepActive
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApp());
        }
    }

    /// <summary>
    /// Runs entirely in the system tray. Periodically sends a single, invisible
    /// F15 keystroke (a key that doesn't exist on real keyboards and that no app
    /// reacts to) so Windows resets its idle timer and Teams doesn't flip to Away.
    /// It does NOT move your mouse or type into whatever window has focus.
    /// </summary>
    public class TrayApp : ApplicationContext
    {
        // --- Win32 interop -------------------------------------------------
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_F15 = 0x7E;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // --- UI state --------------------------------------------------------
        private readonly NotifyIcon _trayIcon;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly System.Windows.Forms.Timer _durationWatchdog;
        private readonly ToolStripMenuItem _toggleItem;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem[] _intervalItems;
        private readonly ToolStripMenuItem[] _durationItems;

        private bool _running = true;
        private int _intervalMinutes = 3; // Teams goes "Away" after ~5 min idle; 3 gives headroom

        // null = run forever until manually paused/exited. Otherwise, the UTC time to auto-pause at.
        private DateTime? _stopAtUtc = null;

        public TrayApp()
        {
            var menu = new ContextMenuStrip();

            _statusItem = new ToolStripMenuItem("Status: Active") { Enabled = false };
            menu.Items.Add(_statusItem);
            menu.Items.Add(new ToolStripSeparator());

            _toggleItem = new ToolStripMenuItem("Pause", null, OnToggle);
            menu.Items.Add(_toggleItem);
            menu.Items.Add(new ToolStripSeparator());

            var intervalMenu = new ToolStripMenuItem("Interval");
            _intervalItems = new ToolStripMenuItem[] {
                MakeIntervalItem(1, intervalMenu),
                MakeIntervalItem(2, intervalMenu),
                MakeIntervalItem(3, intervalMenu),
                MakeIntervalItem(4, intervalMenu),
            };
            foreach (var item in _intervalItems) intervalMenu.DropDownItems.Add(item);
            menu.Items.Add(intervalMenu);

            var durationMenu = new ToolStripMenuItem("Run for");
            _durationItems = new ToolStripMenuItem[] {
                MakeDurationItem("1 Hour", TimeSpan.FromHours(1)),
                MakeDurationItem("4 Hours", TimeSpan.FromHours(4)),
                MakeDurationItem("24 Hours", TimeSpan.FromHours(24)),
                MakeDurationItem("Forever", null),
            };
            foreach (var item in _durationItems) durationMenu.DropDownItems.Add(item);
            menu.Items.Add(durationMenu);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(new ToolStripMenuItem("Exit", null, OnExit));

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Teams Keep Active",
                Visible = true,
                ContextMenuStrip = menu
            };
            _trayIcon.DoubleClick += OnToggle;

            UpdateIntervalCheckmarks();
            UpdateDurationCheckmarks(); // defaults to "Forever"

            _timer = new System.Windows.Forms.Timer { Interval = _intervalMinutes * 60 * 1000 };
            _timer.Tick += (s, e) => Nudge();
            _timer.Start();

            // Checks once a minute whether a timed "Run for" duration has elapsed.
            _durationWatchdog = new System.Windows.Forms.Timer { Interval = 60 * 1000 };
            _durationWatchdog.Tick += (s, e) => CheckDurationElapsed();
            _durationWatchdog.Start();
        }

        private ToolStripMenuItem MakeDurationItem(string label, TimeSpan? duration)
        {
            var item = new ToolStripMenuItem(label) { Tag = duration };
            item.Click += (s, e) =>
            {
                _stopAtUtc = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : (DateTime?)null;
                if (!_running) OnToggle(s, e); // starting a duration also resumes if paused
                UpdateDurationCheckmarks();
                UpdateStatusText();
            };
            return item;
        }

        private void UpdateDurationCheckmarks()
        {
            foreach (var item in _durationItems)
            {
                var d = item.Tag as TimeSpan?;
                bool isForever = d is null;
                item.Checked = isForever ? _stopAtUtc is null : _stopAtUtc.HasValue &&
                    Math.Abs((_stopAtUtc.Value - DateTime.UtcNow - d!.Value).TotalMinutes) < 1;
            }
        }

        private void CheckDurationElapsed()
        {
            if (_running && _stopAtUtc.HasValue && DateTime.UtcNow >= _stopAtUtc.Value)
            {
                _running = false;
                _stopAtUtc = null;
                _toggleItem.Text = "Resume";
                _trayIcon.Text = "Teams Keep Active - paused";
                _trayIcon.ShowBalloonTip(5000, "Teams Keep Active",
                    "Duration finished — paused. Choose Resume or set a new duration.", ToolTipIcon.Info);
                UpdateDurationCheckmarks();
                UpdateStatusText();
            }
        }

        private void UpdateStatusText()
        {
            if (!_running) { _statusItem.Text = "Status: Paused"; return; }
            _statusItem.Text = _stopAtUtc.HasValue
                ? $"Status: Active until {_stopAtUtc.Value.ToLocalTime():h:mm tt}"
                : "Status: Active (forever)";
        }

        private ToolStripMenuItem MakeIntervalItem(int minutes, ToolStripMenuItem parent)
        {
            var item = new ToolStripMenuItem($"{minutes} minute{(minutes == 1 ? "" : "s")}")
            {
                CheckOnClick = false,
                Tag = minutes
            };
            item.Click += (s, e) =>
            {
                _intervalMinutes = minutes;
                _timer.Interval = minutes * 60 * 1000;
                UpdateIntervalCheckmarks();
            };
            return item;
        }

        private void UpdateIntervalCheckmarks()
        {
            foreach (var item in _intervalItems)
                item.Checked = item.Tag is int m && m == _intervalMinutes;
        }

        private void Nudge()
        {
            if (!_running) return;
            keybd_event(VK_F15, 0, 0, UIntPtr.Zero);
            keybd_event(VK_F15, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void OnToggle(object? sender, EventArgs e)
        {
            _running = !_running;
            _toggleItem.Text = _running ? "Pause" : "Resume";
            _trayIcon.Text = _running
                ? "Teams Keep Active - running"
                : "Teams Keep Active - paused";
            UpdateStatusText();
        }

        private void OnExit(object? sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _timer.Stop();
            _durationWatchdog.Stop();
            Application.Exit();
        }
    }
}
