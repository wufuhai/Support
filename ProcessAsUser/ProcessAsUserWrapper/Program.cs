using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProcessAsUserWrapper {
    static class Program {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args){
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var mainForm = new Form1();
            mainForm.Load += (sender, eventArgs) =>{
                var processStartInfo = new ProcessStartInfo(args[0], args.Length == 2 ? args[1] : null){
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                var process = Process.Start(processStartInfo);
                var handle = GetConsoleWindow();
                ShowWindow(handle, SW_HIDE);
                Debug.Assert(process != null, "process != null");
                process.WaitForExit();
                var streamWriter = File.CreateText("processAsuserWrapper.log");
                streamWriter.Write(process.StandardOutput.ReadToEnd());
                streamWriter.Close();
                Application.ExitThread();
            };
            Application.Run(mainForm);
        }

    }
}
