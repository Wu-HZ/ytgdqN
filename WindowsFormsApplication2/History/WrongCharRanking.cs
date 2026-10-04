using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using WindowsFormsApplication2.Storage;

namespace WindowsFormsApplication2.History
{
    public sealed class WrongCharRanking : Form
    {
        private readonly ScoreData storage;
        private readonly string selectedScoreTime;
        private string activeScoreTime;
        private readonly DateTimePicker startPicker = new DateTimePicker();
        private readonly DateTimePicker endPicker = new DateTimePicker();
        private readonly DataGridView rankingGrid = new DataGridView();
        private readonly DataGridView detailGrid = new DataGridView();
        private readonly Label detailTitle = new Label();

        public WrongCharRanking(ScoreData scoreData, string scoreTime = null)
        {
            storage = scoreData ?? throw new ArgumentNullException(nameof(scoreData));
            selectedScoreTime = scoreTime;
            Text = string.IsNullOrEmpty(scoreTime) ? "错字排行" : "逐字跟打详情与错字排行";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 460);
            Size = new Size(900, 620);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8, 7, 4, 4), WrapContents = false };
            top.Controls.Add(new Label { Text = "开始日期", AutoSize = true, Margin = new Padding(3, 6, 3, 0) });
            startPicker.Width = 125;
            startPicker.Format = DateTimePickerFormat.Short;
            top.Controls.Add(startPicker);
            top.Controls.Add(new Label { Text = "结束日期", AutoSize = true, Margin = new Padding(10, 6, 3, 0) });
            endPicker.Width = 125;
            endPicker.Format = DateTimePickerFormat.Short;
            top.Controls.Add(endPicker);
            var refresh = new Button { Text = "统计", AutoSize = true, Height = 27 };
            refresh.Click += (s, e) => RefreshRanking();
            top.Controls.Add(refresh);
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Size = new Size(880, 540) };
            split.SplitterDistance = 245;
            ConfigureGrid(rankingGrid);
            ConfigureGrid(detailGrid);
            rankingGrid.Columns.Add("character", "错字");
            rankingGrid.Columns.Add("count", "出现次数");
            rankingGrid.Columns.Add("source", "涉及段数");
            detailTitle.Text = "逐字用时（选择错字可查看它出现在哪些位置）";
            detailTitle.Dock = DockStyle.Top;
            detailTitle.Height = 26;
            detailTitle.Padding = new Padding(4, 5, 0, 0);
            split.Panel1.Controls.Add(rankingGrid);
            split.Panel2.Controls.Add(detailGrid);
            split.Panel2.Controls.Add(detailTitle);
            Controls.Add(split);
            Controls.Add(top);
            rankingGrid.SelectionChanged += RankingGrid_SelectionChanged;

            endPicker.Value = DateTime.Today;
            startPicker.Value = DateTime.Today.AddDays(-6);
            if (!string.IsNullOrEmpty(selectedScoreTime) && DateTime.TryParse(selectedScoreTime, out DateTime scoreDate))
            {
                startPicker.Value = scoreDate.Date;
                endPicker.Value = scoreDate.Date;
            }
            RefreshRanking();
        }

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
        }

        private void RefreshRanking()
        {
            var records = storage.GetTypingDetails(startPicker.Value.Date, endPicker.Value.Date);
            var events = new List<(string Character, int Index, string ScoreTime)>();
            foreach (TypingDetailRecord record in records)
            {
                try
                {
                    var mistakes = JsonConvert.DeserializeObject<List<WrongCharEvent>>(record.WrongCharsJson) ?? new List<WrongCharEvent>();
                    foreach (WrongCharEvent mistake in mistakes)
                    {
                        if (!string.IsNullOrEmpty(mistake.Character))
                            events.Add((mistake.Character, mistake.Index, record.ScoreTime));
                    }
                }
                catch { }
            }

            rankingGrid.Rows.Clear();
            foreach (var item in events.GroupBy(e => e.Character)
                         .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
            {
                int row = rankingGrid.Rows.Add(item.Key, item.Count(), item.Select(e => e.ScoreTime).Distinct().Count());
                rankingGrid.Rows[row].Tag = item.Key;
            }

            string selectedTime = selectedScoreTime;
            if (string.IsNullOrEmpty(selectedTime))
            {
                string visibleTime = detailGrid.Tag as string;
                if (visibleTime != null && records.Any(r => NormalizeTime(r.ScoreTime) == NormalizeTime(visibleTime)))
                    selectedTime = visibleTime;
                else if (records.Count > 0)
                    selectedTime = records[0].ScoreTime;
            }
            activeScoreTime = selectedTime;
            LoadSegmentDetail(activeScoreTime, null);
        }

        private static string NormalizeTime(string value) => (value ?? "").Replace("T", " ").Trim();

        private void RankingGrid_SelectionChanged(object sender, EventArgs e)
        {
            if (rankingGrid.CurrentRow == null) return;
            string character = rankingGrid.CurrentRow.Tag as string;
            LoadSegmentDetail(activeScoreTime, character);
        }

        private void LoadSegmentDetail(string scoreTime, string filterCharacter)
        {
            detailGrid.Columns.Clear();
            detailGrid.Columns.Add("time", "字符位置");
            detailGrid.Columns.Add("target", "目标字");
            detailGrid.Columns.Add("typed", "最终输入");
            detailGrid.Columns.Add("elapsed", "估算用时(秒)");
            detailGrid.Columns.Add("wrong", "错字次数");
            detailGrid.Tag = scoreTime;
            if (string.IsNullOrEmpty(scoreTime))
            {
                detailTitle.Text = "逐字用时（所选日期范围没有逐字记录）";
                return;
            }

            TypingDetailRecord record = storage.GetTypingDetail(scoreTime);
            if (record == null)
            {
                detailTitle.Text = "逐字详情：该段没有逐字数据（可能是旧版本记录）";
                return;
            }
            detailTitle.Text = "逐字详情：" + NormalizeTime(record.ScoreTime);

            var elapsedByIndex = new Dictionary<int, double>();
            try
            {
                var reports = JsonConvert.DeserializeObject<List<TypeDate>>(record.TypeReportJson) ?? new List<TypeDate>();
                foreach (TypeDate part in reports.Where(p => p.Length > 0))
                {
                    int count = Math.Min(part.Length, Math.Max(0, record.TargetText.Length - part.Start));
                    if (count <= 0) continue;
                    double each = part.TotalTime / count;
                    for (int i = Math.Max(0, part.Start); i < part.Start + count; i++)
                        elapsedByIndex[i] = elapsedByIndex.TryGetValue(i, out double old) ? old + each : each;
                }
            }
            catch { }

            List<WrongCharEvent> mistakes;
            try { mistakes = JsonConvert.DeserializeObject<List<WrongCharEvent>>(record.WrongCharsJson) ?? new List<WrongCharEvent>(); }
            catch { mistakes = new List<WrongCharEvent>(); }
            var mistakesByIndex = mistakes.GroupBy(m => m.Index).ToDictionary(g => g.Key, g => g.Count());
            int length = Math.Max(record.TargetText?.Length ?? 0, record.TypedText?.Length ?? 0);
            for (int i = 0; i < length; i++)
            {
                string target = i < (record.TargetText?.Length ?? 0) ? record.TargetText.Substring(i, 1) : "";
                string typed = i < (record.TypedText?.Length ?? 0) ? record.TypedText.Substring(i, 1) : "";
                int wrong = mistakesByIndex.TryGetValue(i, out int count) ? count : 0;
                if (filterCharacter != null && target != filterCharacter && wrong == 0) continue;
                int row = detailGrid.Rows.Add(i + 1, target, typed,
                    elapsedByIndex.TryGetValue(i, out double seconds) ? seconds.ToString("0.000", CultureInfo.InvariantCulture) : "—", wrong);
                if (wrong > 0 || target != typed)
                    detailGrid.Rows[row].DefaultCellStyle.BackColor = Color.MistyRose;
            }
        }
    }
}
