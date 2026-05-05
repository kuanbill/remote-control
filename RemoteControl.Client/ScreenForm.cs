using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RemoteControl.Common;

namespace RemoteControl.Client
{
    public partial class ScreenForm : Form
    {
        private PictureBox _screenView;
        private Panel _panelContainer;
        private ScreenData _currentScreenData;
        private int _originalWidth;
        private int _originalHeight;
        private float _scaleFactor = 1.0f;
        private TrackBar _zoomTrackBar;
        private Label _lblZoom;
        private Label _lblKeyboardStatus;
        private ComboBox _cmbResolution;
        private Point _remoteCursorPos = new Point(-1, -1);
        private Size _lastScreenViewSize = Size.Empty;
        private int _lastResolutionIndex = -1;
        private float _lastAppliedScaleFactor = -1f;
        private readonly Dictionary<Keys, KeyboardEventData> _pressedKeys = new Dictionary<Keys, KeyboardEventData>();
        private readonly LowLevelKeyboardProc _keyboardProc;
        private IntPtr _keyboardHook = IntPtr.Zero;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WH_KEYBOARD_LL = 13;
        private const int LLKHF_EXTENDED = 0x01;
        private const int LLKHF_UP = 0x80;

        public event Action<MouseEventData> MouseEventOccurred;
        public event Action<KeyboardEventData> KeyboardEventOccurred;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public ScreenForm()
        {
            _keyboardProc = KeyboardHookCallback;
            InitializeComponent();
            KeyPreview = true;
            Resize += ScreenForm_Resize;
            Activated += ScreenForm_Activated;
            Deactivate += ScreenForm_Deactivate;
            HandleCreated += ScreenForm_HandleCreated;
            HandleDestroyed += ScreenForm_HandleDestroyed;
        }

        private void InitializeComponent()
        {
            Text = "遠端桌面";
            Size = new Size(1200, 800);
            MinimumSize = new Size(900, 700);
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.Sizable;
            DoubleBuffered = true;

            var panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            var lblResolution = new Label { Text = "顯示模式", Location = new Point(10, 12), Size = new Size(60, 20) };
            _cmbResolution = new ComboBox
            {
                Location = new Point(75, 9),
                Size = new Size(140, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbResolution.Items.AddRange(new string[] { "符合視窗", "原始大小", "800 x 600", "1024 x 768", "1280 x 720", "1920 x 1080" });
            _cmbResolution.SelectedIndex = 0;
            _cmbResolution.SelectedIndexChanged += CmbResolution_SelectedIndexChanged;

            _lblZoom = new Label { Text = "縮放: 100%", Location = new Point(235, 12), Size = new Size(90, 20) };
            _zoomTrackBar = new TrackBar
            {
                Location = new Point(320, 5),
                Size = new Size(200, 30),
                Minimum = 25,
                Maximum = 200,
                Value = 100,
                TickFrequency = 25
            };
            _zoomTrackBar.ValueChanged += ZoomTrackBar_ValueChanged;

            _lblKeyboardStatus = new Label
            {
                Text = "鍵盤: 準備中",
                Location = new Point(535, 12),
                Size = new Size(240, 20)
            };

            panelTop.Controls.AddRange(new Control[] { lblResolution, _cmbResolution, _lblZoom, _zoomTrackBar, _lblKeyboardStatus });

            _panelContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                AutoScroll = true,
                TabStop = true
            };

            _screenView = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
                TabStop = true
            };
            _screenView.MouseDown += ScreenView_MouseDown;
            _screenView.MouseUp += ScreenView_MouseUp;
            _screenView.MouseMove += ScreenView_MouseMove;
            _screenView.Paint += ScreenView_Paint;
            _screenView.PreviewKeyDown += InputSurface_PreviewKeyDown;
            _screenView.MouseEnter += InputSurface_MouseEnter;
            _screenView.MouseDown += InputSurface_MouseDown;

            _panelContainer.Controls.Add(_screenView);
            _panelContainer.PreviewKeyDown += InputSurface_PreviewKeyDown;
            _panelContainer.MouseEnter += InputSurface_MouseEnter;
            _panelContainer.MouseDown += InputSurface_MouseDown;

            Controls.Add(_panelContainer);
            Controls.Add(panelTop);
        }

