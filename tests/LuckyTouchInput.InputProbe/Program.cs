using System;
using System.Drawing;
using System.Windows.Forms;

namespace Lucky.TouchInput.InputProbe
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var editor = new TextBox();
            editor.AccessibleName = "Input probe editor";
            editor.Dock = DockStyle.Fill;
            editor.Font = new Font("Segoe UI", 18f);
            editor.Multiline = true;

            var form = new Form();
            form.Text = "Lucky Touch Input Probe";
            form.KeyPreview = true;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.Width = 900;
            form.Height = 600;
            form.Controls.Add(editor);
            form.Shown += delegate { editor.Focus(); };
            form.Activated += delegate { editor.Focus(); };
            form.KeyDown += delegate(object sender, KeyEventArgs eventArgs) {
                form.Text = "Lucky Touch Input Probe | KeyDown=" + eventArgs.KeyCode;
            };
            editor.TextChanged += delegate {
                form.Text = "Lucky Touch Input Probe | TextLength=" + editor.TextLength;
            };
            Application.Run(form);
        }
    }
}
