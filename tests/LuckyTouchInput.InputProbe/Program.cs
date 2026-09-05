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
            editor.ScrollBars = ScrollBars.Vertical;

            var form = new Form();
            form.Text = "Lucky Touch Input Probe";
            form.KeyPreview = true;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.Width = 900;
            form.Height = 600;
            form.Controls.Add(editor);
            form.Shown += delegate { editor.Focus(); };
            form.Activated += delegate { editor.Focus(); };
            int enterCount = 0;
            form.KeyDown += delegate(object sender, KeyEventArgs eventArgs) {
                if (eventArgs.KeyCode == Keys.Enter) enterCount++;
                form.Text = "Lucky Touch Input Probe | KeyDown=" + eventArgs.KeyCode + " | EnterCount=" + enterCount;
            };
            editor.TextChanged += delegate {
                form.Text = "Lucky Touch Input Probe | TextLength=" + editor.TextLength + " | EnterCount=" + enterCount;
            };
            editor.MouseWheel += delegate(object sender, MouseEventArgs eventArgs) {
                form.Text = "Lucky Touch Input Probe | Wheel=" + eventArgs.Delta;
            };
            Application.Run(form);
        }
    }
}
