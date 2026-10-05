using System;
using System.IO;
using System.Text.Json;

namespace WindowsFormsApplication2.Legado
{
    // Only the book identity and segment length are persisted; chapter position always comes from WebDAV.
    internal sealed class LegadoBookResume
    {
        public string Url { get; set; }
        public string Directory { get; set; }
        public string Account { get; set; }
        public string FileName { get; set; }
        public string RuleName { get; set; }
        public int SegmentLength { get; set; }

        internal static LegadoBookResume Load(string path)
        {
            var resume = JsonSerializer.Deserialize<LegadoBookResume>(File.ReadAllText(path));
            if (resume == null || string.IsNullOrWhiteSpace(resume.Url) ||
                string.IsNullOrWhiteSpace(resume.Account) || string.IsNullOrWhiteSpace(resume.FileName) ||
                resume.SegmentLength < 1)
                throw new InvalidDataException("电子书恢复记录不完整。");
            return resume;
        }

        internal void Save(string path)
        {
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(this));
                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
