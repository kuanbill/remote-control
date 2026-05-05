using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using RemoteControl.Common;
using Message = RemoteControl.Common.Message;

namespace RemoteControl.Server
{
    public partial class ServerForm : Form
    {
        private const string StartupAppName = "RemoteControlServer";
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private readonly bool _startMinimized;
        private readonly ServerSettings _settings;
        private TcpListener _listener;
        private TcpClient _client;
        private NetworkManager _networkManager;
        private string _password = "123456";
        private bool _isRunning;
        private CancellationTokenSource _cancellationToken;
        private NotifyIcon _notifyIcon;
        private FileStream _receivingFile;
        private string _receivingFilePath;
        private CheckBox _chkAutoStart;
        private TextBox _txtPort;
        private TextBox _txtPassword;
        private Label _lblStatus;

        public ServerForm(bool startMinimized)
        {
            _startMinimized = startMinimized;
            _settings = ServerSettings.Load();
            InitializeComponent();
            Load += ServerForm_Load;
            Shown += ServerForm_Shown;
        }

        private void InitializeComponent()
        {
            Text = "遠端遙控 - 伺服器端";
            Size = new Size(420, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "遠端遙控 - 伺服器端",
                Visible = false
            };
            _notifyIcon.DoubleClick += (s, e) => ShowFromTray();

            var lblIP = new Label { Text = "本機 IP:", Location = new Point(20, 20), Size = new Size(70, 20) };
            var txtIP = new TextBox { Name = "txtIP", Location = new Point(100, 20), Size = new Size(260, 20), ReadOnly = true };
            txtIP.Text = GetLocalIP();

            var lblPort = new Label { Text = "連接埠:", Location = new Point(20, 55), Size = new Size(70, 20) };
            _txtPort = new TextBox { Name = "txtPort", Location = new Point(100, 55), Size = new Size(160, 20), Text = _settings.Port.ToString() };
            _txtPort.Leave += SettingsInput_Leave;

            var lblPassword = new Label { Text = "密碼:", Location = new Point(20, 90), Size = new Size(70, 20) };
            _txtPassword = new TextBox { Name = "txtPassword", Location = new Point(100, 90), Size = new Size(160, 20), PasswordChar = '*', Text = _settings.Password };
            _txtPassword.Leave += SettingsInput_Leave;

            _chkAutoStart = new CheckBox
            {
                Name = "chkAutoStart",
                Text = "開機自動啟動並最小化到系統匣",
                Location = new Point(20, 130),
                Size = new Size(280, 24),
                AutoSize = false
            };
            _chkAutoStart.CheckedChanged += ChkAutoStart_CheckedChanged;

            _lblStatus = new Label
            {
                Name = "lblStatus",
                Text = "狀態: 準備啟動中...",
                Location = new Point(20, 180),
                Size = new Size(360, 20)
            };

            Controls.AddRange(new Control[] { lblIP, txtIP, lblPort, _txtPort, lblPassword, _txtPassword, _chkAutoStart, _lblStatus });
        }

        private void ServerForm_Shown(object sender, EventArgs e)
        {
            bool startupEnabled = IsStartupEnabled();
            _chkAutoStart.CheckedChanged -= ChkAutoStart_CheckedChanged;
            _chkAutoStart.Checked = startupEnabled;
            _chkAutoStart.CheckedChanged += ChkAutoStart_CheckedChanged;
            _settings.AutoStart = startupEnabled;
            SaveSettings();

            if (_startMinimized)
            {
                HideToTray(false);
            }
        }

        private string GetLocalIP()
        {
            var ipList = new List<string>();

            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        continue;
                    }

