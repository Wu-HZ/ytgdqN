using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApplication2.Legado
{
    internal sealed class LegadoWebDavSettingsDialog : Form
    {
        private readonly TextBox urlBox = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox directoryBox = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox accountBox = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox passwordBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        private readonly CheckBox useSystemProxyBox = new CheckBox { Text = "使用系统代理（网络要求时再选）", AutoSize = true };

        public string ServerUrl => urlBox.Text.Trim();
        public string Directory => directoryBox.Text.Trim();
        public string Account => accountBox.Text.Trim();
        public string Password => passwordBox.Text;
        public bool UseSystemProxy => useSystemProxyBox.Checked;

        public LegadoWebDavSettingsDialog(string serverUrl, string directory, string account, string password, bool useSystemProxy)
        {
            Text = "WebDAV 设置";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(460, 265);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            Controls.Add(layout);

            AddRow(layout, 0, "WebDAV 地址", urlBox, serverUrl);
            AddRow(layout, 1, "同步目录", directoryBox, directory);
            AddRow(layout, 2, "账号", accountBox, account);
            AddRow(layout, 3, "密码", passwordBox, password);

            var hint = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = Color.DimGray,
                Text = "密码由当前 Windows 用户加密保存，换电脑/用户需重输；HTTP 传输不加密。"
            };
            layout.Controls.Add(hint, 0, 4);
            layout.SetColumnSpan(hint, 2);
            useSystemProxyBox.Checked = useSystemProxy;
            layout.Controls.Add(useSystemProxyBox, 0, 5);
            layout.SetColumnSpan(useSystemProxyBox, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var save = new Button { Text = "确定", DialogResult = DialogResult.OK, Width = 82 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 82 };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 0, 7);
            layout.SetColumnSpan(buttons, 2);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private static void AddRow(TableLayoutPanel layout, int row, string label, Control input, string value)
        {
            layout.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);
            input.Text = value ?? string.Empty;
            layout.Controls.Add(input, 1, row);
        }
    }
}
