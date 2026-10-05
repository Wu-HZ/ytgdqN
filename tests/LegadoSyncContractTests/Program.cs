using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using WindowsFormsApplication2.Legado;

var secret = "本机密码:paß🔒";
var protectedSecret = LegadoPasswordProtection.Protect(secret);
Check(protectedSecret != secret && !protectedSecret.Contains("paß") &&
    LegadoPasswordProtection.Unprotect(protectedSecret) == secret, "DPAPI 当前用户加密保存并恢复密码");
try { LegadoPasswordProtection.Unprotect("not-base64"); throw new Exception("无效密文被接受"); }
catch (FormatException) { }
string resumePath = Path.Combine(Path.GetTempPath(), "legado-resume-" + Guid.NewGuid().ToString("N") + ".json");
try
{
    new LegadoBookResume { Url = "https://example.test/dav/", Directory = "Legado", Account = "user",
        FileName = "目录/书名.txt", SegmentLength = 200 }.Save(resumePath);
    var resumed = LegadoBookResume.Load(resumePath);
    Check(resumed.FileName == "目录/书名.txt" && resumed.SegmentLength == 200 &&
        !File.ReadAllText(resumePath).Contains("secret") &&
        !File.ReadAllText(resumePath).Contains("ChapterPos"), "恢复记录只保存书籍身份和段长，不存密码/本地进度");
    File.WriteAllText(resumePath, "{\"SegmentLength\":0}");
    try { LegadoBookResume.Load(resumePath); throw new Exception("损坏的恢复记录被接受"); }
    catch (InvalidDataException) { }
}
finally { File.Delete(resumePath); }
Check(LegadoTxtBook.AvailableRules.Count == 27 && LegadoTxtBook.AvailableRules.Count(rule => rule.Enabled) == 12,
    "Legado 内置 27 条规则及启用状态");
