using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using RemoteControl.Common;
using Message = RemoteControl.Common.Message;

public class SavedServer
{
    public string Name { get; set; }
    public string IP { get; set; }
    public int Port { get; set; }
    public string Password { get; set; }
}

namespace RemoteControl.Client
{
    public partial class ClientForm : Form
    {
        private TcpClient _client;
        private NetworkManager _networkManager;
        private bool _isConnected;
        private ScreenForm _screenForm;
        private ListBox _serverList;
        private Timer _clipboardTimer;
        private uint _lastClipboardSeq;
        private bool _isUpdatingClipboard;

        private const string SavedServersFile = "saved_servers.json";

        public ClientForm()
        {
            InitializeComponent();
            LoadSavedServers();
        }

        private void InitializeComponent()
        {
            Text = "遠端遙控 - 客戶端";
            Size = new Size(400, 600);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            var lblIP = new Label { Text = "伺服器 IP:", Location = new Point(10, 20), Size = new Size(75, 20) };
            var txtIP = new TextBox { Name = "txtIP", Location = new Point(90, 20), Size = new Size(140, 20) };

            var lblPort = new Label { Text = "連接埠:", Location = new Point(10, 50), Size = new Size(75, 20) };
            var txtPort = new TextBox { Name = "txtPort", Location = new Point(90, 50), Size = new Size(140, 20), Text = "8888" };

            var lblPassword = new Label { Text = "密碼:", Location = new Point(10, 80), Size = new Size(75, 20) };
            var txtPassword = new TextBox { Name = "txtPassword", Location = new Point(90, 80), Size = new Size(140, 20), PasswordChar = '*' };

            var btnConnect = new Button { Name = "btnConnect", Text = "連線", Location = new Point(90, 120), Size = new Size(140, 30) };
            btnConnect.Click += BtnConnect_Click;

            var btnSaveServer = new Button { Name = "btnSaveServer", Text = "儲存伺服器", Location = new Point(90, 160), Size = new Size(140, 30) };
            btnSaveServer.Click += BtnSaveServer_Click;

            var btnDeleteServer = new Button { Name = "btnDeleteServer", Text = "刪除伺服器", Location = new Point(90, 200), Size = new Size(140, 30) };
            btnDeleteServer.Click += BtnDeleteServer_Click;

            var btnSendFile = new Button { Name = "btnSendFile", Text = "傳送檔案", Location = new Point(90, 240), Size = new Size(140, 30) };
            btnSendFile.Click += BtnSendFile_Click;

            var lblStatus = new Label { Name = "lblStatus", Text = "狀態: 尚未連線", Location = new Point(10, 285), Size = new Size(250, 20) };

            var lblServerList = new Label { Text = "已儲存的伺服器", Location = new Point(10, 315), Size = new Size(220, 20) };
            _serverList = new ListBox { Name = "lstServers", Location = new Point(10, 340), Size = new Size(220, 205) };
            _serverList.SelectedIndexChanged += ServerList_SelectedIndexChanged;

            Controls.AddRange(new Control[]
            {
                lblIP, txtIP, lblPort, txtPort, lblPassword, txtPassword,
                btnConnect, btnSaveServer, btnDeleteServer, btnSendFile,
                lblStatus, lblServerList, _serverList
            });
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            var txtIP = Controls.Find("txtIP", true)[0] as TextBox;
            var txtPort = Controls.Find("txtPort", true)[0] as TextBox;
            var txtPassword = Controls.Find("txtPassword", true)[0] as TextBox;
            var btnConnect = Controls.Find("btnConnect", true)[0] as Button;
            var lblStatus = Controls.Find("lblStatus", true)[0] as Label;

            if (_isConnected)
            {
                Disconnect();
                btnConnect.Text = "連線";
                lblStatus.Text = "狀態: 已中斷連線";
                return;
            }

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(txtIP.Text, int.Parse(txtPort.Text));
                _networkManager = new NetworkManager(_client);
                _isConnected = true;
                btnConnect.Text = "中斷連線";
                lblStatus.Text = "狀態: 連線中...";

                var connectRequest = new ConnectRequest { Password = txtPassword.Text };
                await _networkManager.SendMessageAsync(new Message { Type = MessageType.Connect, Data = connectRequest });

                var response = await _networkManager.ReceiveMessageAsync();
                var connectResponse = response.Data as ConnectResponse;
                if (connectResponse?.Success == true)
                {
                    lblStatus.Text = "狀態: 已連線";
                    _screenForm = new ScreenForm();
                    _screenForm.MouseEventOccurred += ScreenForm_MouseEventOccurred;
                    _screenForm.KeyboardEventOccurred += ScreenForm_KeyboardEventOccurred;
                    _screenForm.CtrlAltDelRequested += ScreenForm_CtrlAltDelRequested;
                    _screenForm.Show();
                    StartReceiving();
                    StartClipboardMonitor();
                }
                else
                {
                    MessageBox.Show(connectResponse?.Message ?? "連線失敗。", "連線失敗");
                    Disconnect();
                    btnConnect.Text = "連線";
                    lblStatus.Text = "狀態: 連線失敗";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"連線錯誤: {ex.Message}");
                Disconnect();
            }
        }

