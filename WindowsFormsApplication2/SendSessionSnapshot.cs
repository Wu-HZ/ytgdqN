using System.Collections.Generic;

namespace WindowsFormsApplication2
{
    public class SendSessionSnapshot
    {
        public string CurrentContent { get; set; } = "";

        public string CurrentInput { get; set; } = "";

        public string CurrentTitle { get; set; } = "";

        public int CurrentSegmentNum { get; set; }

        public long SentId { get; set; } = -1;

        public string ArticleFullText { get; set; } = "";

        public string SendFullText { get; set; } = "";

        public string SendType { get; set; } = "";

        public bool SingleDisorder { get; set; }

        public bool NoRepeat { get; set; }

        public int SendCount { get; set; }

        public int SendMark { get; set; }

        public int SentSegmentCount { get; set; }

        public bool IsCycle { get; set; }

        public int CycleValue { get; set; }

        public int CycleCounter { get; set; }

        public int ArticleSource { get; set; }

        public int SentCharCount { get; set; }

        public bool IsAuto { get; set; }

        public string PhraseSeparator { get; set; } = "";

        public bool PhraseDisorder { get; set; }

        public List<string> Phrases { get; set; } = new List<string>();

        public List<string> AllPhrases { get; set; } = new List<string>();

        public bool Trim { get; set; }

        public bool AutoCondition { get; set; }

        public string ConditionValue { get; set; } = "";

        public int AutoNo { get; set; }

        public List<string> SegmentRecord { get; set; } = new List<string>();

        public int SendCursor { get; set; }
    }
}
