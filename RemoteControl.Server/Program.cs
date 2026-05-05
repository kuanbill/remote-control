using System;
using System.Windows.Forms;

namespace RemoteControl.Server
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool startMinimized = args != null &&
                                  Array.Exists(args, arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ServerForm(startMinimized));
        }
    }
}
