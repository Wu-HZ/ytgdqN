using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using WindowsFormsApplication2.Storage;
using WindowsFormsApplication2.Category;

namespace WindowsFormsApplication2.History
{
    public partial class History : Form
    {
        /// <summary>
        /// 当前成绩数据
        /// </summary>
        private StorageDataSet.ScoreDataTable currentScoreData = new StorageDataSet.ScoreDataTable();

        /// <summary>
        /// 当前筛选结果对应的图表数据
        /// </summary>
        private StorageDataSet.ScoreDataTable chartScoreData = new StorageDataSet.ScoreDataTable();

        /// <summary>
        /// 表格操作器
        /// </summary>
        private HistoryDataGridHandler gridHandler;

        private readonly Form1 frm;

        /// <summary>
        /// 类别标识
        /// 用于翻页操作中
        /// </summary>
        private DataType dataType = new DataType();

        /// <summary>
        /// 每页数据量
        /// </summary>
        private readonly int PageSize = 30;

        /// <summary>
        /// 当前页
        /// </summary>
        private int currentPage = 0;

        /// <summary>
        /// 是否展示长期趋势图
        /// </summary>
        private bool showTrendChart = true;

        /// <summary>
        /// 总数据量
        /// </summary>
        private int totalCount = 0;

        private bool suppressArticleListEvent = false;

        private bool isSessionView = true;

        private string currentSessionId = null;

        private string currentSessionTitle = null;

        private Button backButton;

        /// <summary>
        /// 总页数
        /// </summary>
        private int TotalPage { 
            get
            {
                int pages = (int)Math.Ceiling((float)this.totalCount / this.PageSize);
                return Math.Max(1, pages);
            } 
        }

        private Panel chartOverlay;

        public History(Form1 frm1)
        {
            this.frm = frm1;
            InitializeComponent();
            this.MonthCalendar.HigherViewRangeSelected += MonthCalendar_HigherViewRangeSelected;
            this.WindowState = FormWindowState.Maximized;
            this.paginationPanel.Resize += (sender, e) => UpdatePaginationLayout();
            this.leftPanel.Resize += (sender, e) => UpdateLeftPanelLayout();
            this.SpeedChart.MouseDoubleClick += SpeedChart_MouseDoubleClick;

            this.chartOverlay = new Panel();
            this.chartOverlay.Dock = DockStyle.Fill;
            this.chartOverlay.BackColor = Color.FromArgb(30, 30, 30);
            this.chartOverlay.Visible = false;
            this.outerSplitContainer.Panel2.Controls.Add(this.chartOverlay);

            this.backButton = new Button();
            this.backButton.Text = "< 返回发文列表";
            this.backButton.Visible = false;
            this.backButton.FlatStyle = FlatStyle.Flat;
            this.backButton.ForeColor = Color.White;
            this.backButton.BackColor = Color.FromArgb(67, 80, 86);
            this.backButton.FlatAppearance.BorderSize = 0;
            this.backButton.Height = 25;
            this.backButton.Width = 120;
            this.backButton.Click += BackButton_Click;
            this.filterInfoPanel.Controls.Add(this.backButton);
        }

        private void UpdateLeftPanelLayout()
        {
            if (!this.IsHandleCreated || this.leftPanel.ClientSize.Width <= 0) return;

            this.MonthCalendar.Location = new Point(
                (this.leftPanel.ClientSize.Width - this.MonthCalendar.Width) / 2,
                this.MonthCalendar.Location.Y);

            int labelTop = this.MonthCalendar.Bottom + 5;
            this.articleListLabel.Location = new Point(5, labelTop);

            int listTop = this.articleListLabel.Bottom + 3;
            this.articleListBox.SetBounds(5, listTop,
                this.leftPanel.ClientSize.Width - 10,
                this.leftPanel.ClientSize.Height - listTop);
        }

        private void History_Load(object sender, EventArgs e)
        {
            this.gridHandler = new HistoryDataGridHandler(this.dataGridView1);
            this.MonthCalendar.BoldedDates = Glob.ScoreHistory.GetAllScoreDates();
            this.showTrendChart = this.TrendChartCheckBox.Checked;
            this.dataType.Date = DateTime.Now;
            this.dataType.EndDate = DateTime.Now;
            this.LoadArticleList();
            this.RefreshData();
            this.dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;
            this.BeginInvoke((MethodInvoker)(() =>
            {
                if (this.outerSplitContainer.Width > 100)
                {
                    this.outerSplitContainer.SplitterDistance = (int)(this.outerSplitContainer.Width * 0.17);
                }
                if (this.innerSplitContainer.Width > 100)
                {
                    this.innerSplitContainer.SplitterDistance = (int)(this.innerSplitContainer.Width * 0.73);
                }
                if (this.rightSplitContainer.Height > 100)
                {
                    this.rightSplitContainer.SplitterDistance = (int)(this.rightSplitContainer.Height * 0.4);
                }
                this.UpdatePaginationLayout();
                this.UpdateLeftPanelLayout();
            }));
        }

        private void LoadArticleList()
        {
            this.suppressArticleListEvent = true;
            string previousSelection = this.articleListBox.SelectedItem as string;
            this.articleListBox.Items.Clear();
            this.articleListBox.Items.Add("(全部文章)");
            List<string> titles = Glob.ScoreHistory.GetDistinctArticleTitles();
            foreach (string title in titles)
            {
                this.articleListBox.Items.Add(title);
            }

            if (previousSelection != null && this.articleListBox.Items.Contains(previousSelection))
            {
                this.articleListBox.SelectedItem = previousSelection;
            }
            else
            {
                this.articleListBox.SelectedIndex = 0;
            }
            this.suppressArticleListEvent = false;
        }

        private void UpdatePaginationLayout()
        {
            const int buttonHeight = 23;
            const int spacing = 4;
            Control[] controls = { this.FirstPageButton, this.PrePageButton, this.PageNumTextBox, this.TotalPageNumLabel, this.JumpPageButton, this.NextPageButton, this.LastPageButton };

            int totalWidth = spacing * (controls.Length - 1);
            foreach (Control c in controls)
            {
                totalWidth += c.Width;
            }

            int startX = (this.paginationPanel.ClientSize.Width - totalWidth) / 2;
            int centerY = (this.paginationPanel.ClientSize.Height - buttonHeight) / 2;
            int x = startX;

            foreach (Control c in controls)
            {
                c.Location = new Point(x, centerY);
                x += c.Width + spacing;
            }
        }

