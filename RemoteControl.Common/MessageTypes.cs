using System;
using System.Text.Json.Serialization;

namespace RemoteControl.Common
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageType
    {
        Connect,
        ConnectResponse,
        Disconnect,
        ScreenCapture,
        MouseEvent,
        KeyboardEvent,
        FileTransferRequest,
        FileTransferResponse,
        FileTransferData,
        FileTransferComplete,
        ClipboardData
    }

    public class Message
    {
        public MessageType Type { get; set; }
        public object Data { get; set; }
    }

    public class ConnectRequest
    {
        public string Password { get; set; }
    }

    public class ConnectResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class ScreenData
    {
        public byte[] ImageData { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ScreenLeft { get; set; }
        public int ScreenTop { get; set; }
        public int CursorX { get; set; }
        public int CursorY { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MouseEventType
    {
        Move,
        LeftDown,
        LeftUp,
        RightDown,
        RightUp,
        MiddleDown,
        MiddleUp,
        Wheel
    }

    public class MouseEventData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public MouseEventType EventType { get; set; }
        public int Button { get; set; }
    }

    public class KeyboardEventData
    {
        public int KeyCode { get; set; }
        public int ScanCode { get; set; }
        public bool IsExtendedKey { get; set; }
        public bool IsKeyDown { get; set; }
    }

    public class FileTransferInfo
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string TargetPath { get; set; }
    }

    public class ClipboardContent
    {
        public string Text { get; set; }
    }
}