foreach (var rule in LegadoTxtBook.AvailableRules.Where(rule => !string.IsNullOrEmpty(rule.Pattern)))
{
    try { LegadoTxtBook.CompileRule(rule); }
    catch (Exception ex) { throw new Exception("目录规则无法编译：" + rule.Name, ex); }
}
const string fixture = "书名\n作者\n第1章 开篇\n　　甲乙丙\n第2章 后续\n　　丁戊己";
var book = LegadoTxtBook.Parse(Encoding.UTF8.GetBytes(fixture));
byte[] epubBytes;
using (var stream = new MemoryStream())
{
    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
    {
        AddZipFile(zip, "META-INF/container.xml", "<container><rootfiles><rootfile full-path='OEBPS/book.opf'/></rootfiles></container>");
        AddZipFile(zip, "OEBPS/book.opf", "<package version='3.0'><metadata><dc:title xmlns:dc='http://purl.org/dc/elements/1.1/'>书名</dc:title></metadata><manifest><item id='toc' href='nav.xhtml' properties='nav' media-type='application/xhtml+xml' /><item id='pre' href='text/pre.xhtml' media-type='application/xhtml+xml'/><item id='one' href='text/one.xhtml' media-type='application/xhtml+xml'/><item id='two' href='text/two.xhtml' media-type='application/xhtml+xml'/></manifest><spine><itemref idref='pre'/><itemref idref='one'/><itemref idref='two'/></spine></package>");
        AddZipFile(zip, "OEBPS/nav.xhtml", "<html xmlns:epub='http://www.idpf.org/2007/ops'><body><nav epub:type='toc'><ol><li><span>第一卷</span><ol><li><a href='text/one.xhtml'>第一章</a><ol><li><a href='text/two.xhtml#part'>第二章</a></li></ol></li></ol></li></ol></nav></body></html>");
        AddZipFile(zip, "OEBPS/text/pre.xhtml", "<html><head><title>序言</title></head><body><p>起始页</p></body></html>");
        AddZipFile(zip, "OEBPS/text/one.xhtml", "<html><body><h1>第一章</h1><p>甲乙</p><script>不要显示</script></body></html>");
    }
    epubBytes = stream.ToArray();
}
using (var stream = new MemoryStream())
{
    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
    {
        AddZipFile(zip, "META-INF/container.xml", "<container><rootfiles><rootfile full-path='OPS/book.opf'/></rootfiles></container>");
        AddZipFile(zip, "OPS/book.opf", "<package version='2.0'><manifest><item id='toc' href='toc.ncx' media-type='application/x-dtbncx+xml'/><item id='front' href='front.xhtml' media-type='application/xhtml+xml'/><item id='body' href='body.xhtml' media-type='application/xhtml+xml'/></manifest><spine toc='toc'><itemref idref='front'/><itemref idref='body'/></spine></package>");
        AddZipFile(zip, "OPS/toc.ncx", "<ncx><navMap><navPoint><navLabel><text>正文</text></navLabel><content src='body.xhtml#start'/></navPoint></navMap></ncx>");
        AddZipFile(zip, "OPS/front.xhtml", "<html><head><title>扉页</title></head><body>书前</body></html>");
        AddZipFile(zip, "OPS/body.xhtml", "<html><body><h1 id='start'>正文</h1></body></html>");
    }
    var ncxEntries = LegadoEpubToc.Parse(stream.ToArray());
    Check(ncxEntries.Count == 2 && ncxEntries[0].Title == "扉页" &&
        ncxEntries[1].Href == "OPS/body.xhtml#start", "EPUB2 NCX 与卷首索引偏移");
}
var epubToc = LegadoEpubToc.Parse(epubBytes);
Check(epubToc.Count == 4 && epubToc[0].Title == "序言" && epubToc[1].Title == "第一卷" &&
    epubToc[1].Href == "epub-toc://0" && epubToc[1].IsVolume &&
    epubToc[2].Title == "第一章" && epubToc[2].IsVolume &&
    epubToc[3].Href == "OEBPS/text/two.xhtml#part", "EPUB3 卷首与无资源分卷占用 Legado 章节索引");
Check(LegadoEpubToc.ReadBookTitle(epubBytes) == "书名", "EPUB 元数据书名用于查找云端进度");
var epubTransport = new FakeDav { Remote = new LegadoProgress { Name = "书名", Author = "",
    ChapterIndex = 2, ChapterTitle = "第一章", ChapterPos = 42 } };
using (var epubClient = new LegadoWebDavClient("https://example.test/dav", "Legado", "user", "secret", epubTransport))
{
    var epubProgress = await epubClient.ReadEpubProgressAsync("书名", epubToc);
    Check(epubProgress?.ChapterIndex == 2 && epubProgress.ChapterPos == 42 && epubTransport.Writes == 0,
        "EPUB 只读取进度，标题和目录索引匹配，不写回");
    try { await epubClient.UploadIfForwardAsync(epubProgress, book);
        throw new Exception("只读 EPUB 进度意外授权了 TXT 写回"); }
    catch (InvalidOperationException) { }
    epubTransport.Remote.ChapterTitle = "不匹配";
    Check(await epubClient.ReadEpubProgressAsync("书名", epubToc) == null && epubTransport.Writes == 0,
        "EPUB 标题不匹配时不采纳云端坐标");
    epubTransport.Remote.ChapterTitle = "第一章";
    epubTransport.Remote.ChapterIndex = epubToc.Count;
    Check(await epubClient.ReadEpubProgressAsync("书名", epubToc) == null && epubTransport.Writes == 0,
        "EPUB 目录索引越界时仍不得读取为可靠进度");
}
Check(LegadoEpubToc.ReadChapterText(epubBytes, epubToc[2].Href) == "第一章\n甲乙",
    "EPUB 正文抽取去除脚本，供本地跟打使用");
