# Remote Control 開發說明

## 專案位置

- 專案根目錄: `E:\Remote control`
- 方案檔: `E:\Remote control\RemoteControl.sln`

## 專案目的

這是一套以 Windows Forms 與 .NET 9 為基礎的遠端遙控工具，分成 Client 與 Server 兩端，功能包含：

- 遠端桌面畫面傳輸
- 滑鼠與鍵盤遠端控制
- 檔案傳送
- Server 端常駐與開機自啟
- Client 端已儲存伺服器清單管理

## 專案結構

### `RemoteControl.Common`

共用模組，提供 Client 與 Server 共同使用的資料結構與底層能力。

- `MessageTypes.cs`
  - 定義通訊訊息種類與資料模型
  - 包含 `ConnectRequest`、`ConnectResponse`、`ScreenData`、`MouseEventData`、`KeyboardEventData`、`FileTransferInfo`
- `NetworkManager.cs`
  - 負責 TCP 訊息封包收送
  - 目前使用「4 byte 長度 + JSON 內容」的自訂訊息格式
  - 已補上完整讀取與送出鎖，避免封包交錯造成 JSON 損毀
- `ScreenCapture.cs`
  - 負責擷取遠端畫面
  - 回傳影像資料與畫面範圍資訊
- `InputSimulator.cs`
  - 透過 Win32 API 模擬滑鼠與鍵盤輸入

### `RemoteControl.Server`

遠端被控端程式。

- `Program.cs`
  - 支援 `--minimized` 參數
  - 開機自啟時會用這個參數直接最小化到系統匣
- `ServerForm.cs`
  - 主視窗與主要控制流程
  - 目前負責：
    - 監聽 TCP 連線
    - 驗證密碼
    - 傳送遠端畫面
    - 接收滑鼠、鍵盤與檔案傳送指令
    - 斷線後自動回到等待連線狀態
    - 開機自啟 UI 與系統匣常駐
    - 設定保存與載入
- `ServerSettings.cs`
  - Server 本機設定模型
  - 設定檔儲存在 `server_settings.json`

### `RemoteControl.Client`

遠端控制端程式。

- `ClientForm.cs`
  - 連線設定畫面
  - 支援伺服器清單儲存、刪除與檔案傳送
- `ScreenForm.cs`
  - 遠端桌面顯示視窗
  - 支援縮放、顯示模式切換、滑鼠定位映射與遠端游標繪製

## 目前已完成的功能

### 1. 遠端桌面與滑鼠座標修正

先前有解析度顯示錯誤與滑鼠偏移問題，已做以下修正：

- Client 與 Server 都啟用高 DPI 模式
- `ScreenData` 增加 `ScreenLeft` 與 `ScreenTop`
- Client 端改用實際影像顯示區域進行滑鼠換算
- Client 端使用 `PictureBoxSizeMode.Zoom` 並重新整理縮放邏輯

### 2. 通訊穩定性修正

先前出現：

- `The input does not contain any JSON tokens`

已在 `NetworkManager.cs` 修正為：

- 發送時加上 `SemaphoreSlim`，避免多個訊息同時寫入同一個 `NetworkStream`
- 接收時用 `ReadExactlyAsync` 確保長度與內容完整讀取
- 收到空白或無效封包時明確拋出例外

### 3. Server 斷線後可重新等待連線

先前 Server 僅接受一次連線，Client 斷線後不會回到等待狀態。現在已改成：

- 持續執行接受連線迴圈
- 每次 Client 斷線後清除舊連線資源
- 自動回到等待下一個 Client 連線
- 不再跳出阻塞式「已斷線」對話框

### 4. Server 設定保存

Server 現在會自動保存：

- 連接埠
- 密碼
- 開機自啟勾選狀態

保存時機：

- `Port` 或 `Password` 欄位離開焦點
- 勾選/取消開機自啟
- 關閉程式時

設定檔位置：

- `Publish\Server\server_settings.json`

### 5. Server 開機自啟與最小化

Server 已支援：

- 透過勾選框控制是否開機自啟
- 自動寫入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- 開機啟動時自動帶 `--minimized`
- 啟動後常駐系統匣

