using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsFormsApplication2.History
{
    internal class HistoryMonthCalendar : MonthCalendar
    {
        private const int WmLButtonDown = 0x0201;
        private const int WmMouseMove = 0x0200;
        private const int WmLButtonUp = 0x0202;
        private const int WmLButtonDblClk = 0x0203;
        private const int McmFirst = 0x1000;
        private const int McmGetCurrentView = McmFirst + 22;
        private const int McmSetCurrentView = McmFirst + 32;
        private const int McmvMonth = 0;
        private const int McmvYear = 1;
        private const int McmvDecade = 2;
        private const int McmvCentury = 3;

        private bool suppressDateSelected = false;
        private DateTime lastClickDate = DateTime.MinValue;
        private int lastClickView = McmvMonth;
        private long lastClickTick = -1;
        private bool isHigherViewSelecting = false;
        private DateTime higherViewSelectionAnchor = DateTime.MinValue;
        private DateTime higherViewSelectionCurrent = DateTime.MinValue;
        private int higherViewSelectionView = McmvMonth;

        public event DateRangeEventHandler HigherViewRangeSelected;

        public int CurrentView
        {
            get
            {
                if (!this.IsHandleCreated)
                {
                    return McmvMonth;
                }

                return SendMessage(this.Handle, McmGetCurrentView, IntPtr.Zero, IntPtr.Zero).ToInt32();
            }
        }

        protected override void OnDateSelected(DateRangeEventArgs drevent)
        {
            if (this.suppressDateSelected)
            {
                this.suppressDateSelected = false;
                return;
            }

            base.OnDateSelected(drevent);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmLButtonDblClk && this.CurrentView > McmvMonth)
            {
                return;
            }

            if (m.Msg == WmLButtonDown && this.TryBeginHigherViewSelection(m.LParam))
            {
                return;
            }

            if (m.Msg == WmMouseMove && this.TryTrackHigherViewSelection(m.LParam, m.WParam))
            {
                return;
            }

            if (m.Msg == WmLButtonUp && this.TryEndHigherViewSelection(m.LParam))
            {
                return;
            }

            base.WndProc(ref m);
        }

        private bool TryBeginHigherViewSelection(IntPtr lParam)
        {
            int currentView = this.CurrentView;
            if (currentView <= McmvMonth)
            {
                this.ResetClickState();
                return false;
            }

            DateTime targetDate = this.GetHigherViewHitDate(lParam);
            if (targetDate == DateTime.MinValue)
            {
                this.ResetClickState();
                return false;
            }

            this.isHigherViewSelecting = true;
            this.higherViewSelectionAnchor = targetDate;
            this.higherViewSelectionCurrent = targetDate;
            this.higherViewSelectionView = currentView;
            this.ApplyHigherViewSelection(targetDate, targetDate, currentView);
            return true;
        }

        private bool TryTrackHigherViewSelection(IntPtr lParam, IntPtr wParam)
        {
            if (!this.isHigherViewSelecting)
            {
                return false;
            }

            if ((((long)wParam) & 0x0001) == 0)
            {
                return true;
            }

            DateTime targetDate = this.GetHigherViewHitDate(lParam);
            if (targetDate == DateTime.MinValue || targetDate == this.higherViewSelectionCurrent)
            {
                return true;
            }

            this.higherViewSelectionCurrent = targetDate;
            this.ApplyHigherViewSelection(this.higherViewSelectionAnchor, targetDate, this.higherViewSelectionView);
            return true;
        }

        private bool TryEndHigherViewSelection(IntPtr lParam)
        {
            if (!this.isHigherViewSelecting)
            {
                return false;
            }

            DateTime targetDate = this.GetHigherViewHitDate(lParam);
            if (targetDate != DateTime.MinValue)
            {
                this.higherViewSelectionCurrent = targetDate;
            }

            int currentView = this.higherViewSelectionView;
            DateTime startDate = this.higherViewSelectionAnchor;
            DateTime endDate = this.higherViewSelectionCurrent;
            this.isHigherViewSelecting = false;

            long now = Environment.TickCount64;
            bool isSingleSelection = startDate == endDate;
            bool isDoubleClick = isSingleSelection
                && this.lastClickTick >= 0
                && this.lastClickView == currentView
                && this.lastClickDate == endDate
                && now - this.lastClickTick <= SystemInformation.DoubleClickTime;

            if (isDoubleClick)
            {
                this.EnterLowerView(endDate, currentView);
                this.ResetClickState();
            }
            else
            {
                this.ApplyHigherViewSelection(startDate, endDate, currentView);
                this.RaiseHigherViewRangeSelected(startDate, endDate, currentView);
                if (isSingleSelection)
                {
                    this.lastClickDate = endDate;
                    this.lastClickView = currentView;
                    this.lastClickTick = now;
                }
                else
                {
                    this.ResetClickState();
                }
            }

            return true;
        }

        private DateTime GetHigherViewHitDate(IntPtr lParam)
        {
            MonthCalendar.HitTestInfo hitInfo = this.HitTest(GetXLParam(lParam), GetYLParam(lParam));
            if (hitInfo.HitArea != MonthCalendar.HitArea.Date)
            {
                return DateTime.MinValue;
            }

            return hitInfo.Time.Date;
        }

        private void ApplyHigherViewSelection(DateTime startDate, DateTime endDate, int currentView)
        {
            DateTime rangeStart;
            DateTime rangeEnd;
            this.GetHigherViewRange(startDate, endDate, currentView, out rangeStart, out rangeEnd);

            this.suppressDateSelected = true;
            this.SetSelectionRange(rangeStart, rangeEnd);
            this.SetCurrentView(currentView);
        }

        private void EnterLowerView(DateTime targetDate, int currentView)
        {
            this.suppressDateSelected = true;
            this.SetDate(targetDate);
            this.SetCurrentView(currentView - 1);
        }

        private void SetCurrentView(int view)
        {
            if (!this.IsHandleCreated)
            {
                return;
            }

            SendMessage(this.Handle, McmSetCurrentView, IntPtr.Zero, new IntPtr(view));
        }

        private void ResetClickState()
        {
            this.lastClickDate = DateTime.MinValue;
            this.lastClickView = McmvMonth;
            this.lastClickTick = -1;
        }

        private void GetHigherViewRange(DateTime startDate, DateTime endDate, int currentView, out DateTime rangeStart, out DateTime rangeEnd)
        {
            DateTime unitStart;
            DateTime unitEnd;
            DateTime currentUnitStart;
            DateTime currentUnitEnd;
            this.GetHigherViewUnitRange(startDate, currentView, out unitStart, out unitEnd);
            this.GetHigherViewUnitRange(endDate, currentView, out currentUnitStart, out currentUnitEnd);

            if (unitStart <= currentUnitStart)
            {
                rangeStart = unitStart;
                rangeEnd = currentUnitEnd;
            }
            else
            {
                rangeStart = currentUnitStart;
                rangeEnd = unitEnd;
            }
        }

        private void GetHigherViewUnitRange(DateTime targetDate, int currentView, out DateTime rangeStart, out DateTime rangeEnd)
        {
            switch (currentView)
            {
                case McmvYear:
                    rangeStart = new DateTime(targetDate.Year, targetDate.Month, 1);
                    rangeEnd = rangeStart.AddMonths(1).AddDays(-1);
                    break;
                case McmvDecade:
                    rangeStart = new DateTime(targetDate.Year, 1, 1);
                    rangeEnd = new DateTime(targetDate.Year, 12, 31);
                    break;
                case McmvCentury:
                    int decadeStartYear = (targetDate.Year / 10) * 10;
                    rangeStart = new DateTime(decadeStartYear, 1, 1);
                    rangeEnd = new DateTime(decadeStartYear + 9, 12, 31);
                    break;
                default:
                    rangeStart = targetDate.Date;
                    rangeEnd = targetDate.Date;
                    break;
            }
        }

        private void RaiseHigherViewRangeSelected(DateTime startDate, DateTime endDate, int currentView)
        {
            if (this.HigherViewRangeSelected == null)
            {
                return;
            }

            DateTime rangeStart;
            DateTime rangeEnd;
            this.GetHigherViewRange(startDate, endDate, currentView, out rangeStart, out rangeEnd);
            this.HigherViewRangeSelected(this, new DateRangeEventArgs(rangeStart, rangeEnd));
        }

        private static int GetXLParam(IntPtr lParam)
        {
            return unchecked((short)(long)lParam);
        }

        private static int GetYLParam(IntPtr lParam)
        {
            return unchecked((short)(((long)lParam >> 16) & 0xFFFF));
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
