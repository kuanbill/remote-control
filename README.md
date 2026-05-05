# Windows 遠端遙控軟體

這是一套以 C# / .NET 9 開發的 Windows 遠端控制工具，支援桌面檢視、滑鼠鍵盤控制與檔案傳輸。

## 功能特色

- **畫面檢視**：即時查看遠端電腦畫面
- **滑鼠控制**：遠端控制滑鼠移動、點擊與滾動
- **鍵盤控制**：將本機按鍵操作傳送到遠端主機
- **檔案傳輸**：由 Client 向 Server 傳送檔案
- **密碼驗證**：連線時需輸入密碼進行身分驗證
- **IP / 連接埠設定**：可自訂伺服器 IP 與連接埠

## 專案結構

```text
RemoteControl.sln
├── RemoteControl.Common/    # 共用類別庫
│   ├── MessageTypes.cs      # 訊息型別與資料模型
│   ├── NetworkManager.cs    # 網路通訊管理
│   ├── ScreenCapture.cs     # 畫面擷取
│   └── InputSimulator.cs    # 輸入模擬
├── RemoteControl.Server/    # 伺服器端（被控端）
│   ├── Program.cs
│   ├── ServerForm.cs
│   └── ServerSettings.cs
├── RemoteControl.Client/    # 用戶端（控制端）
│   ├── ClientForm.cs
│   └── ScreenForm.cs
├── Publish/
│   ├── Client/
│   └── Server/
└── ai-output/
    └── development-guide.md
```

## 環境需求

- Windows 作業系統
- .NET 9 SDK 或更高版本
- Visual Studio 2022，或任何支援 .NET 9 的 IDE

## 開發與執行

1. 以 Visual Studio 開啟 `RemoteControl.sln`
2. 還原相依套件
3. 建置方案
4. 依用途選擇啟動專案：
   - `RemoteControl.Server`：被控端
   - `RemoteControl.Client`：控制端

也可以直接使用命令列建置：

```powershell
dotnet build "E:\Remote control\RemoteControl.sln"
```

## 基本使用方式

### 伺服器端

1. 啟動 `RemoteControl.Server`
2. 確認畫面顯示的本機 IP
3. 設定連接埠與密碼
4. 保持程式在等待連線狀態

### 用戶端

1. 啟動 `RemoteControl.Client`
2. 輸入伺服器 IP、連接埠與密碼
3. 點擊「連線」
4. 連線成功後會開啟遠端桌面畫面
5. 可直接以滑鼠、鍵盤操作遠端主機
6. 需要時可透過「傳送檔案」將檔案送到遠端

## 發佈

目前專案使用單檔、自包含方式發佈。

### Client

```powershell
dotnet publish "RemoteControl.Client\RemoteControl.Client.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "E:\Remote control\Publish\Client"
```

### Server

```powershell
dotnet publish "RemoteControl.Server\RemoteControl.Server.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "E:\Remote control\Publish\Server"
```

## 注意事項

- 建議修改預設密碼以提高安全性
- 伺服器與用戶端需位於可互通的網路環境
- 若遭防火牆阻擋，需允許程式通訊或開放對應連接埠
- 若 `Publish\Server\RemoteControl.Server.exe` 被占用，發佈前請先關閉舊版 Server

## 補充文件

- 專案維護與交接說明請見 [ai-output/development-guide.md](E:\Remote control\ai-output\development-guide.md)

## 後續可改進方向

- 多螢幕切換
- 更完整的鍵盤與輸入法支援
- 加密傳輸
- 自動重連
- 傳輸與畫面壓縮效能優化
- 操作與錯誤日誌