### 6. Client 已儲存伺服器清單管理

Client 已支援：

- 儲存伺服器
- 刪除伺服器
- 點選已儲存伺服器後自動帶入 IP / Port / Password

設定檔位置：

- `Publish\Client\saved_servers.json`

## 發佈方式

目前使用單檔自包含發佈。

### Client

```powershell
dotnet publish "RemoteControl.Client\RemoteControl.Client.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "E:\Remote control\Publish\Client"
```

### Server

```powershell
dotnet publish "RemoteControl.Server\RemoteControl.Server.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "E:\Remote control\Publish\Server"
```

如果 `Publish\Server\RemoteControl.Server.exe` 被占用，發佈可能失敗。這通常代表：

- 舊版 Server 仍在執行
- 或輸出檔正在被其他程式占用

遇到這種情況時，可以：

- 先關閉正在執行的 `RemoteControl.Server.exe`
- 或先發佈到新的資料夾，再做替換

## 目前輸出位置

- Client 發佈資料夾: `E:\Remote control\Publish\Client`
- Server 發佈資料夾: `E:\Remote control\Publish\Server`

## 已知問題

### 1. 部分原始碼與舊 README 存在亂碼

目前專案內仍有部分檔案文字顯示亂碼，尤其是：

- `README.md`
- `ClientForm.cs`
- `ScreenForm.cs`
- 某些 `MessageBox` 文字

這代表專案有過編碼混亂或檔案內容未完整清理。功能已持續修補，但文字層仍建議後續統一整理為 UTF-8 繁體中文。

### 2. UI 文案仍未全面清理

雖然 Server 端主要控制流程已重寫為可維護版本，但 Client 端和部分畫面上的字串仍可再完整整理一次，避免後續維護時再混入舊字串。

### 3. 單用戶端模式

目前 Server 設計是單一 Client 連線模式，不是多用戶端同時控制架構。

## 建議下一步

### 優先建議

1. 全面清理 Client 與 ScreenForm 的 UI 文字亂碼
2. 重寫根目錄 `README.md` 為正常繁體版本
3. 把 Server 狀態、錯誤、重連記錄寫入日誌檔

### 進階功能

1. 多螢幕切換支援
2. 滑鼠滾輪與拖曳體驗補強
3. Client 端自動重連
4. 密碼雜湊或更安全的認證方式
5. 畫面壓縮品質與傳輸效能調校

## 驗證重點

每次修改後建議至少驗證：

1. Server 啟動後是否進入等待連線
2. Client 連線成功後是否可看到遠端畫面
3. 滑鼠與鍵盤控制是否準確
4. Client 關閉後 Server 是否自動回到等待連線
5. Server 重開後 Port / Password / AutoStart 是否正確載入
6. 發佈後的 `exe` 是否可獨立執行

## 備註

這份文件是根據目前 `E:\Remote control` 內的實作狀態整理，適合作為後續續開發、交接與除錯的基礎說明。
## 2026-05-05 鍵盤控制補強

- Client `ScreenForm.cs`
  - 攔截 `WM_SYSKEYDOWN` 與 `WM_SYSKEYUP`，讓 `Alt`、`F10` 這類系統鍵不會被 WinForms 吃掉。
  - 追蹤目前按住的按鍵，視窗失焦或關閉時會主動補送對應的 KeyUp，避免遠端端卡住修飾鍵。
- Common `InputSimulator.cs`
  - 鍵盤模擬優先使用 scan code 搭配 `SendInput`，並保留 extended-key 標記，讓導覽鍵與組合鍵更接近實體鍵盤行為。

建議回歸測試：
1. 一般英數輸入
2. `Shift` 加數字列符號，例如 `!`、`@`、`#`
3. `Ctrl + C`、`Ctrl + V`、`Ctrl + A`
4. `Alt + Tab`、`Alt + F4`、`Esc`
5. 方向鍵、`Home/End`、`PageUp/PageDown`
6. 按住修飾鍵後切回本機，再回到遠端，確認不會出現卡鍵
