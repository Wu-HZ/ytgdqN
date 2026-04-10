using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

namespace WindowsFormsApplication2
{
    public partial class About : Form
    {
        public About()
        {
            InitializeComponent();
            this.Text = string.Format("关于 {0}", AssemblyTitle);
            string bitVal;
            if (Glob.Bit.Length > 0)
            {
                bitVal = " (64-bit)";
            }
            else
            {
                bitVal = " (32-bit)";
            }
            this.labelVersion.Text = "v" + Glob.Ver + bitVal;
            AdjustDialogSize();
        }

        // 根据实际文本宽度扩展对话框，避免高 DPI 或字体差异导致右侧文字被截断。
        private void AdjustDialogSize()
        {
            const int rightPadding = 24;
            int bottomPadding = this.ClientSize.Height - button1.Bottom;
            int requiredWidth = new Control[]
            {
                label2,
                labelVersion,
                lblInfo,
                label4,
                label3,
                linkLabel1,
                linkLabel2
            }.Max(control => control.Right) + rightPadding;
            int requiredHeight = Math.Max(button1.Bottom, linkLabel2.Bottom) + bottomPadding;

            this.ClientSize = new Size(
                Math.Max(this.ClientSize.Width, requiredWidth),
                Math.Max(this.ClientSize.Height, requiredHeight));
            button1.Left = (this.ClientSize.Width - button1.Width) / 2;
            button1.Top = this.ClientSize.Height - bottomPadding - button1.Height;
        }

        private void Jump(string str)
        {
            try
            {
                Process.Start(new ProcessStartInfo(str) { UseShellExecute = true });
            }
            catch (Exception)
            {

            }
        }

        private DateTime Start = new DateTime(2021, 11, 25);
        private void Start_Load(object sender, EventArgs e)
        {
            var ts = DateTime.Now - Start;
            lblInfo.Text = string.Format("雨天跟打器从{0}发布至今已过去{1}天", Start.ToShortDateString(),
            ts.TotalDays.ToString("0"));
            AdjustDialogSize();
        }

        #region 程序集特性访问器
        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }
        #endregion

        private void LinkLabel1Clicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Jump("https://github.com/taliove/tygdq");
        }

        private void LinkLabel2Clicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Jump(Glob.HomeUrl);
        }

        private void OKClicked(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