try { LegadoEpubToc.ReadChapterText(epubBytes, epubToc[2].Href, epubToc[3].Href);
    throw new Exception("缺失后续 spine 资源被误认为完整章节"); }
catch (InvalidDataException) { }
using (var stream = new MemoryStream())
{
    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
    {
        AddZipFile(zip, "META-INF/container.xml", "<container><rootfiles><rootfile full-path='content.opf'/></rootfiles></container>");
        AddZipFile(zip, "content.opf", "<package version='3.0'><manifest><item id='nav' href='nav.xhtml' properties='nav' media-type='application/xhtml+xml'/><item id='body' href='body.xhtml' media-type='application/xhtml+xml'/></manifest><spine><itemref idref='body'/></spine></package>");
        AddZipFile(zip, "nav.xhtml", "<html xmlns:epub='http://www.idpf.org/2007/ops'><body><nav epub:type='toc'><ol><li><a href='body.xhtml#one'>一</a></li><li><a href='body.xhtml#two'>二</a></li></ol></nav></body></html>");
        AddZipFile(zip, "body.xhtml", "<html><body><p>卷前</p><h1 id='one'>一</h1><p>第一段</p><h1 id='two'>二</h1><p>第二段</p></body></html>");
    }
    var fragmentBytes = stream.ToArray();
    var fragmentToc = LegadoEpubToc.Parse(fragmentBytes);
    Check(fragmentToc.Count == 2 && fragmentToc[0].Href == "body.xhtml#one",
        "EPUB 同一资源多章节目录");
    Check(LegadoEpubToc.ReadChapterText(fragmentBytes, fragmentToc[0].Href, fragmentToc[1].Href) == "一\n第一段" &&
        LegadoEpubToc.ReadChapterText(fragmentBytes, fragmentToc[1].Href) == "二\n第二段",
        "EPUB 同文件锚点区间预览不能重复整页正文");
    try { LegadoEpubToc.ReadChapterText(fragmentBytes, "body.xhtml#missing");
        throw new Exception("缺失锚点的 EPUB 章节被误认为整页正文"); }
    catch (InvalidDataException) { }
}
using (var stream = new MemoryStream())
{
    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
    {
        AddZipFile(zip, "META-INF/container.xml", "<container><rootfiles><rootfile full-path='book.opf'/></rootfiles></container>");
        AddZipFile(zip, "book.opf", "<package version='3.0'><manifest><item id='nav' href='nav.xhtml' properties='nav' media-type='application/xhtml+xml'/><item id='a' href='one.xhtml' media-type='application/xhtml+xml'/><item id='b' href='two.xhtml' media-type='application/xhtml+xml'/><item id='c' href='three.xhtml' media-type='application/xhtml+xml'/></manifest><spine><itemref idref='a'/><itemref idref='b'/><itemref idref='c'/></spine></package>");
        AddZipFile(zip, "nav.xhtml", "<html xmlns:epub='http://www.idpf.org/2007/ops'><body><nav epub:type='toc'><ol><li><a href='one.xhtml#begin'>第一章</a></li><li><a href='three.xhtml#next'>第二章</a></li></ol></nav></body></html>");
        AddZipFile(zip, "one.xhtml", "<html><body><p>卷首</p><h1 id='begin'>章一</h1><p>正文A</p></body></html>");
        AddZipFile(zip, "two.xhtml", "<html><body><p>正文B</p></body></html>");
        AddZipFile(zip, "three.xhtml", "<html><body><p>过渡页</p><h1 id='next'>章二</h1><p>正文C</p></body></html>");
    }
    var pages = LegadoEpubToc.Parse(stream.ToArray());
    Check(pages.Count == 2 &&
        LegadoEpubToc.ReadChapterText(stream.ToArray(), pages[0].Href, pages[1].Href) ==
        "章一\n正文A\n正文B\n过渡页", "EPUB 跨 spine 资源与下一章起始锚点前内容");
    var pageTexts = LegadoEpubToc.ReadChapterTexts(stream.ToArray(), pages, 0);
    var epubTypingBook = LegadoTxtBook.FromEpubChapters(pages, pageTexts);
    var selectedTexts = LegadoEpubToc.ReadChapterTexts(stream.ToArray(), pages, 1);
    Check(pageTexts.SequenceEqual(new[] { "章一\n正文A\n正文B\n过渡页", "章二\n正文C" }) &&
        selectedTexts[0] == "" && selectedTexts[1] == pageTexts[1] &&
        epubTypingBook.Chapters[1].TypingText == "章二正文C" &&
        epubTypingBook.Chapters[1].OriginalPosition(2) == 3,
        "EPUB 按目录索引预载所选章及后续正文，并保留估算位置映射");
}

