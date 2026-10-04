using System;

namespace WindowsFormsApplication2.Storage
{
    public sealed class WrongCharEvent
    {
        public int Index { get; set; }
        public string Character { get; set; }
        public string TypedCharacter { get; set; }
        public bool BackspaceCorrection { get; set; }
    }

    public sealed class TypingDetailRecord
    {
        public string ScoreTime { get; set; }
        public string TargetText { get; set; }
        public string TypedText { get; set; }
        public string TypeReportJson { get; set; }
        public string WrongCharsJson { get; set; }
    }
}