        private async void ScreenForm_MouseEventOccurred(MouseEventData mouseEvent)
        {
            try
            {
                if (_networkManager != null)
                {
                    await _networkManager.SendMessageAsync(new Message { Type = MessageType.MouseEvent, Data = mouseEvent });
                }
            }
            catch
            {
            }
        }

        private async void ScreenForm_CtrlAltDelRequested()
        {
            try
            {
                if (_networkManager != null)
                {
                    await _networkManager.SendMessageAsync(new Message { Type = MessageType.SendCtrlAltDel, Data = null });
                }
            }
            catch
            {
            }
        }

        private async void ScreenForm_KeyboardEventOccurred(KeyboardEventData keyEvent)
        {
            try
            {
                if (_networkManager != null)
                {
                    await _networkManager.SendMessageAsync(new Message { Type = MessageType.KeyboardEvent, Data = keyEvent });
                }
            }
            catch
            {
            }
        }

        private async void BtnSendFile_Click(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("請先連線到伺服器。");
                return;
            }

            using var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                await SendFile(openFileDialog.FileName);
            }
        }

        private async Task SendFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var transferInfo = new FileTransferInfo
                {
                    FileName = Path.GetFileName(filePath),
                    FileSize = fileInfo.Length,
                    TargetPath = @"C:\Temp"
                };

                await _networkManager.SendMessageAsync(new Message { Type = MessageType.FileTransferRequest, Data = transferInfo });

                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    var fileData = new byte[bytesRead];
                    Array.Copy(buffer, fileData, bytesRead);
                    await _networkManager.SendMessageAsync(new Message { Type = MessageType.FileTransferData, Data = fileData });
                }

                await _networkManager.SendMessageAsync(new Message { Type = MessageType.FileTransferComplete, Data = null });
                MessageBox.Show("檔案傳送完成。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"檔案傳送失敗: {ex.Message}");
            }
        }

        private async void StartReceiving()
        {
            try
            {
                while (_isConnected && _client != null && _client.Connected)
                {
                    var message = await _networkManager.ReceiveMessageAsync();
                    switch (message.Type)
                    {
                        case MessageType.ScreenCapture:
                            HandleReceivedScreenCapture(message);
                            break;
                        case MessageType.ClipboardData:
                            HandleReceivedClipboardData(message);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isConnected)
                {
                    MessageBox.Show($"連線已中斷: {ex.Message}");
                }

                Disconnect();
            }
        }

        private void HandleReceivedScreenCapture(Message message)
        {
            var screenData = message.Data as ScreenData;
            if (screenData == null || _screenForm == null || _screenForm.IsDisposed)
            {
                return;
            }

            _screenForm.BeginInvoke(new Action(() =>
            {
                _screenForm.UpdateScreen(screenData);
                _screenForm.UpdateRemoteCursor(new Point(screenData.CursorX, screenData.CursorY));
            }));
        }

        private void HandleReceivedClipboardData(Message message)
        {
            if (message.Data is ClipboardContent content && !string.IsNullOrEmpty(content.Text))
            {
                _isUpdatingClipboard = true;
                try
                {
                    Clipboard.SetText(content.Text);
                }
                catch
                {
                }
                finally
                {
                    _isUpdatingClipboard = false;
                }
            }
        }

        private void StartClipboardMonitor()
        {
            if (_clipboardTimer != null)
            {
                return;
            }

            _clipboardTimer = new Timer { Interval = 500 };
            _clipboardTimer.Tick += ClipboardTimer_Tick;
            _clipboardTimer.Start();
        }

        private async void ClipboardTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                uint currentSeq = InputSimulator.GetClipboardSequenceNumber();
                if (currentSeq != _lastClipboardSeq && !_isUpdatingClipboard)
                {
                    _lastClipboardSeq = currentSeq;
                    if (Clipboard.ContainsText())
                    {
                        string text = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(text) && _networkManager != null)
                        {
                            await _networkManager.SendMessageAsync(new Message
                            {
                                Type = MessageType.ClipboardData,
                                Data = new ClipboardContent { Text = text }
                            });
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void StopClipboardMonitor()
        {
            if (_clipboardTimer != null)
            {
                _clipboardTimer.Stop();
                _clipboardTimer.Dispose();
                _clipboardTimer = null;
            }

            _lastClipboardSeq = 0;
            _isUpdatingClipboard = false;
        }

        private void Disconnect()
        {
            StopClipboardMonitor();
            _isConnected = false;

            if (_screenForm != null)
            {
                _screenForm.MouseEventOccurred -= ScreenForm_MouseEventOccurred;
                _screenForm.KeyboardEventOccurred -= ScreenForm_KeyboardEventOccurred;
                _screenForm.CtrlAltDelRequested -= ScreenForm_CtrlAltDelRequested;
                _screenForm.Close();
                _screenForm = null;
            }

            _networkManager?.Dispose();
            _networkManager = null;
            _client?.Close();
            _client = null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveServers();
            Disconnect();
            base.OnFormClosing(e);
        }

        private void LoadSavedServers()
        {
            try
            {
                if (!File.Exists(SavedServersFile))
                {
                    return;
                }

                string json = File.ReadAllText(SavedServersFile);
                var servers = JsonSerializer.Deserialize<List<SavedServer>>(json);
                if (servers == null)
                {
                    return;
                }

                foreach (var server in servers)
                {
                    _serverList.Items.Add($"{server.Name} ({server.IP}:{server.Port})");
                }

                _serverList.Tag = servers;
            }
            catch
            {
            }
        }

        private void SaveServers()
        {
            try
            {
                if (_serverList.Tag is not List<SavedServer> servers)
                {
                    return;
                }

                string json = JsonSerializer.Serialize(servers, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SavedServersFile, json);
            }
            catch
            {
            }
        }

        private void BtnSaveServer_Click(object sender, EventArgs e)
        {
            var txtIP = Controls.Find("txtIP", true)[0] as TextBox;
            var txtPort = Controls.Find("txtPort", true)[0] as TextBox;
            var txtPassword = Controls.Find("txtPassword", true)[0] as TextBox;

            if (string.IsNullOrWhiteSpace(txtIP.Text))
            {
                MessageBox.Show("請輸入伺服器 IP。");
                return;
            }

            var servers = _serverList.Tag as List<SavedServer> ?? new List<SavedServer>();
            var newServer = new SavedServer
            {
                Name = $"Server{servers.Count + 1}",
                IP = txtIP.Text,
                Port = int.Parse(txtPort.Text),
                Password = txtPassword.Text
            };

            servers.Add(newServer);
            _serverList.Tag = servers;
            _serverList.Items.Add($"{newServer.Name} ({newServer.IP}:{newServer.Port})");
            SaveServers();
            MessageBox.Show("伺服器已儲存。");
        }

        private void BtnDeleteServer_Click(object sender, EventArgs e)
        {
            if (_serverList.SelectedIndex < 0)
            {
                MessageBox.Show("請先選擇要刪除的伺服器。");
                return;
            }

            if (_serverList.Tag is not List<SavedServer> servers || _serverList.SelectedIndex >= servers.Count)
            {
                MessageBox.Show("找不到要刪除的伺服器資料。");
                return;
            }

            var selectedIndex = _serverList.SelectedIndex;
            var selectedServer = servers[selectedIndex];
            var confirm = MessageBox.Show(
                $"確定要刪除 {selectedServer.Name} ({selectedServer.IP}:{selectedServer.Port}) 嗎？",
                "刪除伺服器",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            servers.RemoveAt(selectedIndex);
            _serverList.Items.RemoveAt(selectedIndex);
            _serverList.Tag = servers;
            SaveServers();
            MessageBox.Show("伺服器已刪除。");
        }

        private void ServerList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_serverList.SelectedIndex < 0)
            {
                return;
            }

            if (_serverList.Tag is not List<SavedServer> servers || _serverList.SelectedIndex >= servers.Count)
            {
                return;
            }

            var server = servers[_serverList.SelectedIndex];
            var txtIP = Controls.Find("txtIP", true)[0] as TextBox;
            var txtPort = Controls.Find("txtPort", true)[0] as TextBox;
            var txtPassword = Controls.Find("txtPassword", true)[0] as TextBox;

            txtIP.Text = server.IP;
            txtPort.Text = server.Port.ToString();
            txtPassword.Text = server.Password;
        }
    }
}
