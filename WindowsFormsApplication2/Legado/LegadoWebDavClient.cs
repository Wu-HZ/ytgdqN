using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace WindowsFormsApplication2.Legado
{
    internal sealed class LegadoProgress
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("author")] public string Author { get; set; }
        [JsonProperty("durChapterIndex")] public int ChapterIndex { get; set; }
        [JsonProperty("durChapterPos")] public int ChapterPos { get; set; }
        [JsonProperty("durChapterTime")] public long ChapterTime { get; set; }
        [JsonProperty("durChapterTitle")] public string ChapterTitle { get; set; }
    }

    internal sealed class LegadoWebDavClient : IDisposable
    {
        private static readonly HttpClient DirectClient = CreateSharedClient(false);
        private static readonly HttpClient ProxyClient = CreateSharedClient(true);
        private readonly HttpClient client;
        private readonly bool ownsClient;
        private readonly AuthenticationHeaderValue authorization;
        private readonly Uri root;
        private readonly string account;
        private Uri progressFile;
        private LegadoProgress lastProgress;
        private readonly SemaphoreSlim uploadLock = new SemaphoreSlim(1, 1);

        public LegadoWebDavClient(string serverUrl, string directory, string account, string password,
            HttpMessageHandler handler = null, bool useSystemProxy = false)
        {
            if (string.IsNullOrWhiteSpace(serverUrl) ||
                !Uri.TryCreate(serverUrl.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new ArgumentException("WebDAV 地址必须以 http:// 或 https:// 开头，且不含查询参数。");
            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password))
                throw new ArgumentException("请填写 WebDAV 账号和密码。");
            string baseUrl = uri.AbsoluteUri.TrimEnd('/') + "/";
            string dir = (directory ?? "").Trim().Trim('/');
            if (dir.Length != 0)
                baseUrl += string.Join("/", dir.Split('/').Select(Uri.EscapeDataString)) + "/";
            root = new Uri(baseUrl);
            this.account = account.Trim();
            // Keep handlers alive across scans so TLS connections are pooled instead of
            // paying a cold connection setup on every book-list refresh.
            client = handler == null ? (useSystemProxy ? ProxyClient : DirectClient) : new HttpClient(handler);
            ownsClient = handler != null;
            if (ownsClient) client.Timeout = TimeSpan.FromSeconds(40);
            // Legado 使用 okhttp Credentials.basic 的默认 ISO-8859-1 编码。
            authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.Latin1.GetBytes(account + ":" + password)));
        }

        private static HttpClient CreateSharedClient(bool useSystemProxy)
        {
            HttpMessageHandler handler = useSystemProxy
                ? new HttpClientHandler { UseProxy = true }
                : new SocketsHttpHandler { UseProxy = false, ConnectCallback = ConnectIpv4FirstAsync };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
        }

        private static async ValueTask<Stream> ConnectIpv4FirstAsync(
            SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            bool hasIpv6 = addresses.Any(address => address.AddressFamily == AddressFamily.InterNetworkV6);
            Exception lastError = null;
            foreach (var address in addresses.OrderBy(address =>
                         address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1))
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (hasIpv6 && address.AddressFamily == AddressFamily.InterNetwork)
                    connectTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), connectTimeout.Token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex) when (ex is SocketException ||
                    (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
                {
                    socket.Dispose();
                    lastError = ex;
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
            throw lastError ?? new SocketException((int)SocketError.HostNotFound);
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
        {
            var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = authorization;
            return request;
        }

        private int lastBookListingRequestCount;
        public int LastBookListingRequestCount => lastBookListingRequestCount;

        public async Task<IReadOnlyList<string>> ListBookFilesAsync()
        {
            var booksRoot = new Uri(root, "books/");
            var folders = new Queue<Uri>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new HashSet<string>(StringComparer.Ordinal);
            folders.Enqueue(booksRoot);
            lastBookListingRequestCount = 0;
            XNamespace dav = "DAV:";

            while (folders.Count > 0)
            {
                // Depth:1 is much cheaper than a server-side Depth:infinity tree walk.
                // Scan up to four discovered directories concurrently, keeping library scans
                // responsive without flooding the DAV server.
                var batch = new List<Uri>();
                while (folders.Count > 0 && batch.Count < 4)
                {
                    Uri folder = folders.Dequeue();
                    if (visited.Add(folder.AbsoluteUri)) batch.Add(folder);
                }
                var listings = await Task.WhenAll(batch.Select(async folder =>
                {
                    using var request = CreateRequest(new HttpMethod("PROPFIND"), folder);
                    request.Headers.Add("Depth", "1");
                    using var response = await client.SendAsync(request);
                    Interlocked.Increment(ref lastBookListingRequestCount);
                    response.EnsureSuccessStatusCode();
                    var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
                    return (Folder: folder, Document: document);
                }));

                foreach (var listing in listings)
                foreach (var item in listing.Document.Descendants(dav + "response"))
                {
                    string href = item.Element(dav + "href")?.Value;
                    if (string.IsNullOrWhiteSpace(href)) continue;
                    Uri itemUri = new Uri(root, href);
                    if (!itemUri.AbsolutePath.StartsWith(booksRoot.AbsolutePath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemUri.AbsolutePath.TrimEnd('/'), listing.Folder.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                        continue;
                    string relativePath = Uri.UnescapeDataString(itemUri.AbsolutePath.Substring(booksRoot.AbsolutePath.Length).TrimEnd('/'));
                    if (string.IsNullOrEmpty(relativePath)) continue;
                    if (item.Descendants(dav + "collection").Any())
                    {
                        folders.Enqueue(new Uri(itemUri.AbsoluteUri.TrimEnd('/') + "/"));
                    }
                    else if (relativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                        relativePath.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(relativePath);
                    }
                }
            }
            return files.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public string GetBookCacheKey(string fileName) => this.account + "\n" + GetBookUri(fileName);

        private string GetBookUri(string fileName)
        {
            string[] segments = (fileName ?? "").Split('/');
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('\\') ||
                segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..") ||
                !(fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".epub", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("请选择 books/ 中的 TXT 或 EPUB 文件。");
            string escapedPath = string.Join("/", segments.Select(Uri.EscapeDataString));
            return new Uri(root, "books/" + escapedPath).AbsoluteUri;
        }

        public async Task<byte[]> DownloadBookAsync(string fileName)
        {
            using var request = CreateRequest(HttpMethod.Get, new Uri(GetBookUri(fileName)));
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<LegadoProgress> FindProgressAsync(string name, LegadoTxtBook book)
        {
            var candidates = await ListProgressCandidatesAsync(name);
            if (candidates.Count != 1)
                throw new InvalidOperationException(candidates.Count == 0
                    ? "未找到该书的云端进度。请确认 Legado 已开启阅读进度同步并上传过该书的进度。"
                    : "发现多份同名书进度，请先在 WebDAV 中确认书名及作者，避免写错进度。");

            progressFile = new Uri(root, "bookProgress/" + Uri.EscapeDataString(candidates[0]));
            var (progress, _) = await ReadProgressAsync();
            if (progress.Name != name || progress.Author == null)
                throw new InvalidOperationException("云端书名/作者与选定文件不一致，已停止同步。");
            Validate(progress, book);
            lastProgress = progress;
            return progress;
        }

        // EPUB is read-only until its formatted durChapterPos coordinates have been verified.
        // Do not set progressFile/lastProgress: they authorize TXT conditional writes.
        public async Task<LegadoProgress> ReadEpubProgressAsync(string name, IReadOnlyList<LegadoEpubToc.Entry> chapters)
        {
            var candidates = await ListProgressCandidatesAsync(name);
            if (candidates.Count == 0) return null;
            if (candidates.Count != 1)
                throw new InvalidOperationException("发现多份同名 EPUB 进度，无法可靠选择云端记录。");
            using var request = CreateRequest(HttpMethod.Get,
                new Uri(root, "bookProgress/" + Uri.EscapeDataString(candidates[0])));
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var progress = JsonConvert.DeserializeObject<LegadoProgress>(await response.Content.ReadAsStringAsync());
            if (progress?.Name != name || progress.Author == null ||
                progress.ChapterIndex < 0 || progress.ChapterIndex >= chapters.Count ||
                chapters[progress.ChapterIndex].Title != progress.ChapterTitle || progress.ChapterPos < 0)
                return null;
            return progress;
        }

        private async Task<List<string>> ListProgressCandidatesAsync(string name)
        {
            // 不猜作者：Legado 的文件名取决于书架里记录的作者。
            var progressRoot = new Uri(root, "bookProgress/");
            using var request = CreateRequest(new HttpMethod("PROPFIND"), progressRoot);
            request.Headers.Add("Depth", "1");
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
            XNamespace dav = "DAV:";
            string prefix = NormalizeFileName(name + "_");
            return xml.Descendants(dav + "href")
                .Select(element => new Uri(root, element.Value))
                .Where(uri => uri.Scheme == root.Scheme && uri.Authority == root.Authority &&
                    uri.AbsolutePath.StartsWith(progressRoot.AbsolutePath, StringComparison.Ordinal))
                .Select(uri => Uri.UnescapeDataString(uri.AbsolutePath.Split('/').Last()))
                .Where(filename => filename.StartsWith(prefix, StringComparison.Ordinal) && filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal).ToList();
        }

        public async Task<string> UploadIfForwardAsync(LegadoProgress completed, LegadoTxtBook book)
        {
            await uploadLock.WaitAsync();
            try
            {
                if (progressFile == null || lastProgress == null)
                    throw new InvalidOperationException("尚未确认云端进度，不能上传。");
                Validate(completed, book);
                if (completed.Name != lastProgress.Name || completed.Author != lastProgress.Author)
                    throw new InvalidOperationException("上传书籍与拉取的书籍不同。");
                var (remote, etag) = await ReadProgressAsync();
                Validate(remote, book);
                if (remote.Name != completed.Name || remote.Author != completed.Author)
                    throw new InvalidOperationException("云端书籍已改变，停止上传。");
                if (Compare(remote, completed) >= 0)
                {
                    lastProgress = remote;
                    return "云端进度未落后，本次未覆盖";
                }
                if (etag == null)
                    throw new InvalidOperationException("WebDAV 未返回 ETag，无法保证并发写入安全，已跳过上传。");
                using var request = CreateRequest(HttpMethod.Put, progressFile);
                request.Headers.IfMatch.Add(etag);
                request.Content = new StringContent(JsonConvert.SerializeObject(completed), Encoding.UTF8, "application/json");
                using var response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                    throw new InvalidOperationException("阅读端同时更新了进度，本次上传已取消，请重新打开书籍同步。");
                response.EnsureSuccessStatusCode();
                lastProgress = completed;
                return "进度已上传";
            }
            finally
            {
                uploadLock.Release();
            }
        }

        private async Task<(LegadoProgress progress, EntityTagHeaderValue etag)> ReadProgressAsync()
        {
            using var request = CreateRequest(HttpMethod.Get, progressFile);
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var progress = JsonConvert.DeserializeObject<LegadoProgress>(await response.Content.ReadAsStringAsync());
            return (progress ?? throw new InvalidOperationException("云端进度 JSON 为空。"), response.Headers.ETag);
        }

        private static void Validate(LegadoProgress progress, LegadoTxtBook book)
        {
            if (progress.ChapterIndex < 0 || progress.ChapterIndex >= book.Chapters.Count)
                throw new InvalidOperationException("云端章节索引不在 TXT 目录内，已停止同步。");
            var chapter = book.Chapters[progress.ChapterIndex];
            if (chapter.Title != progress.ChapterTitle || progress.ChapterPos < 0 || progress.ChapterPos > chapter.Content.Length)
                throw new InvalidOperationException($"云端章节或章内位置与 TXT 不一致：{progress.ChapterIndex} / {progress.ChapterTitle}。已停止同步。");
        }

        private static int Compare(LegadoProgress x, LegadoProgress y)
        {
            int chapter = x.ChapterIndex.CompareTo(y.ChapterIndex);
            return chapter != 0 ? chapter : x.ChapterPos.CompareTo(y.ChapterPos);
        }

        private static string NormalizeFileName(string name)
        {
            foreach (char invalid in "\\/:*?\"<>|") name = name.Replace(invalid, '_');
            return name;
        }

        public void Dispose()
        {
            if (ownsClient) client.Dispose();
            uploadLock.Dispose();
        }
    }
}