        public void UpdateScreen(ScreenData screenData)
        {
            if (screenData == null)
            {
                return;
            }

            try
            {
                bool layoutChanged = screenData.Width != _originalWidth ||
                                     screenData.Height != _originalHeight;

                _currentScreenData = screenData;
                _originalWidth = screenData.Width;
                _originalHeight = screenData.Height;

                using var ms = new MemoryStream(screenData.ImageData);
                if (_screenView.Image != null)
                {
                    var oldImage = _screenView.Image;
                    _screenView.Image = null;
                    oldImage.Dispose();
                }

                _screenView.Image = Image.FromStream(ms);
                if (layoutChanged)
                {
                    AdjustScreenSize(forceRecenter: true);
                }
                _screenView.Invalidate();
            }
            catch
            {
            }
        }

        public void UpdateRemoteCursor(Point cursorPos)
        {
            _remoteCursorPos = cursorPos;
            _screenView?.Invalidate();
        }

        private void ScreenView_Paint(object sender, PaintEventArgs e)
        {
            if (_remoteCursorPos.X < 0 || _remoteCursorPos.Y < 0 || _currentScreenData == null || _screenView.Image == null)
            {
                return;
            }

            Rectangle imageRect = GetImageDisplayRectangle();
            if (imageRect.Width <= 0 || imageRect.Height <= 0)
            {
                return;
            }

            int relativeCursorX = _remoteCursorPos.X - _currentScreenData.ScreenLeft;
            int relativeCursorY = _remoteCursorPos.Y - _currentScreenData.ScreenTop;
            if (relativeCursorX < 0 || relativeCursorY < 0 || relativeCursorX >= _originalWidth || relativeCursorY >= _originalHeight)
            {
                return;
            }

            int cursorX = imageRect.Left + (int)Math.Round(relativeCursorX * imageRect.Width / (double)_originalWidth);
            int cursorY = imageRect.Top + (int)Math.Round(relativeCursorY * imageRect.Height / (double)_originalHeight);

            using (var pen = new Pen(Color.Red, 2))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Point[] arrow =
                {
                    new Point(cursorX, cursorY),
                    new Point(cursorX, cursorY + 20),
                    new Point(cursorX + 5, cursorY + 15),
                    new Point(cursorX + 10, cursorY + 25),
                    new Point(cursorX + 12, cursorY + 23),
                    new Point(cursorX + 7, cursorY + 13),
                    new Point(cursorX + 15, cursorY + 13)
                };
                e.Graphics.DrawLines(pen, arrow);
            }

            using var crossPen = new Pen(Color.FromArgb(128, Color.Lime), 1);
            e.Graphics.DrawLine(crossPen, cursorX - 10, cursorY, cursorX + 10, cursorY);
            e.Graphics.DrawLine(crossPen, cursorX, cursorY - 10, cursorX, cursorY + 10);
        }

        private Rectangle GetImageDisplayRectangle()
        {
            if (_screenView.Image == null || _screenView.Width <= 0 || _screenView.Height <= 0)
            {
                return Rectangle.Empty;
            }

            float imageAspect = _screenView.Image.Width / (float)_screenView.Image.Height;
            float boxAspect = _screenView.Width / (float)_screenView.Height;

            if (imageAspect > boxAspect)
            {
                int height = (int)Math.Round(_screenView.Width / imageAspect);
                int top = (_screenView.Height - height) / 2;
                return new Rectangle(0, top, _screenView.Width, height);
            }

            int width = (int)Math.Round(_screenView.Height * imageAspect);
            int left = (_screenView.Width - width) / 2;
            return new Rectangle(left, 0, width, _screenView.Height);
        }

        private void AdjustScreenSize(bool forceRecenter = false)
        {
            if (_currentScreenData == null || _originalWidth <= 0 || _originalHeight <= 0)
            {
                return;
            }

            Size baseSize = GetBaseDisplaySize();
            int targetWidth = Math.Max(1, (int)Math.Round(baseSize.Width * _scaleFactor));
            int targetHeight = Math.Max(1, (int)Math.Round(baseSize.Height * _scaleFactor));
            var newSize = new Size(targetWidth, targetHeight);
            bool sizeChanged = newSize != _lastScreenViewSize;
            bool viewModeChanged = _lastResolutionIndex != _cmbResolution.SelectedIndex ||
                                   Math.Abs(_lastAppliedScaleFactor - _scaleFactor) > float.Epsilon;

            _screenView.Size = newSize;
            _panelContainer.AutoScrollMinSize = _screenView.Size;
            if (forceRecenter || sizeChanged || viewModeChanged)
            {
                CenterScreenView();
            }

            _lastScreenViewSize = newSize;
            _lastResolutionIndex = _cmbResolution.SelectedIndex;
            _lastAppliedScaleFactor = _scaleFactor;
        }