using (var stream = new MemoryStream())
{
    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
    {
        AddZipFile(zip, "META-INF/container.xml", "<container><rootfile full-path='book.opf'/></container>");
        AddZipFile(zip, "book.opf", "<package><manifest><item id='ncx' href='toc.ncx' media-type='application/x-dtbncx+xml'/></manifest><spine toc='ncx'/></package>");
        AddZipFile(zip, "toc.ncx", "<ncx><navMap><navPoint><navLabel><text>卷一</text></navLabel><content src='a.xhtml'/><navPoint><navLabel><text>第1章</text></navLabel><content src='b.xhtml#section'/></navPoint></navPoint></navMap></ncx>");
    }
    var ncxToc = LegadoEpubToc.Parse(stream.ToArray());
    Check(ncxToc.Count == 2 && ncxToc[0].IsVolume && ncxToc[1].Href == "b.xhtml#section",
        "EPUB2 NCX 嵌套目录只读预览");
}
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var gbBook = LegadoTxtBook.Parse(Encoding.GetEncoding("GB18030").GetBytes(fixture));
var crlfBook = LegadoTxtBook.Parse(Encoding.UTF8.GetBytes(fixture.Replace("\n", "\r\n")));
Check(crlfBook.Chapters[1].Title == book.Chapters[1].Title, "CRLF 标题不得携带回车导致进度匹配失败");
Check(gbBook.Chapters.Count == book.Chapters.Count && gbBook.Chapters[1].Title == book.Chapters[1].Title &&
    gbBook.Chapters[1].Content == book.Chapters[1].Content, "GB18030 / GBK TXT 与 UTF-8 章节位置等价");
var utf16Book = LegadoTxtBook.Parse(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(fixture)).ToArray());
Check(utf16Book.Chapters[1].Title == book.Chapters[1].Title, "UTF-16 BOM TXT 章节解析");
var explicitRule = LegadoTxtBook.Parse(Encoding.UTF8.GetBytes("第一回 开始\n内容\n第二回 后续\n内容"), "目录(古典、轻小说备用)");
Check(explicitRule.Chapters.Count > 0, "禁用的内置规则可显式选择");
try { LegadoTxtBook.Parse(Encoding.UTF8.GetBytes(fixture), "默认分章规则");
    throw new Exception("兜底分章规则在未知 Legado 用户分章长度时仍可上传"); }
catch (NotSupportedException) { }
var splitText = new string('a', 9001) + "\n" + new string('b', 9002) + "\n" + new string('c', 150);
var splitChapters = LegadoNoTocPreview.Parse(Encoding.UTF8.GetBytes(splitText), 3000);
Check(splitChapters.Count == 3 && splitChapters[0].Title == "第1章(1)" &&
    splitChapters[0].Start == 0 && splitChapters[0].End == 9001 &&
    splitChapters[1].Start == 9001 && splitChapters[1].End == 18004 &&
    splitChapters[2].End == Encoding.UTF8.GetByteCount(splitText), "无目录 3000 字字节阈值与换行位置");
var utf16NoToc = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(
    new string('中', 3200) + "\n" + new string('中', 1800))).ToArray();