        private void ArticleListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.suppressArticleListEvent)
            {
                return;
            }

            if (this.articleListBox.SelectedIndex <= 0 || this.articleListBox.SelectedItem.ToString() == "(全部文章)")
            {
                this.dataType.Title = null;
            }
            else
            {
                this.dataType.Title = this.articleListBox.SelectedItem.ToString();
            }

            if (!this.isSessionView)
            {
                this.isSessionView = true;
                this.currentSessionId = null;
                this.backButton.Visible = false;
            }
            this.RefreshData();
        }

        private void RefreshData()
        {
            this.ClearGridData();

            if (this.dataType.Date > this.dataType.EndDate)
            {
                DateTime temp = this.dataType.Date;
                this.dataType.Date = this.dataType.EndDate;
                this.dataType.EndDate = temp;
            }

            bool isSingleDate = this.dataType.Date == this.dataType.EndDate;
            string label = isSingleDate
                ? "日期：" + this.dataType.Date.ToString("d")
                : "日期：" + this.dataType.Date.ToString("d") + " - " + this.dataType.EndDate.ToString("d");

            if (!string.IsNullOrEmpty(this.dataType.Title))
            {
                label += " | 标题：" + this.dataType.Title;
            }
            if (this.dataType.SegmentId.HasValue)
            {
                label += " | 文段ID：" + this.dataType.SegmentId.Value.ToString();
            }
            this.ResultLabel.Text = label;

            if (this.isSessionView)
            {
                this.totalCount = Glob.ScoreHistory.GetSessionCount(
                    this.dataType.Date, this.dataType.EndDate, this.dataType.Title, this.dataType.SegmentId);

                this.currentPage = 1;

                this.UpdateGridToolBar();

                if (this.totalCount > 0)
                {
                    this.currentScoreData = Glob.ScoreHistory.GetSessionScores(
                        this.dataType.Date, this.dataType.EndDate, this.dataType.Title, this.dataType.SegmentId, 0, PageSize);
                    this.ReloadChartScoreData();
                }

                this.ShowSessionList();
            }
            else
            {
                this.totalCount = Glob.ScoreHistory.GetScoreCountFiltered(
                    this.dataType.Date, this.dataType.EndDate, this.dataType.Title, this.dataType.SegmentId);

                this.currentPage = 1;

                this.UpdateGridToolBar();

                if (this.totalCount > 0)
                {
                    this.currentScoreData = Glob.ScoreHistory.GetScoresFiltered(
                        this.dataType.Date, this.dataType.EndDate, this.dataType.Title, this.dataType.SegmentId, 0, PageSize);
                    this.ReloadChartScoreData();
                }

                this.SetupDetailColumns();
                this.FillDetailGrid();
            }
            this.RefreshChart();
        }

        /// <summary>
        /// 填充详情表格（含重打差异对比行）
        /// </summary>
        private void FillDetailGrid()
        {
            int index = 0;
            long lastSegmentId = -1;
            foreach (var dataRow in this.currentScoreData)
            {
                string typeCountStr = "";
                string[] curSpeed = dataRow["speed"].ToString().Split('/');
                double speedVal = double.Parse(curSpeed[0]);
                Glob.CategoryValue categoryVal = (Glob.CategoryValue)dataRow["category"];
                bool isEn = CategoryHandler.IsEn(categoryVal);
                if (isEn)
                {
                    speedVal *= 5;
                }

                if ((long)dataRow["segment_id"] == lastSegmentId)
                {
                    //* 为重打数据
                    int rowCount = this.dataGridView1.Rows.Count - 1;
                    string[] oldSpeed = this.dataGridView1.Rows[rowCount].Cells[3].Value.ToString().Split('/');
                    double speedPlus;
                    if (isEn)
                    {
                        speedPlus = speedVal / 5 - double.Parse(oldSpeed[0]);
                    }
                    else
                    {
                        speedPlus = speedVal - double.Parse(oldSpeed[0]);
                    }
                    double keystrokePlus = (double)dataRow["keystroke"] - double.Parse(this.dataGridView1.Rows[rowCount].Cells[4].Value.ToString());
                    double codeLenPlus = (double)dataRow["code_len"] - double.Parse(this.dataGridView1.Rows[rowCount].Cells[5].Value.ToString());
                    //! 添加对比行
                    this.dataGridView1.Rows.Add("", "", "", (speedPlus > 0 ? "+" : "") + speedPlus.ToString("0.00"), (keystrokePlus > 0 ? "+" : "") + keystrokePlus.ToString("0.00"), (codeLenPlus > 0 ? "+" : "") + codeLenPlus.ToString("0.00"));
                    rowCount++;
                    this.dataGridView1.Rows[rowCount].Height = 10;
                    this.dataGridView1.Rows[rowCount].DefaultCellStyle.Font = new Font("Arial", 6.8f);
                    this.dataGridView1.Rows[rowCount].DefaultCellStyle.ForeColor = Color.LightGray;
                    // 对比高亮
                    if (speedPlus > 0)
                    {
                        this.dataGridView1.Rows[rowCount].Cells[3].Style.ForeColor = Color.FromArgb(253, 108, 108);
                    }
                    if (keystrokePlus > 0)
                    {
                        this.dataGridView1.Rows[rowCount].Cells[4].Style.ForeColor = Color.FromArgb(255, 129, 233);
                    }
                    if (codeLenPlus < 0)
                    {
                        this.dataGridView1.Rows[rowCount].Cells[5].Style.ForeColor = Color.FromArgb(124, 222, 255);
                    }
                    for (int i = 0; i < 24; i++)
                    {
                        if (i == 3 || i == 4 || i == 5)
                        {
                            this.dataGridView1.Rows[rowCount].Cells[i].Style.BackColor = Color.FromArgb(90, 90, 90);
                        }
                    }
                }
                else
                {
                    index++;
                    typeCountStr = index.ToString();
                }

                double diff = (double)dataRow["difficulty"];
                string cateText = CategoryHandler.GetCategoryText(categoryVal);
                DateTime scoreTime = Convert.ToDateTime(dataRow["score_time"]);
                this.dataGridView1.Rows.Add(typeCountStr, scoreTime.ToString("HH:mm:ss"), dataRow["segment_num"], dataRow["speed"], ((double)dataRow["keystroke"]).ToString("0.00"), ((double)dataRow["code_len"]).ToString("0.00"), ((double)dataRow["calc_len"]).ToString("0.00"), diff.ToString("0.00"), (diff * speedVal).ToString("0.00"), dataRow["back_change"], dataRow["backspace"], dataRow["enter"], dataRow["duplicate"], dataRow["error"], dataRow["back_rate"] + "%", dataRow["accuracy_rate"] + "%", dataRow["effciency"] + "%", dataRow["keys"], dataRow["count"], dataRow["type_words"], dataRow["words_rate"] + "%", dataRow["cost_time"], cateText, dataRow["article_title"]);
                this.dataGridView1.Rows[dataGridView1.RowCount - 1].Cells[1].Tag = scoreTime.ToString("yyyy-MM-dd HH:mm:ss");
                this.dataGridView1.Rows[dataGridView1.RowCount - 1].ContextMenuStrip = this.HistoryContextMenuStrip;
                #region 单元格高亮
                CellHighlight.Speed(dataGridView1.Rows[dataGridView1.RowCount - 1].Cells[3], speedVal, diff);
                CellHighlight.Keystroke(dataGridView1.Rows[dataGridView1.RowCount - 1].Cells[4], (double)dataRow["keystroke"]);
                CellHighlight.CodeLen(dataGridView1.Rows[dataGridView1.RowCount - 1].Cells[5], (double)dataRow["code_len"], (double)dataRow["calc_len"]);
                CellHighlight.Error(dataGridView1.Rows[dataGridView1.RowCount - 1].Cells[13], (int)dataRow["error"]);
                #endregion
                lastSegmentId = (long)dataRow["segment_id"];
            }

            this.dataGridView1.Enabled = true;
        }

        /// <summary>
        /// 清理数据
        /// </summary>
        private void ClearGridData()
        {
            this.dataGridView1.Rows.Clear();
            this.currentScoreData.Clear();
            this.chartScoreData.Clear();
            this.PreviewGroupBox.Text = "文段预览";
            this.PreviewRichTextBox.Text = "";
            this.SpeedChart.Series[0].Points.Clear();
            this.SpeedChart.Titles.Clear();
            this.dataGridView1.Enabled = false;
        }

        private void SetupSessionColumns()
        {
            this.dataGridView1.Columns.Clear();
            var cols = new (string Name, string Header, int Width)[] {
                ("序", "序", 28),
                ("日期", "日期", 70),
                ("时间", "时间", 50),
                ("标题", "标题", 80),
                ("段数", "段数", 40),
                ("速度", "速度", 50),
                ("击键", "击键", 50),
                ("码长", "码长", 45),
                ("难度", "难度", 45),
                ("评级", "评级", 45),
                ("回改率", "回改率", 52),
                ("键准", "键准", 45),
                ("效率", "效率", 45),
                ("键数", "键数", 45),
                ("字数", "字数", 45),
                ("打词率", "打词率", 52),
                ("用时", "用时", 50),
                ("类别", "类别", 40),
            };
            foreach (var c in cols)
            {
                var col = this.dataGridView1.Columns.Add(c.Name, c.Header);
                this.dataGridView1.Columns[col].Width = c.Width;
            }
            foreach (DataGridViewColumn col in this.dataGridView1.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
                col.ReadOnly = true;
                col.DefaultCellStyle.BackColor = Color.DimGray;
                col.DefaultCellStyle.ForeColor = Color.FromArgb(224, 224, 224);
                col.DefaultCellStyle.SelectionBackColor = Color.DimGray;
                col.DefaultCellStyle.SelectionForeColor = Color.Bisque;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            this.dataGridView1.Columns["日期"].DefaultCellStyle.BackColor = Color.Gray;
            this.dataGridView1.Columns["日期"].DefaultCellStyle.ForeColor = Color.FromArgb(255, 224, 192);
            this.dataGridView1.Columns["时间"].DefaultCellStyle.BackColor = Color.Gray;
            this.dataGridView1.Columns["时间"].DefaultCellStyle.ForeColor = Color.FromArgb(255, 224, 192);
        }

        private void SetupDetailColumns()
        {
            this.dataGridView1.Columns.Clear();
            var cols = new (string Name, string Header)[] {
                ("序", "序"), ("时间", "时间"), ("段号", "段号"), ("速度", "速度"), ("击键", "击键"),
                ("码长", "码长"), ("理论", "理论"), ("难度", "难度"), ("评级", "评级"),
                ("回改", "回改"), ("退格", "退格"), ("回车", "回车"), ("选重", "选重"),
                ("错字", "错字"), ("回改率", "回改率"), ("键准", "键准"), ("效率", "效率"),
                ("键数", "键数"), ("字数", "字数"), ("打词", "打词"), ("打词率", "打词率"),
                ("用时", "用时"), ("类别", "类别"), ("标题", "标题")
            };
            foreach (var col in cols)
            {
                var dcol = new DataGridViewTextBoxColumn();
                dcol.Name = col.Name;
                dcol.HeaderText = col.Header;
                dcol.SortMode = DataGridViewColumnSortMode.NotSortable;
                dcol.ReadOnly = true;
                dcol.Width = 43;
                dcol.DefaultCellStyle.BackColor = Color.DimGray;
                dcol.DefaultCellStyle.ForeColor = Color.FromArgb(224, 224, 224);
                dcol.DefaultCellStyle.SelectionBackColor = Color.DimGray;
                dcol.DefaultCellStyle.SelectionForeColor = Color.Bisque;
                dcol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                this.dataGridView1.Columns.Add(dcol);
            }
            this.dataGridView1.Columns["序"].Width = 28;
            this.dataGridView1.Columns["序"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            this.dataGridView1.Columns["时间"].DefaultCellStyle.BackColor = Color.Gray;
            this.dataGridView1.Columns["时间"].DefaultCellStyle.ForeColor = Color.FromArgb(255, 224, 192);
            this.dataGridView1.Columns["回改率"].Width = 58;
            this.dataGridView1.Columns["打词率"].Width = 58;
            this.dataGridView1.Columns["击键"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            this.dataGridView1.Columns["码长"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            this.dataGridView1.Columns["理论"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            this.dataGridView1.Columns["段号"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            this.dataGridView1.Columns["段号"].DefaultCellStyle.BackColor = Color.DimGray;
            this.dataGridView1.Columns["段号"].DefaultCellStyle.ForeColor = Color.FromArgb(224, 224, 224);
            this.dataGridView1.Columns["段号"].DefaultCellStyle.SelectionBackColor = Color.DimGray;
            this.dataGridView1.Columns["段号"].DefaultCellStyle.SelectionForeColor = Color.Bisque;
        }

        private class SessionAggr
        {
            public string SessionId;
            public string FirstTime;
            public string Title;
            public int SegCount;
            public int TotalChars;
            public double TotalSpeed;
            public double TotalKeystroke;
            public double TotalCodeLen;
            public double TotalDifficulty;
            public double TotalRating;
            public double TotalBackRate;
            public double TotalAccuracy;
            public double TotalEfficiency;
            public int TotalKeys;
            public double TotalWordsRate;
            public double TotalSeconds;
            public string Category;
        }

        private double ParseCostTimeSeconds(string costTime)
        {
            if (string.IsNullOrEmpty(costTime))
                return 0;
            try
            {
                var parts = costTime.Split(':');
                if (parts.Length == 2)
                {
                    return int.Parse(parts[0]) * 60 + double.Parse(parts[1]);
                }
                if (parts.Length == 3)
                {
                    return int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + double.Parse(parts[2]);
                }
            }
            catch { }
            return 0;
        }

        private string FormatSeconds(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return ts.ToString(@"h\:mm\:ss");
            return ts.ToString(@"m\:ss");
        }

        private void ShowSessionList()
        {
            this.isSessionView = true;
            this.backButton.Visible = false;
            this.SetupSessionColumns();

            var sessions = new Dictionary<string, SessionAggr>();
            foreach (var dataRow in this.currentScoreData)
            {
                string sessId = dataRow["session_id"] == DBNull.Value ? "" : dataRow["session_id"].ToString();
                if (string.IsNullOrEmpty(sessId))
                {
                    continue;
                }

                string[] curSpeed = dataRow["speed"].ToString().Split('/');
                double speedVal = double.Parse(curSpeed[0]);
                Glob.CategoryValue categoryVal = (Glob.CategoryValue)dataRow["category"];
                bool isEn = CategoryHandler.IsEn(categoryVal);
                if (isEn)
                {
                    speedVal *= 5;
                }

                double keystroke = (double)dataRow["keystroke"];
                double codeLen = (double)dataRow["code_len"];
                double difficulty = (double)dataRow["difficulty"];
                double backRate = (double)dataRow["back_rate"];
                double accuracy = (double)dataRow["accuracy_rate"];
                int effciency = (int)dataRow["effciency"];
                int keys = (int)dataRow["keys"];
                int count = (int)dataRow["count"];
                double wordsRate = (double)dataRow["words_rate"];
                double secs = ParseCostTimeSeconds(dataRow["cost_time"].ToString());
                string cateText = CategoryHandler.GetCategoryText(categoryVal);

                if (!sessions.ContainsKey(sessId))
                {
                    DateTime scoreTime = Convert.ToDateTime(dataRow["score_time"]);
                    sessions[sessId] = new SessionAggr
                    {
                        SessionId = sessId,
                        FirstTime = scoreTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        Title = dataRow["article_title"].ToString(),
                        SegCount = 1,
                        TotalChars = count,
                        TotalSpeed = speedVal,
                        TotalKeystroke = keystroke,
                        TotalCodeLen = codeLen,
                        TotalDifficulty = difficulty,
                        TotalRating = difficulty * speedVal,
                        TotalBackRate = backRate,
                        TotalAccuracy = accuracy,
                        TotalEfficiency = effciency,
                        TotalKeys = keys,
                        TotalWordsRate = wordsRate,
                        TotalSeconds = secs,
                        Category = cateText,
                    };
                }
                else
                {
                    var entry = sessions[sessId];
                    entry.SegCount++;
                    entry.TotalChars += count;
                    entry.TotalSpeed += speedVal;
                    entry.TotalKeystroke += keystroke;
                    entry.TotalCodeLen += codeLen;
                    entry.TotalDifficulty += difficulty;
                    entry.TotalRating += difficulty * speedVal;
                    entry.TotalBackRate += backRate;
                    entry.TotalAccuracy += accuracy;
                    entry.TotalEfficiency += effciency;
                    entry.TotalKeys += keys;
                    entry.TotalWordsRate += wordsRate;
                    entry.TotalSeconds += secs;
                }
            }

            int index = 0;
            var sortedSessions = sessions.Values.OrderByDescending(e => e.FirstTime).ToList();
            foreach (var e in sortedSessions)
            {
                index++;
                int n = e.SegCount;
                string datePart = e.FirstTime.Substring(0, 10);
                string timePart = e.FirstTime.Substring(11);
                this.dataGridView1.Rows.Add(
                    index.ToString(),
                    datePart,
                    timePart,
                    e.Title,
                    n.ToString(),
                    (e.TotalSpeed / n).ToString("0.00"),
                    (e.TotalKeystroke / n).ToString("0.00"),
                    (e.TotalCodeLen / n).ToString("0.00"),
                    (e.TotalDifficulty / n).ToString("0.00"),
                    (e.TotalRating / n).ToString("0.00"),
                    (e.TotalBackRate / n).ToString("0.00") + "%",
                    (e.TotalAccuracy / n).ToString("0.00") + "%",
                    (e.TotalEfficiency / n).ToString("0") + "%",
                    e.TotalKeys.ToString(),
                    e.TotalChars.ToString(),
                    (e.TotalWordsRate / n).ToString("0.00") + "%",
                    FormatSeconds(e.TotalSeconds),
                    e.Category);
                this.dataGridView1.Rows[dataGridView1.RowCount - 1].Cells[1].Tag = e.SessionId;
            }

            this.dataGridView1.Enabled = true;
        }

        private void ShowSessionDetail(string sessionId)
        {
            this.isSessionView = false;
            this.currentSessionId = sessionId;
            this.backButton.Visible = true;
            this.backButton.Location = new Point(6, 2);
            this.ResultLabel.Location = new Point(this.backButton.Right + 10, 4);
            this.currentSessionTitle = "";

            this.currentScoreData = Glob.ScoreHistory.GetScoresBySessionId(sessionId, 0, 1000);
            if (this.currentScoreData.Count > 0)
            {
                this.currentSessionTitle = this.currentScoreData[0]["article_title"].ToString();
            }
            this.chartScoreData = this.currentScoreData;

            this.ResultLabel.Text = "发文详情 — " + this.currentSessionTitle +
                " (" + this.currentScoreData.Count.ToString() + "段)";
            this.UpdateGridToolBar();
            this.SetupDetailColumns();
            this.FillDetailGrid();
            this.RefreshChart();
        }

        private void BackButton_Click(object sender, EventArgs e)
        {
            this.isSessionView = true;
            this.currentSessionId = null;
            this.backButton.Visible = false;
            this.ResultLabel.Location = new Point(6, 4);
            this.RefreshData();
        }

        private void UpdateGridToolBar()
        {
            if (!this.isSessionView)
            {
                this.CountLabel.Text = this.currentScoreData.Count.ToString();
                this.label1.Text = "段数：";
                this.paginationPanel.Visible = false;
                return;
            }
            this.paginationPanel.Visible = true;
            this.label1.Text = "总发文数：";
            this.CountLabel.Text = this.totalCount.ToString();
            this.TotalPageNumLabel.Text = "/" + this.TotalPage.ToString() + "页";
            this.PageNumTextBox.Text = this.currentPage.ToString();
        }

        private void ReloadChartScoreData()
        {
            this.chartScoreData.Clear();
            if (!this.isSessionView)
            {
                this.chartScoreData = this.currentScoreData;
                return;
            }

            if (this.totalCount <= 0)
            {
                return;
            }

            this.chartScoreData = Glob.ScoreHistory.GetSessionScores(
                this.dataType.Date, this.dataType.EndDate, this.dataType.Title, this.dataType.SegmentId, 0, this.totalCount);
        }

        private double GetDisplaySpeed(StorageDataSet.ScoreRow scoreRow)
        {
            string[] curSpeed = scoreRow["speed"].ToString().Split('/');
            double speedVal = double.Parse(curSpeed[0]);
            if (CategoryHandler.IsEn((Glob.CategoryValue)scoreRow["category"]))
            {
                speedVal *= 5;
            }
            return speedVal;
        }

        private void ClearPreview()
        {
            this.PreviewGroupBox.Text = "文段预览";
            this.PreviewRichTextBox.Text = "";
        }

        private void UpdatePreview(DataGridViewRow curRow)
        {
            if (curRow == null || curRow.Cells.Count < 2)
            {
                this.ClearPreview();
                return;
            }

            string scoreTime = curRow.Cells[1].Tag != null ? curRow.Cells[1].Tag.ToString() :
                (curRow.Cells[1].Value == null ? "" : curRow.Cells[1].Value.ToString());
            if (string.IsNullOrEmpty(scoreTime))
            {
                this.ClearPreview();
                return;
            }

            StorageDataSet.ScoreRow sd = StorageDataSet.GetScoreRowFromTime(this.currentScoreData, scoreTime);
            if (sd == null)
            {
                this.ClearPreview();
                return;
            }

            long segmentId = (long)sd["segment_id"];
            double diff = (double)sd["difficulty"];
            this.PreviewGroupBox.Text = "文段预览 <" + this.frm.DiffDict.DiffText(diff) + "> [ID=" + segmentId.ToString() + "]";
            this.PreviewRichTextBox.Text = Glob.ScoreHistory.GetContentFromSegmentId(segmentId);
        }

        private void ConfigureSingleChartStyle()
        {
            Series series = this.SpeedChart.Series[0];
            Axis axisX = this.SpeedChart.ChartAreas[0].AxisX;
            Axis axisY = this.SpeedChart.ChartAreas[0].AxisY;

            series.ChartType = SeriesChartType.SplineArea;
            series.XValueType = ChartValueType.Auto;
            series.MarkerStyle = MarkerStyle.None;
            series.MarkerSize = 0;
            axisX.LabelStyle.Format = "";
            axisX.LabelStyle.Angle = 0;
            axisX.LabelStyle.Interval = 0;
            axisX.LabelStyle.IntervalType = DateTimeIntervalType.Number;
            axisX.IntervalType = DateTimeIntervalType.Number;
            axisX.Minimum = 1D;
            axisX.Maximum = double.NaN;
            axisY.Minimum = double.NaN;
            axisY.Maximum = double.NaN;
        }

        private void ShowTrendChart()
        {
            this.SpeedChart.Series[0].Points.Clear();
            this.SpeedChart.Titles.Clear();
            this.SpeedChart.Titles.Add("速度趋势");

            if (this.chartScoreData.Count == 0)
            {
                return;
            }

            List<StorageDataSet.ScoreRow> orderedRows = this.chartScoreData
                .Cast<StorageDataSet.ScoreRow>()
                .OrderBy(row => Convert.ToDateTime(row["score_time"]))
                .ToList();

            Series series = this.SpeedChart.Series[0];
            Axis axisX = this.SpeedChart.ChartAreas[0].AxisX;
            Axis axisY = this.SpeedChart.ChartAreas[0].AxisY;

            series.ChartType = SeriesChartType.FastLine;
            series.XValueType = ChartValueType.Auto;
            series.MarkerStyle = MarkerStyle.Circle;
            series.MarkerSize = 3;
            axisX.Minimum = double.NaN;
            axisX.Maximum = double.NaN;
            axisX.Interval = 1;
            axisX.LabelStyle.Format = "";
            axisX.LabelStyle.Angle = -45;
            axisX.IsMarginVisible = true;
            axisY.Minimum = double.NaN;
            axisY.Maximum = double.NaN;

            double minSpeed = double.MaxValue;
            int index = 0;
            string lastDate = "";
            foreach (StorageDataSet.ScoreRow row in orderedRows)
            {
                DateTime scoreTime = Convert.ToDateTime(row["score_time"]);
                string dateLabel = scoreTime.ToString("MM-dd");
                double speedVal = this.GetDisplaySpeed(row);
                DataPoint point = new DataPoint();
                point.SetValueXY(index, speedVal);
                point.AxisLabel = dateLabel == lastDate ? "" : dateLabel;
                point.ToolTip = scoreTime.ToString("yyyy-MM-dd HH:mm:ss") + " 速度 " + speedVal.ToString("0.00");
                this.SpeedChart.Series[0].Points.Add(point);
                lastDate = dateLabel;
                if (speedVal < minSpeed)
                {
                    minSpeed = speedVal;
                }
                index++;
            }

            if (minSpeed < double.MaxValue)
            {
                axisY.Minimum = Math.Max(0, Math.Floor(minSpeed / 10.0) * 10.0);
            }
        }

        private void ShowSelectedCurveChart(DataGridViewRow curRow)
        {
            this.SpeedChart.Series[0].Points.Clear();
            this.SpeedChart.Titles.Clear();
            this.SpeedChart.Titles.Add("单次速度曲线");
            this.ConfigureSingleChartStyle();

            if (curRow == null || curRow.Cells.Count < 2)
            {
                return;
            }

            string scoreTime = curRow.Cells[1].Tag != null ? curRow.Cells[1].Tag.ToString() :
                (curRow.Cells[1].Value == null ? "" : curRow.Cells[1].Value.ToString());
            if (string.IsNullOrEmpty(scoreTime))
            {
                return;
            }

            string adv = Glob.ScoreHistory.GetAdvancedDataFromTime(scoreTime, "curve");
            if (string.IsNullOrEmpty(adv))
            {
                return;
            }

            double[] advCurve = Array.ConvertAll(adv.Split('|'), s => double.Parse(s));
            foreach (double ce in advCurve)
            {
                this.SpeedChart.Series[0].Points.AddY(ce);
            }

            this.SpeedChart.ChartAreas[0].AxisY.Minimum = (int)(advCurve.Min() / 20) * 10;
            this.SpeedChart.ChartAreas[0].AxisX.Interval = Math.Max(1, advCurve.Length / 5);
        }

        private void RefreshChart()
        {
            // 会话视图中行数据是聚合结果，无法展示单次曲线，始终显示趋势图
            if (this.showTrendChart || this.isSessionView)
            {
                this.ShowTrendChart();
            }
            else
            {
                this.ShowSelectedCurveChart(this.dataGridView1.CurrentRow);
            }
        }

        private void SpeedChart_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.chartOverlay.Visible)
            {
                this.suppressArticleListEvent = true;
                this.SpeedChart.Dock = DockStyle.Fill;
                this.rightSplitContainer.Panel2.Controls.Add(this.SpeedChart);
                this.rightSplitContainer.Panel2.Controls.SetChildIndex(this.SpeedChart, 0);
                this.chartOverlay.Visible = false;
                this.innerSplitContainer.Visible = true;
                this.suppressArticleListEvent = false;
                return;
            }

            this.chartOverlay.Controls.Add(this.SpeedChart);
            this.SpeedChart.Dock = DockStyle.Fill;
            this.chartOverlay.Visible = true;
            this.innerSplitContainer.Visible = false;
        }

        private void DataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (this.isSessionView && this.dataGridView1.Rows[e.RowIndex].Cells.Count > 1)
            {
                string sessionId = this.dataGridView1.Rows[e.RowIndex].Cells[1].Tag as string;
                if (!string.IsNullOrEmpty(sessionId))
                {
                    this.ShowSessionDetail(sessionId);
                }
            }
        }

        private void HistorySelectionChanged(object sender, EventArgs e)
        {
            DataGridViewRow curRow = (sender as DataGridView).CurrentRow;

            if (this.isSessionView)
            {
                // 会话视图：更新预览面板并加载该发文的全部段成绩到图表
                if (curRow != null && curRow.Cells.Count > 3
                    && curRow.Cells[1].Tag is string sessionId && !string.IsNullOrEmpty(sessionId))
                {
                    string title = curRow.Cells[3].Value?.ToString() ?? "";
                    string segCount = curRow.Cells[4].Value?.ToString() ?? "0";
                    string speed = curRow.Cells[5].Value?.ToString() ?? "0";
                    string date = curRow.Cells[1].Value?.ToString() ?? "";
                    string time = curRow.Cells[2].Value?.ToString() ?? "";
                    this.PreviewGroupBox.Text = "发文概览";
                    this.PreviewRichTextBox.Text =
                        $"标题：{title}\r\n日期：{date} {time}\r\n段数：{segCount}\r\n均速：{speed}";

                    // 加载该发文的全部段成绩用于图表
                    this.chartScoreData = Glob.ScoreHistory.GetScoresBySessionId(sessionId, 0, 1000);
                    this.RefreshChart();
                }
                return;
            }

            this.UpdatePreview(curRow);
            if (!this.showTrendChart)
            {
                this.ShowSelectedCurveChart(curRow);
            }
        }

        #region 表格右键菜单事件
        private void History_CellMoseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (this.isSessionView)
            {
                return;
            }
            this.gridHandler.SetMouseLocation(e);
            this.ItemToolStripTextBox.Text = this.gridHandler.MenuGetScoreTime();
        }

        private void CopyScoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.gridHandler.CopyScore(this.currentScoreData);
        }

        private void CopyPicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.gridHandler.CopyPicScore(this.currentScoreData);
        }

        private void CopyTitleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.gridHandler.CopyTitle();
        }

        private void CopyContentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.gridHandler.CopyContent(this.currentScoreData);
        }

        private void SpeedAnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.gridHandler.SpeedAn(this.currentScoreData, this.frm);
        }

        private void TypeAnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.gridHandler.TypeAn(this.currentScoreData);
        }

        private void KeyAnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.gridHandler.KeyAn();
        }

        private void CalcKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.gridHandler.CalcKeys();
        }

        private void SearchTitleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string articleTitle = this.gridHandler.GetArticleTitle();
            if (!string.IsNullOrEmpty(articleTitle))
            {
                this.dataType.Title = articleTitle;
                this.suppressArticleListEvent = true;
                if (this.articleListBox.Items.Contains(articleTitle))
                {
                    this.articleListBox.SelectedItem = articleTitle;
                }
                else
                {
                    this.articleListBox.SelectedIndex = 0;
                }
                this.suppressArticleListEvent = false;

                if (!this.isSessionView)
                {
                    this.isSessionView = true;
                    this.currentSessionId = null;
                    this.backButton.Visible = false;
                }
                this.RefreshData();
            }
        }

        private void SearchSegmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            long segmentId = this.gridHandler.GetSegmentId(this.currentScoreData);
            if (segmentId != -1)
            {
                this.dataType.SegmentId = segmentId;

                if (!this.isSessionView)
                {
                    this.isSessionView = true;
                    this.currentSessionId = null;
                    this.backButton.Visible = false;
                }
                this.RefreshData();
            }
        }

        private void RetypeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string[] result = this.gridHandler.GetRetype(this.currentScoreData);
            if (result != null)
            {
                this.frm.TypeContentDirectly(result[0], result[1], result[2]);
                this.Close();
            }
        }
        #endregion

        #region 跳转页数处理
        private void JumpPageHandler(int pageNum)
        {
            this.ClearGridData();
            this.currentPage = pageNum;
            this.PageNumTextBox.Text = pageNum.ToString();

            if (this.isSessionView)
            {
                this.currentScoreData = Glob.ScoreHistory.GetSessionScores(
                    this.dataType.Date, this.dataType.EndDate, this.dataType.Title, this.dataType.SegmentId,
                    (pageNum - 1) * PageSize, PageSize);
                this.ReloadChartScoreData();
                this.ShowSessionList();
            }
            else
            {
                this.currentScoreData = Glob.ScoreHistory.GetScoresFiltered(
                    this.dataType.Date, this.dataType.EndDate, this.dataType.Title, this.dataType.SegmentId,
                    (pageNum - 1) * PageSize, PageSize);
                this.ReloadChartScoreData();
                this.SetupDetailColumns();
                this.FillDetailGrid();
            }
            this.RefreshChart();
        }
        #endregion

        #region 翻页按钮
        private void FirstPageButton_Click(object sender, EventArgs e)
        {
            if (this.currentPage > 1)
            {
                this.JumpPageHandler(1);
            }
        }

        private void PrePageButton_Click(object sender, EventArgs e)
        {
            if (this.currentPage > 1)
            {
                this.JumpPageHandler(this.currentPage - 1);
            }
        }

        private void NextPageButton_Click(object sender, EventArgs e)
        {
            if (this.currentPage < this.TotalPage)
            {
                this.JumpPageHandler(this.currentPage + 1);
            }
        }

        private void LastPageButton_Click(object sender, EventArgs e)
        {
            if (this.currentPage < this.TotalPage)
            {
                this.JumpPageHandler(this.TotalPage);
            }
        }

        private void JumpPageButton_Click(object sender, EventArgs e)
        {
            if (this.PageNumTextBox.Text != "" && this.PageNumTextBox.Text != "0")
            {
                int pageNum = int.Parse(this.PageNumTextBox.Text);
                if (pageNum > 0 && pageNum <= this.TotalPage && pageNum != this.currentPage)
                {
                    this.JumpPageHandler(pageNum);
                }
            }
        }

        private void PageNumTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            { // 按下 Enter 跳转
                this.JumpPageButton.PerformClick();
            } 
            else if (e.KeyChar == 27)
            { // 按下 ESC 恢复当前页码
                this.PageNumTextBox.Text = this.currentPage.ToString();
            } 
            else if (e.KeyChar >= 31 && (e.KeyChar < '0' || e.KeyChar > '9'))
            { // 限制输入框只能输入数字
                e.Handled = true;
            }
        }
        #endregion

        #region 删除数据
        private void RefreshAfterDelete()
        {
            if (!this.isSessionView)
            {
                this.ShowSessionDetail(this.currentSessionId);
                return;
            }

            this.totalCount = Glob.ScoreHistory.GetSessionCount(
                this.dataType.Date, this.dataType.EndDate, this.dataType.Title, this.dataType.SegmentId);
            if (this.totalCount <= 0)
            {
                this.totalCount = 0;
                this.currentPage = 1;
                this.UpdateGridToolBar();
                this.ClearGridData();
            }
            else
            {
                if (this.currentPage > this.TotalPage)
                {
                    this.currentPage = this.TotalPage;
                }
                this.UpdateGridToolBar();
                this.JumpPageHandler(this.currentPage);
            }
            this.LoadArticleList();
            this.MonthCalendar.BoldedDates = Glob.ScoreHistory.GetAllScoreDates();
            this.MonthCalendar.UpdateBoldedDates();
        }

        private void DeleteItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string scoreTime = this.gridHandler.MenuGetScoreTime();
            if (!string.IsNullOrEmpty(scoreTime))
            {
                switch (MessageBox.Show("确认删除跟打时间为 " + scoreTime + " 的这条记录吗？", "删除询问", MessageBoxButtons.YesNo))
                {
                    case DialogResult.Yes:
                        if (Glob.ScoreHistory.DeleteScoreItemByTime(scoreTime))
                        {
                            this.RefreshAfterDelete();
                        }
                        break;
                    case DialogResult.No:
                        break;
                }
            }
        }

        private void DeleteSegmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            long segmentId = this.gridHandler.GetSegmentId(this.currentScoreData);
            if (segmentId != -1)
            {
                switch (MessageBox.Show("确认删除文段ID为 " + segmentId.ToString() + " 的所有记录吗？", "删除询问", MessageBoxButtons.YesNo))
                {
                    case DialogResult.Yes:
                        Glob.ScoreHistory.DeleteScoreItemBySegmentId(segmentId);
                        this.RefreshAfterDelete();
                        break;
                    case DialogResult.No:
                        break;
                }
            }
        }

        private void DeletePageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            switch (MessageBox.Show("确认删除该页所有记录吗？", "删除询问", MessageBoxButtons.YesNo))
            {
                case DialogResult.Yes:
                    int deleteCount = 0;
                    foreach (var dataRow in this.currentScoreData)
                    {
                        if (Glob.ScoreHistory.DeleteScoreItemByTime(dataRow["score_time"].ToString()))
                        {
                            deleteCount++;
                        }
                    }

                    if (deleteCount > 20)
                    { // 返还磁盘空间
                        Glob.ScoreHistory.CleanDisk();
                    }

                    this.RefreshAfterDelete();
                    break;
                case DialogResult.No:
                    break;
            }
        }
        #endregion

        #region 选择日期
        private void MonthCalendar_DateSelected(object sender, DateRangeEventArgs e)
        {
            this.dataType.Date = e.Start;
            this.dataType.EndDate = e.End;

            if (!this.isSessionView)
            {
                this.isSessionView = true;
                this.currentSessionId = null;
                this.backButton.Visible = false;
            }
            this.RefreshData();
        }

        private void MonthCalendar_HigherViewRangeSelected(object sender, DateRangeEventArgs e)
        {
            this.dataType.Date = e.Start;
            this.dataType.EndDate = e.End;

            if (!this.isSessionView)
            {
                this.isSessionView = true;
                this.currentSessionId = null;
                this.backButton.Visible = false;
            }
            this.RefreshData();
        }
        #endregion

        #region 日历右键菜单项
        private void MonthCalendar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (this.dataType.Date == default(DateTime))
                {
                    this.dataType.Date = DateTime.Now;
                    this.dataType.EndDate = this.dataType.Date;
                }
                if (this.dataType.Date != this.dataType.EndDate)
                {
                    this.DeleteDayToolStripMenuItem.Text = "删除所选区间的记录";
                }
                else
                {
                    this.DeleteDayToolStripMenuItem.Text = "删除" + this.dataType.Date.ToString("d") + "的记录";
                }
                this.DeleteMonthToolStripMenuItem.Text = "删除" + this.dataType.Date.ToString("Y") + "的记录";
                this.DeleteYearToolStripMenuItem.Text = "删除" + this.dataType.Date.ToString("yyyy") + "年的记录";
            }
        }

        /// <summary>
        /// 日历删除处理器
        /// </summary>
        /// <param name="_type">类别</param>
        private void MonthCalendarDateDeleteHandler(string _type)
        {
            string dateTip = "";
            string dateVal = "";
            bool isRangeDelete = false;
            switch (_type)
            {
                case "day":
                    if (this.dataType.Date != this.dataType.EndDate)
                    {
                        isRangeDelete = true;
                        dateTip = this.dataType.Date.ToString("d") + " - " + this.dataType.EndDate.ToString("d");
                    }
                    else
                    {
                        dateTip = this.dataType.Date.ToString("d");
                        dateVal = this.dataType.Date.ToString("d");
                    }
                    break;
                case "month":
                    dateTip = this.dataType.Date.ToString("Y");
                    dateVal = this.dataType.Date.ToString("yyyy-MM");
                    break;
                case "year":
                    dateTip = this.dataType.Date.ToString("yyyy") + "年";
                    dateVal = this.dataType.Date.ToString("yyyy");
                    break;
            }
            
            if (isRangeDelete || dateVal != "")
            {
                switch (MessageBox.Show("确认删除 " + dateTip + " 的所有记录吗？", "删除询问", MessageBoxButtons.YesNo))
                {
                    case DialogResult.Yes:
                        if (isRangeDelete)
                        {
                            Glob.ScoreHistory.DeleteScoreItemByDateRange(this.dataType.Date, this.dataType.EndDate);
                        }
                        else
                        {
                            Glob.ScoreHistory.DeleteScoreItemByDate(dateVal);
                        }

                        if (!this.isSessionView)
                        {
                            this.isSessionView = true;
                            this.currentSessionId = null;
                            this.backButton.Visible = false;
                            this.ResultLabel.Location = new Point(6, 4);
                        }
                        this.RefreshData();
                        this.LoadArticleList();
                        break;
                    case DialogResult.No:
                        break;
                }
            }
        }

        private void TrendChartCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            this.showTrendChart = this.TrendChartCheckBox.Checked;
            this.RefreshChart();
        }

        private void DeleteDayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.MonthCalendarDateDeleteHandler("day");
        }

        private void DeleteMonthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.MonthCalendarDateDeleteHandler("month");
        }

        private void DeleteYearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.MonthCalendarDateDeleteHandler("year");
        }

        private void DeleteAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            switch (MessageBox.Show("确认删除所有的历史记录吗？", "删除询问", MessageBoxButtons.YesNo))
            {
                case DialogResult.Yes:
                    Glob.ScoreHistory.DeleteAllScore();
                    this.dataType.Date = DateTime.Now;
                    this.dataType.EndDate = DateTime.Now;
                    this.dataType.Title = null;
                    this.dataType.SegmentId = null;
                    this.isSessionView = true;
                    this.currentSessionId = null;
                    this.backButton.Visible = false;
                    this.ResultLabel.Location = new Point(6, 4);
                    this.LoadArticleList();
                    this.MonthCalendar.BoldedDates = Glob.ScoreHistory.GetAllScoreDates();
                    this.MonthCalendar.UpdateBoldedDates();
                    this.RefreshData();
                    break;
                case DialogResult.No:
                    break;
            }
        }
        #endregion

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && this.chartOverlay != null && this.chartOverlay.Visible)
            {
                this.SpeedChart_MouseDoubleClick(null, null);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
