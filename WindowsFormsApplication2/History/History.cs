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

        /// <summary>
        /// 总页数
        /// </summary>
        private int TotalPage { 
            get
            {
                return (int)Math.Ceiling((float)this.totalCount / this.PageSize);
            } 
        }

        public History(Form1 frm1)
        {
            this.frm = frm1;
            InitializeComponent();
            this.PreviewRichTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.MonthCalendar.HigherViewRangeSelected += MonthCalendar_HigherViewRangeSelected;
            this.splitContainer1.Panel1.Resize += (sender, e) => UpdateTopPanelLayout();
        }

        private void History_Load(object sender, EventArgs e)
        {
            this.gridHandler = new HistoryDataGridHandler(this.dataGridView1);
            this.MonthCalendar.BoldedDates = Glob.ScoreHistory.GetAllScoreDates();
            this.BeginInvoke((MethodInvoker)UpdateTopPanelLayout);
            this.showTrendChart = this.TrendChartCheckBox.Checked;
            ShowDataFromDateRange(DateTime.Now, DateTime.Now);
        }

        // 按日历实际尺寸重排顶部区域，避免高 DPI 下出现多余留白。
        private void UpdateTopPanelLayout()
        {
            if (!this.IsHandleCreated || this.splitContainer1.Panel1.ClientSize.Width <= 0)
            {
                return;
            }

            const int outerPadding = 5;
            const int controlSpacing = 5;
            int top = this.MonthCalendar.Top;
            int calendarHeight = this.MonthCalendar.Height;
            int previewLeft = this.MonthCalendar.Right + controlSpacing;
            int previewWidth = this.PreviewGroupBox.Width;
            int chartLeft = previewLeft + previewWidth + controlSpacing;
            int chartWidth = Math.Max(120, this.splitContainer1.Panel1.ClientSize.Width - chartLeft - outerPadding);
            int toolbarTop = this.MonthCalendar.Bottom + controlSpacing;
            int splitterDistance = toolbarTop + this.ToolPanel.Height + outerPadding;
            int maxSplitterDistance = this.splitContainer1.Height - this.splitContainer1.Panel2MinSize - this.splitContainer1.SplitterWidth;

            this.PreviewGroupBox.SetBounds(previewLeft, top, previewWidth, calendarHeight);
            this.SpeedChart.SetBounds(chartLeft, top, chartWidth, calendarHeight);
            this.ToolPanel.SetBounds(
                outerPadding,
                toolbarTop,
                this.splitContainer1.Panel1.ClientSize.Width - outerPadding * 2 - 1,
                this.ToolPanel.Height);

            if (splitterDistance > 0 && splitterDistance <= maxSplitterDistance && this.splitContainer1.SplitterDistance != splitterDistance)
            {
                this.splitContainer1.SplitterDistance = splitterDistance;
            }
        }

        /// <summary>
        /// 展示数据
        /// </summary>
        private void ShowGridData()
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

        private void UpdateGridToolBar()
        {
            this.CountLabel.Text = this.totalCount.ToString();
            this.TotalPageNumLabel.Text = "/" + this.TotalPage.ToString() + "页";
            this.PageNumTextBox.Text = this.currentPage.ToString();
        }

        private void ShowDataFromDate(DateTime date)
        {
            this.ShowDataFromDateRange(date, date);
        }

        private void ShowDataFromDateRange(DateTime startDate, DateTime endDate)
        {
            this.ClearGridData();
            if (startDate.Date > endDate.Date)
            {
                DateTime temp = startDate;
                startDate = endDate;
                endDate = temp;
            }

            this.dataType.Cur = "Date";
            this.dataType.Date = startDate.Date;
            this.dataType.EndDate = endDate.Date;

            bool isSingleDate = this.dataType.Date == this.dataType.EndDate;
            this.ResultLabel.Text = isSingleDate
                ? "日期：" + this.dataType.Date.ToString("d")
                : "日期：" + this.dataType.Date.ToString("d") + " - " + this.dataType.EndDate.ToString("d");
            this.totalCount = isSingleDate
                ? Glob.ScoreHistory.GetScoreCountFromDate(this.dataType.Date)
                : Glob.ScoreHistory.GetScoreCountFromDateRange(this.dataType.Date, this.dataType.EndDate);

            if (this.TotalPage > 0)
            {
                this.currentPage = 1;
            }
            else
            {
                this.currentPage = 0;
            }

            this.UpdateGridToolBar();
            if (this.totalCount > 0)
            {
                this.currentScoreData = isSingleDate
                    ? Glob.ScoreHistory.GetScoreFromDate(this.dataType.Date, 0, PageSize)
                    : Glob.ScoreHistory.GetScoreFromDateRange(this.dataType.Date, this.dataType.EndDate, 0, PageSize);
                this.ReloadChartScoreData();
            }
            this.ShowGridData();
            this.RefreshChart();
        }

        private void ShowDataFromTitle(string title)
        {
            this.ClearGridData();
            this.dataType.Cur = "Title";
            this.dataType.Title = title;
            this.dataType.EndDate = this.dataType.Date;
            this.ResultLabel.Text = "标题：" + title;
            this.totalCount = Glob.ScoreHistory.GetScoreCountFromTitle(title);

            if (this.TotalPage > 0)
            {
                this.currentPage = 1;
            }
            else
            {
                this.currentPage = 0;
            }

            this.UpdateGridToolBar();
            if (this.totalCount > 0)
            {
                this.currentScoreData = Glob.ScoreHistory.GetScoreFromTitle(title, 0, PageSize);
                this.ReloadChartScoreData();
            }
            this.ShowGridData();
            this.RefreshChart();
        }

        private void ShowDataFromSubTitle(string title)
        {
            this.ClearGridData();
            this.dataType.Cur = "SubTitle";
            this.dataType.SubTitle = title;
            this.dataType.EndDate = this.dataType.Date;
            this.ResultLabel.Text = "搜索标题：" + title;
            this.totalCount = Glob.ScoreHistory.GetScoreCountFromSubTitle(title);

            if (this.TotalPage > 0)
            {
                this.currentPage = 1;
            }
            else
            {
                this.currentPage = 0;
            }

            this.UpdateGridToolBar();
            if (this.totalCount > 0)
            {
                this.currentScoreData = Glob.ScoreHistory.GetScoreFromSubTitle(title, 0, PageSize);
                this.ReloadChartScoreData();
            }
            this.ShowGridData();
            this.RefreshChart();
        }

        private void ShowDataFromSegment(long id)
        {
            this.ClearGridData();
            this.dataType.Cur = "SegmentId";
            this.dataType.SegmentId = id;
            this.dataType.EndDate = this.dataType.Date;
            this.ResultLabel.Text = "文段ID：" + id.ToString();
            this.totalCount = Glob.ScoreHistory.GetScoreCountFromSegmentId(id);

            if (this.TotalPage > 0)
            {
                this.currentPage = 1;
            }
            else
            {
                this.currentPage = 0;
            }

            this.UpdateGridToolBar();
            if (this.totalCount > 0)
            {
                this.currentScoreData = Glob.ScoreHistory.GetScoreFromSegmentId(id, 0, PageSize);
                this.ReloadChartScoreData();
            }
            this.ShowGridData();
            this.RefreshChart();
        }

        private void ReloadChartScoreData()
        {
            this.chartScoreData.Clear();
            if (this.totalCount <= 0)
            {
                return;
            }

            switch (this.dataType.Cur)
            {
                case "Date":
                    this.chartScoreData = this.dataType.Date == this.dataType.EndDate
                        ? Glob.ScoreHistory.GetScoreFromDate(this.dataType.Date, 0, this.totalCount)
                        : Glob.ScoreHistory.GetScoreFromDateRange(this.dataType.Date, this.dataType.EndDate, 0, this.totalCount);
                    break;
                case "Title":
                    this.chartScoreData = Glob.ScoreHistory.GetScoreFromTitle(this.dataType.Title, 0, this.totalCount);
                    break;
                case "SubTitle":
                    this.chartScoreData = Glob.ScoreHistory.GetScoreFromSubTitle(this.dataType.SubTitle, 0, this.totalCount);
                    break;
                case "SegmentId":
                    this.chartScoreData = Glob.ScoreHistory.GetScoreFromSegmentId(this.dataType.SegmentId, 0, this.totalCount);
                    break;
            }
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

        private void ConfigureTrendChartStyle(string labelFormat, bool showMarkers)
        {
            Series series = this.SpeedChart.Series[0];
            Axis axisX = this.SpeedChart.ChartAreas[0].AxisX;
            Axis axisY = this.SpeedChart.ChartAreas[0].AxisY;

            series.ChartType = SeriesChartType.FastLine;
            series.XValueType = ChartValueType.DateTime;
            series.MarkerStyle = showMarkers ? MarkerStyle.Circle : MarkerStyle.None;
            series.MarkerSize = showMarkers ? 4 : 0;
            axisX.Minimum = double.NaN;
            axisX.Maximum = double.NaN;
            axisX.Interval = 0;
            axisX.IntervalType = DateTimeIntervalType.Auto;
            axisX.LabelStyle.Format = labelFormat;
            axisX.LabelStyle.Angle = showMarkers ? -45 : 0;
            axisX.LabelStyle.Interval = 0;
            axisX.LabelStyle.IntervalType = DateTimeIntervalType.Auto;
            axisX.IsMarginVisible = true;
            axisY.Minimum = double.NaN;
            axisY.Maximum = double.NaN;
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

        private string GetTrendAxisFormat(DateTime startTime, DateTime endTime)
        {
            if (startTime.Date == endTime.Date)
            {
                return "HH:mm";
            }

            if (startTime.Year == endTime.Year)
            {
                return "MM-dd";
            }

            return "yy-MM";
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
            string labelFormat = this.GetTrendAxisFormat(
                Convert.ToDateTime(orderedRows[0]["score_time"]),
                Convert.ToDateTime(orderedRows[orderedRows.Count - 1]["score_time"]));
            bool showMarkers = orderedRows.Count <= 30;
            this.ConfigureTrendChartStyle(labelFormat, showMarkers);

            double minSpeed = double.MaxValue;
            foreach (StorageDataSet.ScoreRow row in orderedRows)
            {
                DateTime scoreTime = Convert.ToDateTime(row["score_time"]);
                double speedVal = this.GetDisplaySpeed(row);
                DataPoint point = new DataPoint();
                point.SetValueXY(scoreTime, speedVal);
                point.ToolTip = scoreTime.ToString("yyyy-MM-dd HH:mm:ss") + " 速度 " + speedVal.ToString("0.00");
                this.SpeedChart.Series[0].Points.Add(point);
                if (speedVal < minSpeed)
                {
                    minSpeed = speedVal;
                }
            }

            if (minSpeed < double.MaxValue)
            {
                this.SpeedChart.ChartAreas[0].AxisY.Minimum = Math.Max(0, Math.Floor(minSpeed / 10.0) * 10.0);
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
            if (this.showTrendChart)
            {
                this.ShowTrendChart();
            }
            else
            {
                this.ShowSelectedCurveChart(this.dataGridView1.CurrentRow);
            }
        }

        private void HistorySelectionChanged(object sender, EventArgs e)
        {
            DataGridViewRow curRow = (sender as DataGridView).CurrentRow;
            this.UpdatePreview(curRow);
            if (!this.showTrendChart)
            {
                this.ShowSelectedCurveChart(curRow);
            }
        }

        #region 表格右键菜单事件
        private void History_CellMoseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
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
                this.ShowDataFromTitle(articleTitle);
            }
        }

        private void SearchSegmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            long segmentId = this.gridHandler.GetSegmentId(this.currentScoreData);
            if (segmentId != -1)
            {
                this.ShowDataFromSegment(segmentId);
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

        #region 搜索标题框
        private void SearchButton_Click(object sender, EventArgs e)
        {
            string sText = this.SearchTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(sText))
            {
                this.ShowDataFromSubTitle(sText);
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                this.SearchButton.PerformClick();
            }
        }
        #endregion

        #region 跳转页数处理
        private void JumpPageHandler(int pageNum)
        {
            this.ClearGridData();
            this.currentPage = pageNum;
            this.PageNumTextBox.Text = pageNum.ToString();
            switch (this.dataType.Cur)
            {
                case "Date":
                    this.currentScoreData = this.dataType.Date == this.dataType.EndDate
                        ? Glob.ScoreHistory.GetScoreFromDate(this.dataType.Date, (pageNum - 1) * PageSize, PageSize)
                        : Glob.ScoreHistory.GetScoreFromDateRange(this.dataType.Date, this.dataType.EndDate, (pageNum - 1) * PageSize, PageSize);
                    break;
                case "Title":
                    this.currentScoreData = Glob.ScoreHistory.GetScoreFromTitle(this.dataType.Title, (pageNum - 1) * PageSize, PageSize);
                    break;
                case "SubTitle":
                    this.currentScoreData = Glob.ScoreHistory.GetScoreFromSubTitle(this.dataType.SubTitle, (pageNum - 1) * PageSize, PageSize);
                    break;
                case "SegmentId":
                    this.currentScoreData = Glob.ScoreHistory.GetScoreFromSegmentId(this.dataType.SegmentId, (pageNum - 1) * PageSize, PageSize);
                    break;
            }
            this.ReloadChartScoreData();
            this.ShowGridData();
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
                            this.totalCount--;
                            if (this.totalCount <= 0)
                            {
                                this.totalCount = 0;
                                this.currentPage = 0;
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
                        switch (this.dataType.Cur)
                        {
                            case "Date":
                                this.totalCount = this.dataType.Date == this.dataType.EndDate
                                    ? Glob.ScoreHistory.GetScoreCountFromDate(this.dataType.Date)
                                    : Glob.ScoreHistory.GetScoreCountFromDateRange(this.dataType.Date, this.dataType.EndDate);
                                break;
                            case "Title":
                                this.totalCount = Glob.ScoreHistory.GetScoreCountFromTitle(this.dataType.Title);
                                break;
                            case "SubTitle":
                                this.totalCount = Glob.ScoreHistory.GetScoreCountFromSubTitle(this.dataType.SubTitle);
                                break;
                            case "SegmentId":
                                this.totalCount = Glob.ScoreHistory.GetScoreCountFromSegmentId(this.dataType.SegmentId);
                                break;
                        }

                        if (this.totalCount == 0)
                        {
                            this.currentPage = 0;
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

                    this.totalCount -= deleteCount;
                    if (this.totalCount <= 0)
                    {
                        this.totalCount = 0;
                        this.currentPage = 0;
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
                    break;
                case DialogResult.No:
                    break;
            }
        }
        #endregion

        #region 选择日期
        private void MonthCalendar_DateSelected(object sender, DateRangeEventArgs e)
        {
            this.ShowDataFromDateRange(e.Start, e.End);
        }

        private void MonthCalendar_HigherViewRangeSelected(object sender, DateRangeEventArgs e)
        {
            this.ShowDataFromDateRange(e.Start, e.End);
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
                            this.ShowDataFromDateRange(this.dataType.Date, this.dataType.EndDate);
                        }
                        else
                        {
                            Glob.ScoreHistory.DeleteScoreItemByDate(dateVal);
                            this.ShowDataFromDate(this.dataType.Date);
                        }
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
                    this.ShowDataFromDate(DateTime.Now);
                    break;
                case DialogResult.No:
                    break;
            }
        }
        #endregion
    }
}