var utf16Split = LegadoNoTocPreview.Parse(utf16NoToc, 3000);
Check(utf16Split.Count == 2 && utf16Split[0].Start == 0 &&
    utf16Split[0].End == 6402 && utf16Split[1].End == utf16NoToc.Length,
    "无目录 UTF-16 按字节边界分章；仅 UTF-8 BOM 会被 Legado 跳过");
var noNewline = LegadoNoTocPreview.Parse(Encoding.UTF8.GetBytes(new string('a', 9100)), 3000);
Check(noNewline.Count == 1 && noNewline[0].End == 9100,
    "无换行文本只读预览不越界，不复现 Legado 遗留 bufferStart 越界");
var blockSplit = LegadoNoTocPreview.Parse(Encoding.UTF8.GetBytes(new string('a', 520000)), 3000);
Check(blockSplit.Count == 2 && blockSplit[0].End == 512000 &&
    blockSplit[1].Start == 512000 && blockSplit[1].Title == "第2章(1)" &&
    blockSplit[1].End == 520000, "跨 512000 字节块的无目录分章编号");
try { LegadoNoTocPreview.Parse(Encoding.UTF8.GetBytes(splitText), 2999);
    throw new Exception("无目录未知分章长度被接受"); }
catch (ArgumentOutOfRangeException) { }
Check(book.Chapters.Count == 3 && book.Chapters[1].Title == "第1章 开篇", "前言和章节索引");
Check(book.Chapters[1].Content.StartsWith("　　　　甲乙丙"), "Legado Java 空白规则转换");
Check(book.Chapters[1].TypingText == "甲乙丙" && book.Chapters[1].TypingPosition(0) == 0 &&
    book.Chapters[1].OriginalPosition(0) == 4 && book.Chapters[1].OriginalPosition(1) == 5 &&
    book.Chapters[1].OriginalPosition(2) == 6,
    "跳过空白后仍能还原 Legado 原文坐标");
var transport = new FakeDav
{
    Remote = new LegadoProgress { Name = "书名", Author = "", ChapterIndex = 1, ChapterPos = 0,
        ChapterTitle = "第1章 开篇", ChapterTime = 1 }
};
using var client = new LegadoWebDavClient("https://example.test/dav", "Legado", "user", "secret", transport);
Check(Encoding.UTF8.GetString(await client.DownloadBookAsync("书名.txt")) == "test", "books/ 下载");
var listedBooks = await client.ListBookFilesAsync();
Check(listedBooks.Count == 4 && listedBooks.Contains("书名.txt") && listedBooks.Contains("目录/内层书.txt") &&
    listedBooks.Contains("更多/另一本.txt") && listedBooks.Contains("示例.epub") &&
    !listedBooks.Contains("示例.umd") && !listedBooks.Contains("示例.mobi"),
    "Depth:1 仅列出 TXT 和 EPUB");
Check(transport.MaxConcurrentFolderRequests > 1 && transport.OnlyUsedDepthOne &&
    client.LastBookListingRequestCount == 3, "Depth:1 请求并发且不触发服务端无限深度遍历");
string cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
try
{
    var cache = new LegadoBookCache(cacheDir);
    var first = await cache.GetOrDownloadAsync(client, "书名.txt");
    int downloadCount = transport.BookDownloads;
    var second = await cache.GetOrDownloadAsync(client, "书名.txt");
    Check(!first.FromCache && second.FromCache && transport.BookDownloads == downloadCount,
        "二次打开命中本地缓存，不重复下载");
    var refreshed = await cache.GetOrDownloadAsync(client, "书名.txt", true);
    Check(!refreshed.FromCache && transport.BookDownloads == downloadCount + 1, "显式更新缓存重新下载");
}
finally
{
    if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
}
var current = await client.FindProgressAsync("书名", book);
Check(current.ChapterIndex == 1 && current.Author == "", "云端进度匹配（空作者）");
var laterTransport = new FakeDav { Remote = new LegadoProgress { Name = "书名", Author = "",
    ChapterIndex = 2, ChapterPos = 0, ChapterTitle = "第2章 后续", ChapterTime = 10 } };
