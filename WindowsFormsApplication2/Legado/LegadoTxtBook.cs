using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WindowsFormsApplication2.Legado
{
    // Embedded copy of Legado's built-in txtTocRule.json. Disabled rules are only used by explicit choice.
    internal sealed class LegadoTxtBook
    {
        internal sealed record TocRule(string Name, string Pattern, bool Enabled);
        private static readonly IReadOnlyList<TocRule> Rules = LoadRules();
        internal static IReadOnlyList<TocRule> AvailableRules => Rules;
        // Java Pattern's \s and \d are ASCII by default; .NET treats them as Unicode.
        private static readonly Regex LeadingWhitespace = new Regex(@"^[\n\t\x0B\f\r ]+", RegexOptions.Compiled);

        private static IReadOnlyList<TocRule> LoadRules()
        {
            using var stream = typeof(LegadoTxtBook).Assembly.GetManifestResourceStream("LegadoTxtTocRules.json")
                ?? throw new InvalidDataException("缺少 Legado 内置 TXT 目录规则。");
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.EnumerateArray().Select(rule => new TocRule(
                rule.GetProperty("name").GetString(), rule.GetProperty("chapterRule").GetString(),
                rule.GetProperty("enable").GetBoolean())).ToArray();
        }

        internal static Regex CompileRule(TocRule rule)
        {
            return new Regex(NormalizeJavaClasses(rule.Pattern), RegexOptions.Multiline | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(3));
        }

        private static string NormalizeJavaClasses(string pattern)
        {
            var converted = new StringBuilder(pattern.Length);
            bool inClass = false;
            for (int i = 0; i < pattern.Length; i++)
            {
                char ch = pattern[i];
                if (ch == '\\' && i + 1 < pattern.Length)
                {
                    char next = pattern[++i];
                    string ascii = next switch
                    {
                        's' => @"\x09-\x0D\x20",
                        'd' => @"\x30-\x39",
                        'S' when !inClass => @"^\x09-\x0D\x20",
                        _ => null
                    };
                    if (ascii == null) converted.Append('\\').Append(next);
                    else if (inClass) converted.Append(ascii);
                    else converted.Append('[').Append(ascii).Append(']');
                }
                else
                {
                    if (ch == '[') inClass = true;
                    if (ch == ']') inClass = false;
                    converted.Append(ch);
                }
            }
            return converted.ToString();
        }

        private static TocRule ChooseRule(string content)
        {
            TocRule best = null;
            int maxNum = 1;
            foreach (var rule in Rules.Reverse().Where(rule => rule.Enabled && !string.IsNullOrEmpty(rule.Pattern)))
            {
                Regex regex;
                try { regex = CompileRule(rule); }
                catch (ArgumentException) { continue; }
                int start = 0;
                int count = 0;
                foreach (Match match in regex.Matches(content))
                {
                    if (start == 0 || match.Index - start > 1000)
                    {
                        count++;
                        start = match.Index + match.Length;
                    }
                }
                if (count >= maxNum) { maxNum = count; best = rule; }
            }
            return best;
        }

        internal static (string Text, string Charset) Decode(byte[] bytes)
        {
            if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe)
                return (new UnicodeEncoding(false, true, true).GetString(bytes, 2, bytes.Length - 2), "UTF-16LE");
            if (bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff)
                return (new UnicodeEncoding(true, true, true).GetString(bytes, 2, bytes.Length - 2), "UTF-16BE");
            int start = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            try { return (new UTF8Encoding(false, true).GetString(bytes, start, bytes.Length - start), "UTF-8"); }
            catch (DecoderFallbackException) { }
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            try
            {
                var encoding = Encoding.GetEncoding("GB18030", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                return (encoding.GetString(bytes), "GB18030");
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidDataException("TXT 既不是有效 UTF-8，也无法按 GB18030 解码；请核对书籍编码。", ex);
            }
        }

        internal sealed class Chapter
        {
            public int Index { get; init; }
            public string Title { get; init; }
            public string Content { get; init; }
            public string TypingText { get; private set; }
            private int[] originalOffsets;

            public void PrepareForTyping()
            {
                var text = new StringBuilder(Content.Length);
                var offsets = new List<int>();
                for (int i = 0; i < Content.Length; i++)
                {
                    if (char.IsWhiteSpace(Content[i])) continue;
                    offsets.Add(i);
                    text.Append(Content[i]);
                }
                offsets.Add(Content.Length);
                TypingText = text.ToString();
                originalOffsets = offsets.ToArray();
            }

            // Legado 的 durChapterPos 指向原文；跟打区不输入空白，但不能丢失原坐标。
            public int TypingPosition(int originalPosition)
            {
                int index = Array.BinarySearch(originalOffsets, originalPosition);
                return index >= 0 ? index : ~index;
            }

            public int OriginalPosition(int typingPosition) => originalOffsets[typingPosition];
        }

        public IReadOnlyList<Chapter> Chapters { get; }
        public string Charset { get; }
        public string RuleName { get; }

        private LegadoTxtBook(List<Chapter> chapters, string charset, string ruleName)
        {
            Chapters = chapters;
            Charset = charset;
            RuleName = ruleName;
        }

        public static LegadoTxtBook FromEpubChapters(
            IReadOnlyList<LegadoEpubToc.Entry> entries, IReadOnlyList<string> contents)
        {
            if (entries.Count != contents.Count)
                throw new ArgumentException("EPUB 目录与章节正文数量不一致。");
            var chapters = entries.Select((entry, index) => new Chapter
            {
                Index = index,
                Title = entry.Title,
                Content = contents[index] ?? string.Empty
            }).ToList();
            foreach (var chapter in chapters) chapter.PrepareForTyping();
            return new LegadoTxtBook(chapters, "EPUB", "EPUB");
        }

        public static LegadoTxtBook Parse(byte[] bytes, string ruleName = null)
        {
            var (text, charset) = Decode(bytes);
            TocRule rule = ruleName == null ? ChooseRule(text.Substring(0, Math.Min(text.Length, 512000)))
                : Rules.FirstOrDefault(item => item.Name == ruleName);
            if (rule?.Name == "默认分章规则")
                throw new NotSupportedException("Legado 无目录分章长度可由用户更改；仅凭进度文件无法推断章节边界，暂不允许同步。");
            if (rule == null || string.IsNullOrWhiteSpace(rule.Pattern))
                throw new InvalidOperationException("没有找到匹配的内置 TXT 目录规则；请确认 Legado 使用的目录规则。");
            var matches = CompileRule(rule).Matches(text).Cast<Match>().ToList();
            if (matches.Count < 2)
                throw new InvalidOperationException($"目录规则“{rule.Name}”未识别出足够章节，请核对 Legado 中的目录规则。");

            var chapters = new List<Chapter>();
            if (!string.IsNullOrWhiteSpace(text.Substring(0, matches[0].Index)))
                chapters.Add(new Chapter { Index = 0, Title = "前言", Content = Normalize(text.Substring(0, matches[0].Index)) });
            foreach (var match in matches.Select((value, index) => (value, index)))
            {
                int end = match.index + 1 < matches.Count ? matches[match.index + 1].Index : text.Length;
                chapters.Add(new Chapter
                {
                    Index = chapters.Count,
                    // .NET's multiline $ includes CR before LF; Java's $ stops before CRLF.
                    Title = match.value.Value.TrimEnd('\r'),
                    Content = Normalize(text.Substring(match.value.Index + match.value.Length, end - match.value.Index - match.value.Length))
                });
            }
            foreach (var chapter in chapters) chapter.PrepareForTyping();
            return new LegadoTxtBook(chapters, charset, rule.Name);
        }

        private static string Normalize(string content) => LeadingWhitespace.Replace(content, "　　");
    }
}
