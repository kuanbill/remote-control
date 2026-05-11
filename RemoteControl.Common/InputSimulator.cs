using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RemoteControl.Common
{
    public static class InputSimulator
    {
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();

        [DllImport("sas.dll", SetLastError = true)]
        private static extern int SendSAS(bool asUser);

        const uint INPUT_MOUSE = 0;
        const uint MOUSEEVENTF_MOVE = 0x0001;
        const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        const uint MOUSEEVENTF_LEFTUP = 0x04;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        const uint MOUSEEVENTF_RIGHTUP = 0x10;
        const uint MOUSEEVENTF_MIDDLEDOWN = 0x20;
        const uint MOUSEEVENTF_MIDDLEUP = 0x40;
        const uint MOUSEEVENTF_WHEEL = 0x0800;
        const uint KEYEVENTF_KEYDOWN = 0x0000;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        const uint KEYEVENTF_SCANCODE = 0x0008;
        const uint INPUT_KEYBOARD = 1;
        const uint MAPVK_VK_TO_VSC = 0;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public static void SimulateMouseEvent(MouseEventData mouseEvent)
        {
            uint flags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE;
            uint mouseData = 0;

            switch (mouseEvent.EventType)
            {
                case MouseEventType.Move:
                    break;
                case MouseEventType.LeftDown:
                    flags |= MOUSEEVENTF_LEFTDOWN;
                    break;
                case MouseEventType.LeftUp:
                    flags |= MOUSEEVENTF_LEFTUP;
                    break;
                case MouseEventType.RightDown:
                    flags |= MOUSEEVENTF_RIGHTDOWN;
                    break;
                case MouseEventType.RightUp:
                    flags |= MOUSEEVENTF_RIGHTUP;
                    break;
                case MouseEventType.MiddleDown:
                    flags |= MOUSEEVENTF_MIDDLEDOWN;
                    break;
                case MouseEventType.MiddleUp:
                    flags |= MOUSEEVENTF_MIDDLEUP;
                    break;
                case MouseEventType.Wheel:
                    flags |= MOUSEEVENTF_WHEEL;
                    mouseData = (uint)mouseEvent.Button;
                    break;
            }

            int screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
            int screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
            int absX = (mouseEvent.X * 65535) / screenWidth;
            int absY = (mouseEvent.Y * 65535) / screenHeight;

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = absX,
                        dy = absY,
                        mouseData = mouseData,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public static string SimulateKeyboardEvent(KeyboardEventData keyEvent)
        {
            Keys key = (Keys)keyEvent.KeyCode;
            uint flags = keyEvent.IsKeyDown ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP;
            uint scanCode = keyEvent.ScanCode > 0
                ? (uint)keyEvent.ScanCode
                : MapVirtualKey((uint)keyEvent.KeyCode, MAPVK_VK_TO_VSC);
            bool useScanCode = scanCode != 0;

            if (keyEvent.IsExtendedKey || IsExtendedKey(key))
            {
                flags |= KEYEVENTF_EXTENDEDKEY;
            }

            if (useScanCode)
            {
                flags |= KEYEVENTF_SCANCODE;
            }

            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = useScanCode ? (ushort)0 : (ushort)keyEvent.KeyCode,
                        wScan = (ushort)scanCode,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            uint sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
            if (sent > 0)
            {
                return "SendInput 成功";
            }

            int error = Marshal.GetLastWin32Error();
            uint fallbackFlags = keyEvent.IsKeyDown ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP;
            if (keyEvent.IsExtendedKey || IsExtendedKey(key))
            {
                fallbackFlags |= KEYEVENTF_EXTENDEDKEY;
            }

            keybd_event((byte)keyEvent.KeyCode, (byte)(scanCode & 0xFF), fallbackFlags, 0);
            return $"SendInput 失敗 {error}，已嘗試 keybd_event";
        }

        public static bool SendCtrlAltDel()
        {
            try
            {
                int result = SendSAS(true);
                return result != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsExtendedKey(Keys key)
        {
            return key == Keys.RMenu ||
                   key == Keys.RControlKey ||
                   key == Keys.Insert ||
                   key == Keys.Delete ||
                   key == Keys.Home ||
                   key == Keys.End ||
                   key == Keys.PageUp ||
                   key == Keys.PageDown ||
                   key == Keys.Up ||
                   key == Keys.Down ||
                   key == Keys.Left ||
                   key == Keys.Right ||
                   key == Keys.NumLock ||
                   key == Keys.Cancel ||
                   key == Keys.PrintScreen ||
                   key == Keys.Divide;
        }
    }
}