using (var reopenedClient = new LegadoWebDavClient("https://example.test/dav", "Legado", "user", "secret", laterTransport))
{
    var latest = await reopenedClient.FindProgressAsync("书名", book);
    Check(latest.ChapterIndex == 2 && latest.ChapterTitle == "第2章 后续" &&
        laterTransport.BookDownloads == 0, "重开同一缓存书籍仍重新查询云端最新进度");
}
var advanced = new LegadoProgress { Name = "书名", Author = "", ChapterIndex = 1, ChapterPos = 5,
    ChapterTitle = "第1章 开篇", ChapterTime = 2 };
Check(await client.UploadIfForwardAsync(advanced, book) == "进度已上传", "向前提交");
Check(transport.Writes == 1 && transport.Remote.ChapterPos == 5 && transport.SentIfMatch, "条件 PUT + 进度更新");
Check(await client.UploadIfForwardAsync(current, book) != "进度已上传" && transport.Writes == 1,
    "旧段结算不可回退云端");
transport.Remote = new LegadoProgress { Name = "书名", Author = "", ChapterIndex = 2, ChapterPos = 1,
    ChapterTitle = "第2章 后续", ChapterTime = 3 };
Check(await client.UploadIfForwardAsync(advanced, book) != "进度已上传" && transport.Writes == 1,
    "阅读端前进时不得覆盖");
var invalid = new LegadoProgress { Name = "书名", Author = "", ChapterIndex = 2, ChapterPos = 2,
    ChapterTitle = "错误章名" };
try { await client.UploadIfForwardAsync(invalid, book); throw new Exception("非法章名被上传"); }
catch (InvalidOperationException) { }
transport.Remote = current;
transport.ProvideEtag = false;
try { await client.UploadIfForwardAsync(advanced, book); throw new Exception("缺少 ETag 仍上传"); }
catch (InvalidOperationException) { }
Check(transport.Writes == 1, "没有 ETag 时禁止覆盖");
transport.ProvideEtag = true;
transport.ConflictNextPut = true;
try { await client.UploadIfForwardAsync(advanced, book); throw new Exception("412 冲突未上报"); }
catch (InvalidOperationException) { }
Check(transport.Remote.ChapterPos == 0, "并发冲突不能覆盖云端");
Check(transport.AllRequestsUseBasic, "连接复用时每个请求单独附加 Basic 授权");
var httpTransport = new FakeDav { Remote = current };
using (var httpClient = new LegadoWebDavClient("http://example.test/dav", "Legado", "user", "secret", httpTransport))
{
    Check(Encoding.UTF8.GetString(await httpClient.DownloadBookAsync("书名.txt")) == "test" &&
        httpTransport.SawHttpBasic, "HTTP WebDAV 下载和 Basic 授权");
}
try
{
    using var invalidClient = new LegadoWebDavClient("ftp://example.test/dav", "Legado", "user", "secret");
    throw new Exception("非 HTTP 协议被接受");
}
catch (ArgumentException) { }
Console.WriteLine("Legado WebDAV contract checks passed");

static void AddZipFile(ZipArchive zip, string path, string content)
{
    using var writer = new StreamWriter(zip.CreateEntry(path).Open(), Encoding.UTF8);
    writer.Write(content);
}

static void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}