        private Size GetBaseDisplaySize()
        {
            int availableWidth = Math.Max(1, _panelContainer.ClientSize.Width);
            int availableHeight = Math.Max(1, _panelContainer.ClientSize.Height);

            int boundWidth;
            int boundHeight;

            switch (_cmbResolution.SelectedIndex)
            {
                case 1:
                    return new Size(_originalWidth, _originalHeight);
                case 2:
                    boundWidth = 800;
                    boundHeight = 600;
                    break;
                case 3:
                    boundWidth = 1024;
                    boundHeight = 768;
                    break;
                case 4:
                    boundWidth = 1280;
                    boundHeight = 720;
                    break;
                case 5:
                    boundWidth = 1920;
                    boundHeight = 1080;
                    break;
                default:
                    boundWidth = availableWidth;
                    boundHeight = availableHeight;
                    break;
            }

            double scale = Math.Min(boundWidth / (double)_originalWidth, boundHeight / (double)_originalHeight);
            if (scale <= 0)
            {
                scale = 1;
            }

            return new Size(
                Math.Max(1, (int)Math.Round(_originalWidth * scale)),
                Math.Max(1, (int)Math.Round(_originalHeight * scale)));
        }

        private void CenterScreenView()
        {
            int visibleWidth = _panelContainer.ClientSize.Width;
            int visibleHeight = _panelContainer.ClientSize.Height;
            int x = Math.Max(0, (visibleWidth - _screenView.Width) / 2);
            int y = Math.Max(0, (visibleHeight - _screenView.Height) / 2);
            _screenView.Location = new Point(x, y);
        }

        private Point MapToRemote(Point point)
        {
            if (_currentScreenData == null || _screenView.Image == null)
            {
                return Point.Empty;
            }

            Rectangle imageRect = GetImageDisplayRectangle();
            if (imageRect.Width <= 0 || imageRect.Height <= 0 || !imageRect.Contains(point))
            {
                return new Point(int.MinValue, int.MinValue);
            }

            double normalizedX = (point.X - imageRect.Left) / (double)imageRect.Width;
            double normalizedY = (point.Y - imageRect.Top) / (double)imageRect.Height;

            int relativeX = Math.Max(0, Math.Min(_originalWidth - 1, (int)Math.Floor(normalizedX * _originalWidth)));
            int relativeY = Math.Max(0, Math.Min(_originalHeight - 1, (int)Math.Floor(normalizedY * _originalHeight)));

            return new Point(_currentScreenData.ScreenLeft + relativeX, _currentScreenData.ScreenTop + relativeY);
        }

        private void CmbResolution_SelectedIndexChanged(object sender, EventArgs e)
        {
            AdjustScreenSize();
        }

        private void ZoomTrackBar_ValueChanged(object sender, EventArgs e)
        {
            _scaleFactor = _zoomTrackBar.Value / 100f;
            _lblZoom.Text = $"縮放: {_zoomTrackBar.Value}%";
            AdjustScreenSize();
        }

        private void ScreenView_MouseDown(object sender, MouseEventArgs e)
        {
            SendMouseEvent(e, e.Button == MouseButtons.Left ? MouseEventType.LeftDown :
                              e.Button == MouseButtons.Right ? MouseEventType.RightDown :
                              MouseEventType.MiddleDown);
        }

        private void ScreenView_MouseUp(object sender, MouseEventArgs e)
        {
            SendMouseEvent(e, e.Button == MouseButtons.Left ? MouseEventType.LeftUp :
                              e.Button == MouseButtons.Right ? MouseEventType.RightUp :
                              MouseEventType.MiddleUp);
        }

        private void ScreenView_MouseMove(object sender, MouseEventArgs e)
        {
            SendMouseEvent(e, MouseEventType.Move);
        }

