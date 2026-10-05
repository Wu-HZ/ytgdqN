using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowsFormsApplication2.Legado
{
    // Mirrors TextFile.analyze() without a TOC: 512000-byte blocks and a byte
    // threshold derived from the user's maxLengthWithNoToc setting. Preview only:
    // the setting is not present in bookProgress JSON and must never be guessed
    // for WebDAV synchronization.
    internal static class LegadoNoTocPreview
    {
        internal sealed record Entry(string Title, string Content, int Start, int End);
        private const int BlockSize = 512000;
        private static readonly Regex LeadingWhitespace = new Regex(@"^[\n\t\x0B\f\r ]+",
            RegexOptions.Compiled);

        internal static IReadOnlyList<Entry> Parse(byte[] bytes, int maxLengthWithNoToc)
        {
            if (maxLengthWithNoToc < 3000 || maxLengthWithNoToc > 100000)
                throw new ArgumentOutOfRangeException(nameof(maxLengthWithNoToc),
                    "请填写 Legado 当前设置的无目录分章字数（3000–100000）。");
            if (bytes.Length < 3) throw new InvalidDataException("TXT 内容过短，无法分章。");
            var (_, charset) = LegadoTxtBook.Decode(bytes);
            Encoding encoding = charset switch
            {
                "UTF-16LE" => new UnicodeEncoding(false, true),
                "UTF-16BE" => new UnicodeEncoding(true, true),
                "GB18030" => Encoding.GetEncoding("GB18030"),
                _ => new UTF8Encoding(false)
            };
            int bytesPerChar = charset switch
            {
                "UTF-8" => 3,
                "UTF-16LE" or "UTF-16BE" or "GB18030" => 2,
                _ => 1
            };
            int maxBytes = maxLengthWithNoToc * bytesPerChar;
            bool bom = bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
            if (bom && bytes.Length == 3) throw new InvalidDataException("TXT 正文为空。");
            byte[] buffer = new byte[BlockSize];
            int bufferStart = bom ? 0 : 3;
            if (!bom) Array.Copy(bytes, buffer, 3);
            int nextInput = bom ? 3 : 3;
            int currentOffset = bom ? 3 : 0;
            int blockPos = 0, chapterPos = 0;
            var spans = new List<(string Title, int Start, int End)>();
            while (nextInput < bytes.Length)
            {
                int read = Math.Min(BlockSize - bufferStart, bytes.Length - nextInput);
                if (read == 0) throw new InvalidDataException("无目录分章缓冲区已满。");
                Array.Copy(bytes, nextInput, buffer, bufferStart, read);
                nextInput += read;
                blockPos++;
                int length = bufferStart + read;
                // Nothing remains buffered if an entire block becomes a chapter.
                // Legado leaves the old bufferStart in that case and can extend
                // the final chapter beyond EOF; preview must not read past input.
                bufferStart = 0;
                int remaining = length, chapterOffset = 0;
                chapterPos = 0;
                while (remaining > 0)
                {
                    chapterPos++;
                    if (remaining > maxBytes)
                    {
                        int end = length;
                        for (int i = chapterOffset + maxBytes; i < length; i++)
                            if (buffer[i] == 0x0a) { end = i; break; }
                        int start = spans.Count == 0 ? currentOffset : spans[^1].End;
                        spans.Add(($"第{blockPos}章({chapterPos})", start, start + end - chapterOffset));
                        remaining -= end - chapterOffset;
                        chapterOffset = end;
                    }
                    else
                    {
                        Array.Copy(buffer, length - remaining, buffer, 0, remaining);
                        bufferStart = remaining;
                        length -= remaining;
                        remaining = 0;
                    }
                }
                currentOffset += length;
            }
            if (bufferStart > 100 || spans.Count == 0)
            {
                int start = spans.Count == 0 ? currentOffset : spans[^1].End;
                spans.Add(($"第{blockPos}章({chapterPos})", start, start + bufferStart));
            }
            else
            {
                var last = spans[^1];
                spans[^1] = (last.Title, last.Start, last.End + bufferStart);
            }
            var result = new List<Entry>();
            foreach (var span in spans)
            {
                if (span.Start < 0 || span.End < span.Start || span.End > bytes.Length)
                    throw new InvalidDataException("无目录 TXT 章节范围无效。");
                string content = encoding.GetString(bytes, span.Start, span.End - span.Start);
                int title = content.IndexOf(span.Title, StringComparison.Ordinal);
                if (title >= 0) content = content[(title + span.Title.Length)..];
                result.Add(new Entry(span.Title, LeadingWhitespace.Replace(content, "　　"), span.Start, span.End));
            }
            return result;
        }
    }
}
