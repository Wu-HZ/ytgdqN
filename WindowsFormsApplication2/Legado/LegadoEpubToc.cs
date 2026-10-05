using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace WindowsFormsApplication2.Legado
{
    // EPUB text can be used for local typing, but its cloud durChapterPos is only an estimate
    // against this preview text. The EPUB path must never write progress back to WebDAV.
    internal static class LegadoEpubToc
    {
        internal sealed record Entry(string Title, string Href, bool IsVolume);

        internal static string ReadChapterText(byte[] bytes, string href, string nextHref = null)
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            if (href.StartsWith("epub-toc://", StringComparison.Ordinal))
                return "分卷目录项没有独立正文；不能作为跟打章节。";
            return ReadChapterText(zip, ReadSpine(zip), href, nextHref);
        }

        internal static IReadOnlyList<string> ReadChapterTexts(byte[] bytes,
            IReadOnlyList<Entry> chapters, int startIndex)
        {
            if (startIndex < 0 || startIndex >= chapters.Count)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var spine = ReadSpine(zip);
            var texts = Enumerable.Repeat(string.Empty, chapters.Count).ToArray();
            long totalLength = 0;
            for (int i = startIndex; i < chapters.Count; i++)
            {
                string href = chapters[i].Href;
                if (href.StartsWith("epub-toc://", StringComparison.Ordinal)) continue;
                string nextHref = chapters.Skip(i + 1)
                    .FirstOrDefault(chapter => !chapter.Href.StartsWith("epub-toc://", StringComparison.Ordinal))?.Href;
                texts[i] = ReadChapterText(zip, spine, href, nextHref);
                totalLength += texts[i].Length;
                if (totalLength > 32_000_000)
                    throw new InvalidDataException("所选章节之后的 EPUB 正文过大，无法安全载入跟打。");
            }
            return texts;
        }

        private static List<string> ReadSpine(ZipArchive zip)
        {
            var container = ReadXml(zip, "META-INF/container.xml");
            string opfPath = container.Descendants().FirstOrDefault(x => x.Name.LocalName == "rootfile")
                ?.Attribute("full-path")?.Value ?? throw new InvalidDataException("EPUB 缺少 OPF 根文件。");
            opfPath = Resolve("", opfPath);
            var opf = ReadXml(zip, opfPath);
            var resources = opf.Descendants().Where(x => x.Name.LocalName == "item")
                .ToDictionary(x => x.Attribute("id")?.Value ?? "", x => x);
            return opf.Descendants().Where(x => x.Name.LocalName == "itemref")
                .Select(x => x.Attribute("idref")?.Value)
                .Where(id => id != null && resources.ContainsKey(id))
                .Select(id => resources[id])
                .Where(x => x.Attribute("media-type")?.Value.Contains("htm", StringComparison.OrdinalIgnoreCase) == true)
                .Select(x => Resolve(opfPath, x.Attribute("href")?.Value ?? ""))
                .Where(x => zip.GetEntry(x) != null)
                .Distinct(StringComparer.Ordinal).ToList();
        }

        private static string ReadChapterText(ZipArchive zip, List<string> spine, string href, string nextHref)
        {
            string path = href.Split('#')[0];
            if (string.IsNullOrEmpty(path) || path.Contains("..") || path.Contains('\\'))
                throw new InvalidDataException("EPUB 章节地址不可读取。");
            int startIndex = spine.IndexOf(path);
            string nextPath = nextHref?.Split('#')[0];
            int endIndex = nextPath == null ? spine.Count : spine.IndexOf(nextPath);
            if (startIndex < 0 || (nextPath != null && endIndex < 0))
                throw new InvalidDataException("EPUB 章节资源不在正文 spine 内，无法安全预览完整章节。");
            if (endIndex < startIndex) throw new InvalidDataException("EPUB 章节资源顺序无效。");
            if (endIndex - startIndex > 64)
                throw new InvalidDataException("EPUB 单章跨越过多正文文件，已停止预览。");
            var sections = new List<string>();
            int totalLength = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                string text = ReadResourceText(zip, i == startIndex ? href : spine[i], null);
                if (text.Length > 0) sections.Add(text);
                totalLength += text.Length;
                if (totalLength > 2_000_000) throw new InvalidDataException("EPUB 单章预览内容过大。");
            }
            if (nextPath != null && nextHref.Contains('#'))
            {
                string text = ReadResourceText(zip, endIndex == startIndex ? href : nextPath, nextHref);
                if (text.Length > 0) sections.Add(text);
                totalLength += text.Length;
                if (totalLength > 2_000_000) throw new InvalidDataException("EPUB 单章预览内容过大。");
            }
            else if (endIndex == startIndex)
            {
                string text = ReadResourceText(zip, href, nextHref);
                if (text.Length > 0) sections.Add(text);
            }
            return string.Join("\n", sections);
        }

        private static string ReadResourceText(ZipArchive zip, string href, string endHref)
        {
            string path = href.Split('#')[0];
            var document = ReadXml(zip, path);
            var body = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "body")
                ?? throw new InvalidDataException("EPUB 章节缺少正文。");
            foreach (var hidden in body.Descendants().Where(x => x.Name.LocalName == "script" ||
                         x.Name.LocalName == "style").ToList()) hidden.Remove();
            string[] blocks = { "p", "h1", "h2", "h3", "h4", "h5", "h6", "li" };
            var paragraphs = body.DescendantsAndSelf().Where(x => blocks.Contains(x.Name.LocalName) &&
                !x.Ancestors().Any(parent => blocks.Contains(parent.Name.LocalName))).ToList();
            int start = FragmentBoundary(body, paragraphs, href, 0);
            int end = endHref?.Split('#')[0] == path
                ? FragmentBoundary(body, paragraphs, endHref, paragraphs.Count) : paragraphs.Count;
            if (end < start) throw new InvalidDataException("EPUB 章节锚点顺序无效。");
            return string.Join("\n", paragraphs.Skip(start).Take(end - start)
                .Select(x => x.Value.Trim()).Where(x => x.Length > 0));
        }

        private static int FragmentBoundary(XElement body, List<XElement> paragraphs,
            string href, int noFragment)
        {
            int marker = href.IndexOf('#');
            if (marker < 0) return noFragment;
            string id = Uri.UnescapeDataString(href[(marker + 1)..]);
            var anchor = body.DescendantsAndSelf().FirstOrDefault(x => x.Attribute("id")?.Value == id);
            if (anchor == null) throw new InvalidDataException("EPUB 章节锚点不存在：" + id);
            int index = paragraphs.FindIndex(x => x == anchor || x.Descendants().Contains(anchor) ||
                XNode.DocumentOrderComparer.Compare(x, anchor) > 0);
            return index < 0 ? paragraphs.Count : index;
        }

        internal static string ReadBookTitle(byte[] bytes)
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var container = ReadXml(zip, "META-INF/container.xml");
            string path = container.Descendants().FirstOrDefault(x => x.Name.LocalName == "rootfile")
                ?.Attribute("full-path")?.Value ?? throw new InvalidDataException("EPUB 缺少 OPF 根文件。");
            var opf = ReadXml(zip, Resolve("", path));
            return opf.Descendants().FirstOrDefault(x => x.Name.LocalName == "metadata")
                ?.Descendants().FirstOrDefault(x => x.Name.LocalName == "title")?.Value?.Trim();
        }

        internal static IReadOnlyList<Entry> Parse(byte[] bytes)
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var container = ReadXml(zip, "META-INF/container.xml");
            string opfPath = container.Descendants().FirstOrDefault(x => x.Name.LocalName == "rootfile")
                ?.Attribute("full-path")?.Value ?? throw new InvalidDataException("EPUB 缺少 OPF 根文件。");
            opfPath = Resolve("", opfPath);
            var opf = ReadXml(zip, opfPath);
            var manifest = opf.Descendants().Where(x => x.Name.LocalName == "item")
                .Select(x => new
                {
                    Id = x.Attribute("id")?.Value,
                    Href = x.Attribute("href")?.Value,
                    Media = x.Attribute("media-type")?.Value,
                    Properties = x.Attribute("properties")?.Value ?? ""
                }).ToList();
            string tocId = opf.Descendants().FirstOrDefault(x => x.Name.LocalName == "spine")?.Attribute("toc")?.Value;
            bool epub3 = (opf.Root?.Attribute("version")?.Value ?? "").StartsWith("3.", StringComparison.Ordinal);
            var nav = manifest.FirstOrDefault(x => x.Properties.Split(' ').Contains("nav"));
            if (epub3 && nav?.Href != null)
            {
                var navPath = Resolve(opfPath, nav.Href);
                var doc = ReadXml(zip, navPath);
                var tocNav = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "nav" &&
                    x.Attributes().Any(a => a.Name.LocalName == "type" && a.Value.Split(' ').Contains("toc")));
                if (tocNav != null)
                {
                    var items = new List<Entry>();
                    foreach (var ol in tocNav.Elements().Where(x => x.Name.LocalName == "ol"))
                        AddNavItems(ol, navPath, items, "");
                    if (items.Count > 0) return PrependFirstPages(zip, opfPath, opf, items);
                }
            }
            var ncx = manifest.FirstOrDefault(x => x.Id == tocId) ??
                manifest.FirstOrDefault(x => x.Media == "application/x-dtbncx+xml");
            if (ncx?.Href != null)
            {
                string ncxPath = Resolve(opfPath, ncx.Href);
                var doc = ReadXml(zip, ncxPath);
                var map = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "navMap");
                var items = new List<Entry>();
                if (map != null) AddNcxItems(map, ncxPath, items, "");
                if (items.Count > 0) return PrependFirstPages(zip, opfPath, opf, items);
            }
            // EPUBs with no TOC: surface spine titles for inspection, but not for progress sync.
            var spine = opf.Descendants().Where(x => x.Name.LocalName == "itemref")
                .Select(x => x.Attribute("idref")?.Value)
                .Select(id => manifest.FirstOrDefault(x => x.Id == id))
                .Where(x => x?.Href != null && x.Media?.Contains("html") == true)
                .Select(x => new Entry(x.Href, Resolve(opfPath, x.Href), false)).ToList();
            if (spine.Count == 0) throw new InvalidDataException("EPUB 无可读取的导航或章节。");
            return spine;
        }

        private static IReadOnlyList<Entry> PrependFirstPages(ZipArchive zip, string opfPath,
            XDocument opf, List<Entry> items)
        {
            // EpubFile.parseFirstPage() inserts XHTML contents before the first
            // TOC resource; those entries shift all subsequent Legado indexes.
            string first = items.FirstOrDefault(x => !x.Href.StartsWith("epub-toc://", StringComparison.Ordinal))
                ?.Href.Split('#')[0];
            if (first == null) return items;
            var prefix = new List<Entry>();
            foreach (var reference in opf.Descendants().Where(x => x.Name.LocalName == "itemref"))
            {
                string id = reference.Attribute("idref")?.Value;
                var resource = opf.Descendants().FirstOrDefault(x => x.Name.LocalName == "item" &&
                    x.Attribute("id")?.Value == id);
                if (resource?.Attribute("href")?.Value is not string href ||
                    resource.Attribute("media-type")?.Value.Contains("htm", StringComparison.OrdinalIgnoreCase) != true)
                    continue;
                string path = Resolve(opfPath, href);
                if (path == first) break;
                string title = "--卷首--";
                try
                {
                    var doc = ReadXml(zip, path);
                    string candidate = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "title")?.Value?.Trim();
                    if (!string.IsNullOrEmpty(candidate)) title = candidate;
                }
                catch (XmlException) { /* Legado also accepts non-well-formed HTML. */ }
                prefix.Add(new Entry(title, path, false));
            }
            prefix.AddRange(items);
            return prefix;
        }

        private static void AddNavItems(XElement ol, string path, List<Entry> entries, string parentIndex)
        {
            int index = 0;
            foreach (var li in ol.Elements().Where(x => x.Name.LocalName == "li"))
            {
                var link = li.Elements().FirstOrDefault(x => x.Name.LocalName == "a");
                var title = link ?? li.Elements().FirstOrDefault(x => x.Name.LocalName == "span");
                var nested = li.Elements().Where(x => x.Name.LocalName == "ol").ToList();
                string itemIndex = parentIndex.Length == 0 ? index.ToString() : parentIndex + "/" + index;
                if (title != null)
                    entries.Add(new Entry(title.Value.Trim(), link?.Attribute("href")?.Value is string href
                        ? Resolve(path, href) : "epub-toc://" + itemIndex, nested.Count > 0 || link == null));
                foreach (var child in nested) AddNavItems(child, path, entries, itemIndex);
                index++;
            }
        }

        private static void AddNcxItems(XElement parent, string path, List<Entry> entries, string parentIndex)
        {
            int index = 0;
            foreach (var point in parent.Elements().Where(x => x.Name.LocalName == "navPoint"))
            {
                string title = point.Elements().FirstOrDefault(x => x.Name.LocalName == "navLabel")
                    ?.Descendants().FirstOrDefault(x => x.Name.LocalName == "text")?.Value?.Trim() ?? "";
                string href = point.Elements().FirstOrDefault(x => x.Name.LocalName == "content")?.Attribute("src")?.Value;
                string itemIndex = parentIndex.Length == 0 ? index.ToString() : parentIndex + "/" + index;
                entries.Add(new Entry(title, href == null ? "epub-toc://" + itemIndex : Resolve(path, href),
                    point.Elements().Any(x => x.Name.LocalName == "navPoint") || href == null));
                AddNcxItems(point, path, entries, itemIndex);
                index++;
            }
        }

        private static string Resolve(string parent, string href)
        {
            string fragment = href.Contains('#') ? href.Substring(href.IndexOf('#')) : "";
            string path = Uri.UnescapeDataString(href.Split('#')[0]);
            if (path.Length == 0 && fragment.Length > 0) return parent + fragment;
            if (Uri.TryCreate(path, UriKind.Absolute, out _) || path.StartsWith('/') || path.Contains('\\') || path.Contains('?'))
                throw new InvalidDataException("EPUB 包含不支持的绝对路径或外部章节链接。");
            var segments = new List<string>(parent.Split('/').SkipLast(1));
            foreach (var segment in path.Split('/'))
            {
                if (segment == "..")
                {
                    if (segments.Count == 0) throw new InvalidDataException("EPUB 章节路径越界。");
                    segments.RemoveAt(segments.Count - 1);
                }
                else if (segment.Length > 0 && segment != ".") segments.Add(segment);
            }
            return string.Join("/", segments) + fragment;
        }

        private static XDocument ReadXml(ZipArchive zip, string path)
        {
            var entry = zip.GetEntry(path) ?? throw new InvalidDataException("EPUB 缺少文件：" + path);
            if (entry.Length > 2_000_000) throw new InvalidDataException("EPUB 导航文件过大。");
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > 2_000_000) throw new InvalidDataException("EPUB 导航文件过大。");
                buffer.Write(chunk, 0, read);
            }
            buffer.Position = 0;
            using var reader = XmlReader.Create(buffer, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            });
            return XDocument.Load(reader);
        }
    }
}
