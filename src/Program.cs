using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace HOPE5000TitleFix
{
    internal static class Program
    {
        private const string DnlibResourceName = "dnlib.dll";

        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;

            if (args != null && args.Length == 1 &&
                string.Equals(args[0], "--smoke-test", StringComparison.OrdinalIgnoreCase))
            {
                RunSmokeTest();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            string initialTarget = args != null && args.Length > 0 ? args[0] : null;
            RunApplication(initialTarget);
        }

        private static void RunApplication(string initialTarget)
        {
            Application.Run(new MainForm(initialTarget));
        }

        private static void RunSmokeTest()
        {
            try
            {
                string dnlibName = typeof(dnlib.DotNet.ModuleDefMD).Assembly.GetName().Name;
                if (!string.Equals(dnlibName, "dnlib", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Embedded dnlib could not be loaded.");
                }
                Patcher.ValidateTitle("HOPE5000");
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex);
                Environment.ExitCode = 1;
            }
        }

        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            AssemblyName requested;
            try
            {
                requested = new AssemblyName(args.Name);
            }
            catch
            {
                return null;
            }

            if (!string.Equals(requested.Name, "dnlib", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            using (Stream stream = typeof(Program).Assembly.GetManifestResourceStream(DnlibResourceName))
            {
                if (stream == null)
                {
                    return null;
                }

                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return Assembly.Load(memory.ToArray());
                }
            }
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            HandleUnexpectedError(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                WriteCrashLog(exception);
            }
        }

        private static void HandleUnexpectedError(Exception exception)
        {
            string logPath = WriteCrashLog(exception);
            MessageBox.Show(
                "工具遇到未处理的错误。\r\n\r\n" + exception.Message +
                (logPath == null ? string.Empty : "\r\n\r\n错误日志：" + logPath),
                "HOPE5000 标题热修复",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static string WriteCrashLog(Exception exception)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "HOPE5000TitleFix-crash.log");
                StringBuilder text = new StringBuilder();
                text.AppendLine("HOPE5000TitleFix 1.0.0");
                text.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                text.AppendLine();
                text.AppendLine(exception.ToString());
                File.AppendAllText(path, text.ToString(), Encoding.UTF8);
                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
