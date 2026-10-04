using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace WindowsFormsApplication2.检查更新
{
    public sealed class UpdateLogForm : Form
    {
        private readonly ListBox versionList = new ListBox();
        private readonly RichTextBox contentBox = new RichTextBox();
        private readonly List<VersionObject> versions;

        public UpdateLogForm()
        {
            Text = "更新日志";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(600, 380);
            Size = new Size(760, 520);
            ShowIcon = false;

            var layout = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Size = new Size(740, 480),
                FixedPanel = FixedPanel.Panel1
            };
            layout.SplitterDistance = 190;
            versionList.Dock = DockStyle.Fill;
            versionList.IntegralHeight = false;
            versionList.SelectedIndexChanged += VersionList_SelectedIndexChanged;
            contentBox.Dock = DockStyle.Fill;
            contentBox.ReadOnly = true;
            contentBox.BorderStyle = BorderStyle.None;
            contentBox.Font = new Font("微软雅黑", 10f);
            contentBox.BackColor = SystemColors.Window;
            layout.Panel1.Controls.Add(versionList);
            layout.Panel2.Padding = new Padding(12);
            layout.Panel2.Controls.Add(contentBox);
            Controls.Add(layout);

            versions = LoadVersions();
            foreach (VersionObject version in versions)
            {
                versionList.Items.Add(string.IsNullOrWhiteSpace(version.Date)
                    ? version.Version
                    : $"{version.Date}  v{version.Version}");
            }
            if (versionList.Items.Count > 0)
                versionList.SelectedIndex = 0;
            else
                contentBox.Text = "没有可显示的更新日志。";
        }

        private static List<VersionObject> LoadVersions()
        {
            string path = Path.Combine(Application.StartupPath, "updates.json");
            if (!File.Exists(path))
                return new List<VersionObject>();

            try
            {
                var items = JsonConvert.DeserializeObject<List<VersionObject>>(File.ReadAllText(path));
                return (items ?? new List<VersionObject>())
                    .OrderByDescending(v => ParseDate(v.Date))
                    .ThenByDescending(v => v.Version, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<VersionObject>();
            }
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime date)
                ? date : DateTime.MinValue;
        }

        private void VersionList_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = versionList.SelectedIndex;
            if (index < 0 || index >= versions.Count)
                return;

            VersionObject version = versions[index];
            var text = new System.Text.StringBuilder();
            text.AppendLine($"雨天跟打器 {version.Version}");
            if (!string.IsNullOrWhiteSpace(version.Date)) text.AppendLine($"发布日期：{version.Date}");
            text.AppendLine();
            if (!string.IsNullOrWhiteSpace(version.Instra)) text.AppendLine($"更新说明：\r\n{version.Instra}\r\n");
            text.AppendLine($"更新内容：\r\n{version.Content ?? ""}");
            if (!string.IsNullOrWhiteSpace(version.Other)) text.AppendLine($"\r\n其它信息：\r\n{version.Other}");
            contentBox.Text = text.ToString();
            contentBox.SelectionStart = 0;
            contentBox.ScrollToCaret();
        }
    }
}
