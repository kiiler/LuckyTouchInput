using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Lucky.TouchInput
{
    internal static class Diagnostics
    {
        private const long MaximumLogBytes = 256 * 1024;

        internal static void Report(string operation, Exception exception)
        {
            try
            {
                string directory = Path.Combine(UserPreferences.ConfigDirectory, "logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "lucky-touch-input.log");
                if (File.Exists(path) && new FileInfo(path).Length > MaximumLogBytes)
                    File.Delete(path);
                string line = DateTimeOffset.Now.ToString("O") + " [" + operation + "] " +
                    exception.GetType().Name + ": " + exception.Message + Environment.NewLine;
                File.AppendAllText(path, line, new UTF8Encoding(false));
                Debug.WriteLine(line);
            }
            catch
            {
                // Diagnostics must never prevent the keyboard from running.
            }
        }
    }
}
