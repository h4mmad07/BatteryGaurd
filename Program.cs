using System;
using System.Threading;
using System.Windows.Forms;

namespace BatteryGuard
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var mutex = new Mutex(initiallyOwned: true, name: "BatteryGuard-SingleInstance", createdNew: out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Battery Guard is already running in the system tray.", "Battery Guard",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.Run(new TrayAppContext());
        }
    }
}