sealed class FakeDav : HttpMessageHandler
{
    public LegadoProgress Remote;
    public int Writes;
    public int BookDownloads;
    public bool SentIfMatch;
    public bool ProvideEtag = true;
    public bool ConflictNextPut;
    public bool SawHttpBasic;
    public bool AllRequestsUseBasic = true;
    public bool OnlyUsedDepthOne = true;
    private int activeFolderRequests;
    public int MaxConcurrentFolderRequests;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (request.Headers.Authorization?.Scheme != "Basic") AllRequestsUseBasic = false;
        if (request.RequestUri.Scheme == Uri.UriSchemeHttp && request.Headers.Authorization?.Scheme == "Basic")
            SawHttpBasic = true;
        var path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
        if (request.Method.Method == "PROPFIND" && request.Headers.GetValues("Depth").Single() != "1")
            OnlyUsedDepthOne = false;
        if (request.Method.Method == "PROPFIND" && path.EndsWith("/books/"))
            return new HttpResponseMessage(HttpStatusCode.MultiStatus)
            {
                Content = new StringContent("<d:multistatus xmlns:d='DAV:'>" +
                    "<d:response><d:href>/dav/Legado/books/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>" +
                    "<d:response><d:href>/dav/Legado/books/书名.txt</d:href></d:response>" +
                    "<d:response><d:href>/dav/Legado/books/示例.epub</d:href></d:response>" +
                    "<d:response><d:href>/dav/Legado/books/示例.umd</d:href></d:response>" +
                    "<d:response><d:href>/dav/Legado/books/示例.mobi</d:href></d:response>" +
                    "<d:response><d:href>/dav/Legado/books/目录/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>" +
                    "<d:response><d:href>/dav/Legado/books/更多/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>" +
                    "</d:multistatus>")
            };
        if (request.Method.Method == "PROPFIND" &&
            (path.EndsWith("/books/目录/") || path.EndsWith("/books/更多/")))
        {
            int active = Interlocked.Increment(ref activeFolderRequests);
            int maximum;
            while (active > (maximum = Volatile.Read(ref MaxConcurrentFolderRequests)) &&
                   Interlocked.CompareExchange(ref MaxConcurrentFolderRequests, active, maximum) != maximum) { }
            try
            {
                await Task.Delay(40, token);
                string folderName = path.EndsWith("/books/目录/") ? "目录" : "更多";
                string fileName = folderName == "目录" ? "内层书.txt" : "另一本.txt";
                return new HttpResponseMessage(HttpStatusCode.MultiStatus)
                {
                    Content = new StringContent("<d:multistatus xmlns:d='DAV:'>" +
                        $"<d:response><d:href>/dav/Legado/books/{folderName}/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>" +
                        $"<d:response><d:href>/dav/Legado/books/{folderName}/{fileName}</d:href></d:response></d:multistatus>")
                };
            }
            finally
            {
                Interlocked.Decrement(ref activeFolderRequests);
            }
        }
        if (request.Method.Method == "PROPFIND" && path.EndsWith("/bookProgress/"))
            return new HttpResponseMessage(HttpStatusCode.MultiStatus)
            {
                Content = new StringContent("<d:multistatus xmlns:d='DAV:'><d:response><d:href>/dav/Legado/bookProgress/%E4%B9%A6%E5%90%8D_.json</d:href></d:response></d:multistatus>")
            };
        if (request.Method == HttpMethod.Get && path.EndsWith("/books/书名.txt"))
        {
            BookDownloads++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("test") };
        }
        if (request.Method == HttpMethod.Get && path.EndsWith("/bookProgress/书名_.json"))
        {
            var result = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(Remote)) };
            if (ProvideEtag) result.Headers.ETag = new EntityTagHeaderValue("\"version1\"");
            return result;
        }
        if (request.Method == HttpMethod.Put && path.EndsWith("/bookProgress/书名_.json"))
        {
            Writes++;
            SentIfMatch = request.Headers.IfMatch.ToString() == "\"version1\"";
            if (ConflictNextPut)
            {
                ConflictNextPut = false;
                return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
            }
            Remote = JsonConvert.DeserializeObject<LegadoProgress>(await request.Content.ReadAsStringAsync(token));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
        throw new Exception("Unexpected WebDAV request: " + request.Method + " " + request.RequestUri);
    }
}