        private void InputSurface_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            // Mark navigation keys as input keys so WinForms does not swallow them.
            if (IsRemoteControlKey(e.KeyCode))
            {
                e.IsInputKey = true;
            }
        }

        private void InputSurface_MouseEnter(object sender, EventArgs e)
        {
            FocusInputSurface();
        }

        private void InputSurface_MouseDown(object sender, MouseEventArgs e)
        {
            FocusInputSurface();
        }

        private void SendMouseEvent(MouseEventArgs e, MouseEventType eventType)
        {
            Point point = MapToRemote(e.Location);
            if (point.X == int.MinValue)
            {
                return;
            }

            MouseEventOccurred?.Invoke(new MouseEventData
            {
                X = point.X,
                Y = point.Y,
                EventType = eventType
            });
        }

        private void ScreenForm_Activated(object sender, EventArgs e)
        {
            FocusInputSurface();
        }

        private void ScreenForm_HandleCreated(object sender, EventArgs e)
        {
            InstallKeyboardHook();
        }

        private void ScreenForm_HandleDestroyed(object sender, EventArgs e)
        {
            RemoveKeyboardHook();
        }

        private void ScreenForm_Deactivate(object sender, EventArgs e)
        {
            ReleasePressedKeys();
        }

        private void FocusInputSurface()
        {
            if (_screenView != null && _screenView.CanFocus)
            {
                ActiveControl = _screenView;
                _screenView.Select();
                _screenView.Focus();
            }
        }

        private void InstallKeyboardHook()
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                return;
            }

            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(null), 0);
            UpdateKeyboardStatus(_keyboardHook == IntPtr.Zero ? "鍵盤: 啟用失敗" : "鍵盤: 已啟用");
        }

        private void RemoveKeyboardHook()
        {
            if (_keyboardHook == IntPtr.Zero)
            {
                return;
            }

            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
            UpdateKeyboardStatus("鍵盤: 已停用");
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0 || !Visible || IsDisposed || GetForegroundWindow() != Handle)
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            int message = wParam.ToInt32();
            if (message != WM_KEYDOWN && message != WM_KEYUP && message != WM_SYSKEYDOWN && message != WM_SYSKEYUP)
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            var keyboardData = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool isKeyDown = (keyboardData.flags & LLKHF_UP) == 0;
            bool isExtendedKey = (keyboardData.flags & LLKHF_EXTENDED) != 0;
            SendKeyboardEvent((Keys)keyboardData.vkCode, (int)keyboardData.scanCode, isExtendedKey, isKeyDown);

            return (IntPtr)1;
        }

        private void SendKeyboardEvent(Keys keyCode, int scanCode, bool isExtendedKey, bool isKeyDown)
        {
            if (keyCode == Keys.None)
            {
                return;
            }

            if (isKeyDown)
            {
                _pressedKeys[keyCode] = new KeyboardEventData
                {
                    KeyCode = (int)keyCode,
                    ScanCode = scanCode,
                    IsExtendedKey = isExtendedKey,
                    IsKeyDown = true
                };
            }
            else
            {
                _pressedKeys.Remove(keyCode);
            }

            KeyboardEventOccurred?.Invoke(new KeyboardEventData
            {
                KeyCode = (int)keyCode,
                ScanCode = scanCode,
                IsExtendedKey = isExtendedKey,
                IsKeyDown = isKeyDown
            });
            UpdateKeyboardStatus($"鍵盤: 已送 {keyCode}");
        }

        private void ReleasePressedKeys()
        {
            if (_pressedKeys.Count == 0)
            {
                return;
            }

            foreach (KeyboardEventData keyEvent in new List<KeyboardEventData>(_pressedKeys.Values))
            {
                KeyboardEventOccurred?.Invoke(new KeyboardEventData
                {
                    KeyCode = keyEvent.KeyCode,
                    ScanCode = keyEvent.ScanCode,
                    IsExtendedKey = keyEvent.IsExtendedKey,
                    IsKeyDown = false
                });
            }

            _pressedKeys.Clear();
        }

        private void UpdateKeyboardStatus(string text)
        {
            if (_lblKeyboardStatus == null || _lblKeyboardStatus.IsDisposed)
            {
                return;
            }

            if (_lblKeyboardStatus.InvokeRequired)
            {
                _lblKeyboardStatus.BeginInvoke(new Action<string>(UpdateKeyboardStatus), text);
                return;
            }

            _lblKeyboardStatus.Text = text;
        }

        private static bool IsRemoteControlKey(Keys keyCode)
        {
            return keyCode == Keys.Tab ||
                   keyCode == Keys.Left ||
                   keyCode == Keys.Right ||
                   keyCode == Keys.Up ||
                   keyCode == Keys.Down ||
                   keyCode == Keys.Home ||
                   keyCode == Keys.End ||
                   keyCode == Keys.PageUp ||
                   keyCode == Keys.PageDown ||
                   keyCode == Keys.Insert ||
                   keyCode == Keys.Delete ||
                   keyCode == Keys.Escape;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (IsRemoteControlKey(keyData & Keys.KeyCode))
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        private void ScreenForm_Resize(object sender, EventArgs e)
        {
            AdjustScreenSize();
            _screenView?.Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            ReleasePressedKeys();
            RemoveKeyboardHook();
            Hide();
            e.Cancel = true;
        }
    }
}
