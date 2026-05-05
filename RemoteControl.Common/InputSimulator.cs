using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RemoteControl.Common
{
    public static class InputSimulator
    {
        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

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
        const uint INPUT_KEYBOARD = 1;

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
            public KEYBDINPUT ki;
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
            SetCursorPos(mouseEvent.X, mouseEvent.Y);
            switch (mouseEvent.EventType)
            {
                case MouseEventType.LeftDown:
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    break;
                case MouseEventType.LeftUp:
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    break;
                case MouseEventType.RightDown:
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                    break;
                case MouseEventType.RightUp:
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                    break;
                case MouseEventType.MiddleDown:
                    mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                    break;
                case MouseEventType.MiddleUp:
                    mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                    break;
                case MouseEventType.Wheel:
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)mouseEvent.Button, 0);
                    break;
            }
        }

        public static void SimulateKeyboardEvent(KeyboardEventData keyEvent)
        {
            Keys key = (Keys)keyEvent.KeyCode;
            uint flags = keyEvent.IsKeyDown ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP;
            if (IsExtendedKey(key))
            {
                flags |= KEYEVENTF_EXTENDEDKEY;
            }

            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)keyEvent.KeyCode,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            uint sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
            if (sent == 0)
            {
                if (keyEvent.IsKeyDown)
                {
                    keybd_event((byte)keyEvent.KeyCode, 0, flags, 0);
                }
                else
                {
                    keybd_event((byte)keyEvent.KeyCode, 0, flags, 0);
                }
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
