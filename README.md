# Windows 远程遥控软件

基于 C#/.NET 8.0 开发的 Windows 远程控制软件，支持桌面查看、鼠标键盘控制和文件传输。

## 功能特性

- **屏幕查看**: 实时查看远程计算机屏幕
- **鼠标控制**: 远程控制鼠标移动、点击、滚动
- **键盘控制**: 远程输入键盘指令
- **文件传输**: 从客户端向服务器发送文件
- **密码验证**: 连接时需要输入密码进行身份验证
- **IP/端口配置**: 可自定义服务器IP地址和端口号

## 项目结构

```
RemoteControl.sln
├── RemoteControl.Common/    # 公共类库
│   ├── MessageTypes.cs    # 消息类型定义
│   ├── NetworkManager.cs  # 网络通信管理
│   ├── ScreenCapture.cs   # 屏幕捕获
│   └── InputSimulator.cs  # 输入模拟
├── RemoteControl.Server/   # 服务器端(被控端)
│   ├── Program.cs
│   └── ServerForm.cs
└── RemoteControl.Client/   # 客户端(控制端)
    ├── Program.cs
    └── ClientForm.cs
```

## 使用说明

### 环境要求

- .NET 8.0 SDK 或更高版本
- Windows 操作系统
- Visual Studio 2022 或支持 .NET 8.0 的 IDE

### 编译运行

1. 使用 Visual Studio 打开 `RemoteControl.sln`
2. 右键解决方案 → 还原 NuGet 包
3. 生成解决方案 (Ctrl+Shift+B)
4. 设置启动项目：
   - 作为服务器运行：设置 `RemoteControl.Server` 为启动项目
   - 作为客户端运行：设置 `RemoteControl.Client` 为启动项目

### 使用步骤

#### 服务器端 (被控端)

1. 启动 `RemoteControl.Server`
2. 查看显示的本机IP地址
3. 设置端口号 (默认: 8888)
4. 设置连接密码 (默认: 123456)
5. 点击"启动服务"按钮

#### 客户端 (控制端)

1. 启动 `RemoteControl.Client`
2. 输入服务器IP地址
3. 输入服务器端口号
4. 输入连接密码
5. 点击"连接"按钮
6. 连接成功后，右侧将显示远程屏幕
7. 使用鼠标点击/拖动远程屏幕进行控制
8. 键盘输入将自动传输到服务器
9. 点击"发送文件"可将文件传输到服务器

## 注意事项

- 确保服务器和客户端在同一网络或可访问的网络环境
- 防火墙可能需要允许程序通过或开放对应端口
- 默认密码建议修改以提高安全性
- 屏幕捕获间隔为100ms，可根据网络状况调整

## 技术栈

- C# / .NET 8.0
- Windows Forms (UI)
- System.Text.Json (序列化)
- TCP Socket (网络通信)
- GDI+ (屏幕捕获)
- Win32 API (输入模拟)

## 后续改进建议

- [ ] 添加图像压缩选项
- [ ] 支持多显示器
- [ ] 添加加密传输
- [ ] 支持反向连接
- [ ] 添加语音传输
- [ ] 支持文件下载
- [ ] 添加连接日志