                    if (ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Ethernet &&
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                    {
                        continue;
                    }

                    if (ni.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase) ||
                        ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                        ni.Description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase) ||
                        ni.Name.Contains("VMware", StringComparison.OrdinalIgnoreCase) ||
                        ni.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipList.Add($"{ni.Name}: {ua.Address}");
                        }
                    }
                }
            }
            catch
            {
            }

            if (ipList.Count == 0)
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipList.Add($"Default: {ip}");
                    }
                }
            }

            return ipList.Count > 0 ? string.Join("; ", ipList) : "127.0.0.1";
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false);
                if (key == null)
                {
                    return false;
                }

                return key.GetValue(StartupAppName) is string value &&
                       string.Equals(value, GetStartupCommand(), StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private void SetStartupEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true);
            if (key == null)
            {
                throw new InvalidOperationException("無法開啟 Windows 啟動登錄。");
            }

            if (enabled)
            {
                key.SetValue(StartupAppName, GetStartupCommand());
            }
            else
            {
                key.DeleteValue(StartupAppName, false);
            }
        }

        private string GetStartupCommand()
        {
            return $"\"{Application.ExecutablePath}\" --minimized";
        }

        private void ChkAutoStart_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                SetStartupEnabled(_chkAutoStart.Checked);
                _settings.AutoStart = _chkAutoStart.Checked;
                SaveSettings();
            }
            catch (Exception ex)
            {
                _chkAutoStart.CheckedChanged -= ChkAutoStart_CheckedChanged;
                _chkAutoStart.Checked = !_chkAutoStart.Checked;
                _chkAutoStart.CheckedChanged += ChkAutoStart_CheckedChanged;
                MessageBox.Show($"更新開機自啟設定失敗: {ex.Message}");
            }
        }

        private void SettingsInput_Leave(object sender, EventArgs e)
        {
            SaveSettingsFromInputs();
        }

        private void SaveSettingsFromInputs()
        {
            if (!int.TryParse(_txtPort.Text, out int port) || port <= 0 || port > 65535)
            {
                return;
            }

            _settings.Port = port;
            _settings.Password = _txtPassword.Text ?? string.Empty;
            _settings.AutoStart = _chkAutoStart.Checked;
            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"儲存設定失敗: {ex.Message}");
            }
        }

        private void UpdateStatus(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(UpdateStatus), text);
                return;
            }

            _lblStatus.Text = text;
        }

        private async void ServerForm_Load(object sender, EventArgs e)
        {
            _password = _txtPassword.Text;
            if (!int.TryParse(_txtPort.Text, out int port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("連接埠設定無效，將使用預設值 8888。");
                port = 8888;
                _txtPort.Text = "8888";
                SaveSettingsFromInputs();
            }

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;
            _cancellationToken = new CancellationTokenSource();
            UpdateStatus("狀態: 等待用戶端連線...");

            try
            {
                while (_isRunning && !_cancellationToken.IsCancellationRequested)
                {
                    UpdateStatus("狀態: 等待用戶端連線...");
                    _client = await _listener.AcceptTcpClientAsync();
                    _networkManager = new NetworkManager(_client);
                    UpdateStatus("狀態: 用戶端已連入");

                    try
                    {
                        await HandleClient();
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        CleanupClientConnection();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    MessageBox.Show($"伺服器錯誤: {ex.Message}");
                }
            }
        }

        private async Task HandleClient()
        {
            while (_client != null && _client.Connected)
            {
                var message = await _networkManager.ReceiveMessageAsync();
                switch (message.Type)
                {
                    case MessageType.Connect:
                        await HandleConnectAsync(message);
                        break;
                    case MessageType.MouseEvent:
                        HandleMouseEvent(message);
                        break;
                    case MessageType.KeyboardEvent:
                        HandleKeyboardEvent(message);
                        break;
                    case MessageType.FileTransferRequest:
                        await HandleFileTransferRequestAsync(message);
                        break;
                    case MessageType.FileTransferData:
                        HandleFileTransferData(message);
                        break;
                    case MessageType.FileTransferComplete:
                        HandleFileTransferComplete();
                        break;
                }
            }
        }

        private async Task HandleConnectAsync(Message message)
        {
            var request = message.Data as ConnectRequest;
            bool success = request?.Password == _password;
            var response = new ConnectResponse
            {
                Success = success,
                Message = success ? "連線成功。" : "密碼錯誤。"
            };

            await _networkManager.SendMessageAsync(new Message { Type = MessageType.ConnectResponse, Data = response });
            if (response.Success)
            {
                StartScreenCapture();
            }
        }

        private void HandleMouseEvent(Message message)
        {
            if (message.Data is MouseEventData mouseEvent)
            {
                InputSimulator.SimulateMouseEvent(mouseEvent);
            }
        }

        private void HandleKeyboardEvent(Message message)
        {
            if (message.Data is KeyboardEventData keyEvent)
            {
                InputSimulator.SimulateKeyboardEvent(keyEvent);
            }
        }

        private async Task HandleFileTransferRequestAsync(Message message)
        {
            var fileInfo = message.Data as FileTransferInfo;
            try
            {
                Directory.CreateDirectory(fileInfo.TargetPath);
                _receivingFilePath = Path.Combine(fileInfo.TargetPath, fileInfo.FileName);
                _receivingFile = new FileStream(_receivingFilePath, FileMode.Create, FileAccess.Write);
                await _networkManager.SendMessageAsync(new Message { Type = MessageType.FileTransferResponse, Data = true });
            }
            catch (Exception ex)
            {
                await _networkManager.SendMessageAsync(new Message { Type = MessageType.FileTransferResponse, Data = false });
                MessageBox.Show($"檔案接收初始化失敗: {ex.Message}");
            }
        }

        private void HandleFileTransferData(Message message)
        {
            if (_receivingFile == null || message.Data is not byte[] fileData)
            {
                return;
            }

            _receivingFile.Write(fileData, 0, fileData.Length);
        }

        private void HandleFileTransferComplete()
        {
            if (_receivingFile == null)
            {
                return;
            }

            _receivingFile.Close();
            _receivingFile = null;
            MessageBox.Show($"檔案接收完成: {_receivingFilePath}");
        }

        private void StartScreenCapture()
        {
            _ = Task.Run(async () =>
            {
                while (!_cancellationToken.Token.IsCancellationRequested && _client != null && _client.Connected)
                {
                    await SendScreenCaptureAsync();
                    await Task.Delay(100, _cancellationToken.Token);
                }
            }, _cancellationToken.Token);
        }

        private async Task SendScreenCaptureAsync()
        {
            try
            {
                var capture = ScreenCapture.CaptureScreen();
                var cursorPos = Cursor.Position;
                var screenData = new ScreenData
                {
                    ImageData = capture.ImageData,
                    Width = capture.Width,
                    Height = capture.Height,
                    ScreenLeft = capture.ScreenLeft,
                    ScreenTop = capture.ScreenTop,
                    CursorX = cursorPos.X,
                    CursorY = cursorPos.Y
                };

                await _networkManager.SendMessageAsync(new Message { Type = MessageType.ScreenCapture, Data = screenData });
            }
            catch
            {
            }
        }

        private void CleanupClientConnection()
        {
            _receivingFile?.Dispose();
            _receivingFile = null;
            _networkManager?.Dispose();
            _networkManager = null;
            _client?.Close();
            _client = null;
            UpdateStatus("狀態: 等待用戶端連線...");
        }

        private void HideToTray(bool showBalloon)
        {
            WindowState = FormWindowState.Minimized;
            Hide();
            _notifyIcon.Visible = true;

            if (showBalloon)
            {
                _notifyIcon.ShowBalloonTip(3000, "遠端遙控", "伺服器已最小化到系統匣。", ToolTipIcon.Info);
            }
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            _notifyIcon.Visible = false;
        }

        private void StopServer()
        {
            _isRunning = false;
            _cancellationToken?.Cancel();
            _receivingFile?.Dispose();
            _receivingFile = null;
            _networkManager?.Dispose();
            _networkManager = null;
            _client?.Close();
            _client = null;
            _listener?.Stop();
            _listener = null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettingsFromInputs();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            StopServer();
            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized && Visible)
            {
                HideToTray(true);
            }
        }
    }
}
