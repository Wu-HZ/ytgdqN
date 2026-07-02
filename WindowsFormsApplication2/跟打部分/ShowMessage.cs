using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
namespace WindowsFormsApplication2
{
    public class ShowMessage
    {
        Label L_Message;
        Form frm;
        Timer dismissTimer;

        /// <summary>
        /// 初始化操作
        /// </summary>
        public ShowMessage(Size s, Point p, Form frm1)
        {
            frm = frm1;
        }

        /// <summary>
        /// 显示信息（作为父窗体内部控件，不创建独立窗口）
        /// </summary>
        /// <param name="text">需要显示的内容</param>
        public void Show(string text)
        {
            // 先移除已有的提示
            RemoveExistingTip();

            // 创建标签
            L_Message = new Label();
            L_Message.Name = "FlowTip";
            L_Message.AutoSize = true;
            L_Message.TextAlign = ContentAlignment.MiddleCenter;
            L_Message.BackColor = Color.FromArgb(95, 112, 122);
            L_Message.ForeColor = Color.FromArgb(236, 252, 255);
            L_Message.Font = new System.Drawing.Font("微软雅黑", 11f);
            L_Message.MaximumSize = new Size(frm.Width - 40, frm.Height);
            L_Message.AutoEllipsis = true;
            L_Message.Padding = new Padding(10, 5, 10, 5);
            L_Message.Text = text;
            L_Message.Click += L_Message_Click;
            L_Message.Paint += L_Message_Paint;

            // 添加到父窗体
            frm.Controls.Add(L_Message);
            L_Message.BringToFront();

            // 居中定位（水平居中，垂直在窗体上部约 1/4 处）
            int x = Math.Max(0, (frm.ClientSize.Width - L_Message.PreferredSize.Width) / 2);
            int y = Math.Max(10, frm.ClientSize.Height / 5);
            L_Message.Location = new Point(x, y);

            // 显示时长：短文本 2.5 秒，长文本适当延长
            int displayMs = text.Length > 15 ? 2500 + (text.Length - 15) * 200 : 2500;

            // 定时关闭
            dismissTimer = new Timer();
            dismissTimer.Interval = displayMs;
            dismissTimer.Tick += (s, ev) =>
            {
                RemoveExistingTip();
            };
            dismissTimer.Start();
        }

        /// <summary>
        /// 移除已有的浮动提示
        /// </summary>
        private void RemoveExistingTip()
        {
            if (dismissTimer != null)
            {
                dismissTimer.Stop();
                dismissTimer.Dispose();
                dismissTimer = null;
            }

            // 查找并移除已有的 FlowTip 标签
            var existing = frm.Controls.Find("FlowTip", false);
            foreach (Control c in existing)
            {
                if (c is Label lbl)
                {
                    lbl.Click -= L_Message_Click;
                    lbl.Paint -= L_Message_Paint;
                }
                frm.Controls.Remove(c);
                c.Dispose();
            }
        }

        void L_Message_Click(object sender, EventArgs e)
        {
            RemoveExistingTip();
        }

        void L_Message_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Label lbl = sender as Label;
            if (lbl == null) return;

            // 画渐变色覆盖层
            System.Drawing.Drawing2D.LinearGradientBrush lgb = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Point(0, 0), new Point(0, lbl.Height),
                Color.FromArgb(20, Color.White),
                Color.FromArgb(0, Color.FromArgb(80, 95, 103)));
            System.Drawing.Drawing2D.GraphicsPath gp = GetRoundedRectPath(
                new Rectangle(new Point(0, 0), lbl.Size), 5);
            g.FillPath(lgb, gp);

            // 画边框
            g.DrawRectangle(new Pen(Color.FromArgb(47, 56, 61)), 0, 0,
                e.ClipRectangle.Width - 1, e.ClipRectangle.Height - 1);
        }

        public static System.Drawing.Drawing2D.GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            int diameter = radius;
            Rectangle arcRect = new Rectangle(rect.Location, new Size(diameter, diameter));
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            // 左上角
            path.AddArc(arcRect, 180, 90);
            // 右上角
            arcRect.X = rect.Right - diameter;
            path.AddArc(arcRect, 270, 90);
            // 右下角
            arcRect.Y = rect.Bottom - diameter;
            path.AddArc(arcRect, 0, 90);
            // 左下角
            arcRect.X = rect.Left;
            path.AddArc(arcRect, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
