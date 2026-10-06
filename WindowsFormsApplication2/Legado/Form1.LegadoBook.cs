using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApplication2.Legado;

namespace WindowsFormsApplication2
{
    public partial class Form1
    {
        private LegadoWebDavClient bookDav;
        private LegadoTxtBook bookText;
        private LegadoProgress bookIdentity;
        private int bookChapterIndex;
        private int bookCompletedPosition;
        private int bookPendingStart;
        private int bookPendingEnd;
        private string bookPendingText;
        private bool bookPendingCompleted;
        private int bookSession;
        private int bookSegmentLength = 200;
        private bool bookProgressWriteBackEnabled;
        private bool bookResumeEnabled;
        private string bookFileName;
        private string bookRuleName;
        private string bookUrl;
        private string bookDirectory;
        private string bookAccount;
        private string preparedFileName;
        private string preparedRuleName;
        private string preparedUrl;
        private string preparedDirectory;
        private string preparedAccount;
        private bool preparedEpub;
        private int preparedEpubChapterIndex;
        private int preparedEpubChapterPos;
        private readonly Dictionary<LegadoWebDavClient, int> pendingBookUploads = new Dictionary<LegadoWebDavClient, int>();
        private readonly HashSet<LegadoWebDavClient> retiredBookClients = new HashSet<LegadoWebDavClient>();
        private LegadoWebDavClient preparedDav;
        private LegadoTxtBook preparedBook;
        private LegadoProgress preparedProgress;
        private readonly LegadoBookCache bookCache = new LegadoBookCache(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RainTyping", "LegadoBooks"));

        internal async Task<LegadoBookScanResult> ListLegadoBooksAsync(string url, string dir,
            string account, string password, bool useSystemProxy)
        {
            using var dav = new LegadoWebDavClient(url, dir, account, password, useSystemProxy: useSystemProxy);
            var files = await dav.ListBookFilesAsync();
            var books = files.Select(file => new LegadoBookListItem(file, bookCache.Contains(dav, file))).ToList();
            return new LegadoBookScanResult(books, dav.LastBookListingRequestCount);
        }

        internal async Task<(IReadOnlyList<LegadoEpubToc.Entry> Chapters, byte[] Content,
            LegadoProgress Progress, string ProgressWarning)> PreviewLegadoEpubAsync(
            string url, string dir, string account, string password, string fileName, bool useSystemProxy, bool forceDownload)
        {
            if (!fileName.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("只能加载 EPUB 目录与云端进度。", nameof(fileName));
            using var dav = new LegadoWebDavClient(url, dir, account, password, useSystemProxy: useSystemProxy);
            var book = await bookCache.GetOrDownloadAsync(dav, fileName, forceDownload);
            var chapters = await Task.Run(() => LegadoEpubToc.Parse(book.Content));
            string title = LegadoEpubToc.ReadBookTitle(book.Content);
            if (string.IsNullOrWhiteSpace(title))
                return (chapters, book.Content, null, "EPUB 元数据缺少书名，不能可靠查找云端进度。");
            try
            {
                var progress = await dav.ReadEpubProgressAsync(title, chapters);
                return (chapters, book.Content, progress, progress == null
                    ? "未找到与 EPUB 书名、章节索引及标题一致的云端进度。" : null);
            }
            catch (Exception ex)
            {
                return (chapters, book.Content, null, "云端进度读取失败；可手动选章从开头跟打：" + ex.Message);
            }
        }

        internal async Task<IReadOnlyList<LegadoNoTocPreview.Entry>> PreviewLegadoNoTocAsync(
            string url, string dir, string account, string password, string fileName,
            bool useSystemProxy, bool forceDownload, int maxLengthWithNoToc)
        {
            if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("只能预览无目录 TXT。", nameof(fileName));
            using var dav = new LegadoWebDavClient(url, dir, account, password, useSystemProxy: useSystemProxy);
            var book = await bookCache.GetOrDownloadAsync(dav, fileName, forceDownload);
            return await Task.Run(() => LegadoNoTocPreview.Parse(book.Content, maxLengthWithNoToc));
        }

        internal async Task<string> PrepareLegadoBookAsync(string url, string dir, string account,
            string password, string fileName, bool forceDownload, bool useSystemProxy, string ruleName = null)
        {
            if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("非 TXT 书籍未经 Legado 位置校验，不能开启进度同步。");
            LegadoWebDavClient dav = null;
            try
            {
                dav = new LegadoWebDavClient(url, dir, account, password, useSystemProxy: useSystemProxy);
                var cachedBook = await bookCache.GetOrDownloadAsync(dav, fileName.Trim(), forceDownload);
                var book = await Task.Run(() => LegadoTxtBook.Parse(cachedBook.Content, ruleName));
                string name = Path.GetFileNameWithoutExtension(fileName.Trim());
                var progress = await dav.FindProgressAsync(name, book);
                preparedDav?.Dispose();
                preparedDav = dav;
                preparedBook = book;
                preparedProgress = progress;
                preparedFileName = fileName.Trim();
                preparedRuleName = ruleName;
                preparedUrl = url.Trim();
                preparedDirectory = dir.Trim();
                preparedAccount = account.Trim();
                dav = null;
                IniWrite("LegadoWebDav", "URL", url.Trim());
                IniWrite("LegadoWebDav", "Dir", dir.Trim());
                IniWrite("LegadoWebDav", "Account", account.Trim());
                IniWrite("LegadoWebDav", "BookFile", fileName.Trim());
                string cacheStatus = cachedBook.FromCache ? "使用本地缓存" : "已下载并缓存书籍";
                return $"{cacheStatus}；编码 {book.Charset}，目录 {book.RuleName}；云端进度：{progress.ChapterTitle}（第 {progress.ChapterIndex} 章，章内位置 {progress.ChapterPos}）。";
            }
            finally
            {
                dav?.Dispose();
            }
        }

        internal bool StartPreparedLegadoBook(int segmentLength)
        {
            if (preparedDav == null || preparedBook == null || preparedProgress == null || segmentLength < 1) return false;
            StopSendFun();
            bookDav = preparedDav;
            preparedDav = null;
            bookText = preparedBook;
            preparedBook = null;
            bookIdentity = preparedProgress;
            preparedProgress = null;
            bookChapterIndex = bookIdentity.ChapterIndex;
            bookCompletedPosition = bookIdentity.ChapterPos;
            bookSegmentLength = segmentLength;
            bookProgressWriteBackEnabled = true;
            bookResumeEnabled = true;
            bookFileName = preparedFileName;
            bookRuleName = preparedRuleName;
            bookUrl = preparedUrl;
            bookDirectory = preparedDirectory;
            bookAccount = preparedAccount;
            ClearHistoryGrid();
            BeginBookChapter(bookChapterIndex, bookCompletedPosition);
            if (NewSendText.发文状态) SaveLegadoResume();
            return NewSendText.发文状态;
        }

        internal void PrepareLegadoEpubTypingBook(LegadoTxtBook book, LegadoProgress progress,
            string title, string fileName, int chapterIndex, int chapterPos)
        {
            if (chapterIndex < 0 || chapterIndex >= book.Chapters.Count)
                throw new ArgumentOutOfRangeException(nameof(chapterIndex));
            DiscardPreparedLegadoBook();
            preparedBook = book;
            preparedProgress = progress ?? new LegadoProgress
            {
                Name = title, Author = "", ChapterIndex = chapterIndex,
                ChapterPos = chapterPos, ChapterTitle = book.Chapters[chapterIndex].Title
            };
            preparedFileName = fileName;
            preparedEpub = true;
            preparedEpubChapterIndex = chapterIndex;
            preparedEpubChapterPos = chapterPos;
        }

        internal bool StartPreparedLegadoEpubBook(int segmentLength)
        {
            if (!preparedEpub || preparedBook == null || preparedProgress == null || segmentLength < 1)
                return false;
            StopSendFun();
            bookText = preparedBook;
            preparedBook = null;
            bookIdentity = preparedProgress;
            preparedProgress = null;
            bookChapterIndex = preparedEpubChapterIndex;
            bookCompletedPosition = bookIdentity.ChapterPos;
            bookSegmentLength = segmentLength;
            bookProgressWriteBackEnabled = false;
            bookResumeEnabled = false;
            bookFileName = preparedFileName;
            bookRuleName = null;
            bookDav = null;
            try { File.Delete(GetLegadoResumePath()); }
            catch (Exception ex) { ShowFlowText("无法清除 TXT 续打记录：" + ex.Message); }
            ClearHistoryGrid();
            BeginBookChapter(bookChapterIndex, preparedEpubChapterPos);
            preparedEpub = false;
            return NewSendText.发文状态;
        }

        internal void DiscardPreparedLegadoBook()
        {
            preparedDav?.Dispose();
            preparedDav = null;
            preparedBook = null;
            preparedProgress = null;
            preparedFileName = null;
            preparedEpub = false;
        }

        private string GetLegadoResumePath() => Path.Combine(Application.StartupPath, "legado-send-session.json");

        private void SaveLegadoResume()
        {
            try
            {
                new LegadoBookResume
                {
                    Url = bookUrl, Directory = bookDirectory, Account = bookAccount,
                    FileName = bookFileName, RuleName = bookRuleName, SegmentLength = bookSegmentLength
                }.Save(GetLegadoResumePath());
            }
            catch (Exception ex)
            {
                ShowFlowText("无法保存电子书恢复记录，重启后不能自动续打：" + ex.Message);
            }
        }

        private async Task<bool> RestoreLegadoBookOnStartupAsync()
        {
            string path = GetLegadoResumePath();
            if (!File.Exists(path)) return false;
            int startingSession = bookSession;
            try
            {
                var resume = LegadoBookResume.Load(path);
                string url = IniRead("LegadoWebDav", "URL", "");
                string directory = IniRead("LegadoWebDav", "Dir", "");
                string account = IniRead("LegadoWebDav", "Account", "");
                if (!string.Equals(resume.Url, url, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(resume.Directory ?? "", directory ?? "", StringComparison.Ordinal) ||
                    !string.Equals(resume.Account, account, StringComparison.Ordinal))
                    throw new InvalidOperationException("WebDAV 配置与上次跟打的书籍不一致，请在发文窗口手动确认。");
                string encrypted = IniRead("LegadoWebDav", "PasswordProtected", "");
                if (string.IsNullOrWhiteSpace(encrypted))
                    throw new InvalidOperationException("尚未保存 WebDAV 密码，请在发文窗口重新输入。");
                string password = LegadoPasswordProtection.Unprotect(encrypted);
                bool.TryParse(IniRead("LegadoWebDav", "UseSystemProxy", "false"), out bool useSystemProxy);
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp &&
                    MessageBox.Show(this, "HTTP 不加密：WebDAV 的 Basic 账号和密码可被网络中途观察到。确认自动重连吗？",
                        "HTTP WebDAV 安全提示", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return true;
                ShowFlowText("正在从 WebDAV 获取电子书最新进度…");
                await PrepareLegadoBookAsync(url, directory, account, password, resume.FileName, false, useSystemProxy, resume.RuleName);
                if (IsDisposed || Disposing || bookSession != startingSession || NewSendText.发文状态)
                {
                    DiscardPreparedLegadoBook();
                    return true;
                }
                if (!StartPreparedLegadoBook(resume.SegmentLength))
                    throw new InvalidOperationException("云端进度已到书末或无法启动电子书发文。");
                Glob.CurrentSessionId = Guid.NewGuid().ToString("N");
                SetMatch(false);
                ShowFlowText("已按 WebDAV 最新进度恢复电子书发文");
                发文状态ToolStripMenuItem.PerformClick();
            }
            catch (Exception ex)
            {
                DiscardPreparedLegadoBook();
                if (!IsDisposed && !Disposing)
                    MessageBox.Show(this, "电子书自动恢复失败，未使用旧的本地进度：" + ex.Message +
                        "\n请在「发文 → Legado 电子书」中检查设置后重新读取进度。",
                        "电子书自动恢复", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return true;
        }

        private void BeginBookChapter(int index, int position)
        {
            while (index < bookText.Chapters.Count &&
                bookText.Chapters[index].TypingPosition(position) == bookText.Chapters[index].TypingText.Length)
            {
                index++;
                position = 0;
            }
            if (index >= bookText.Chapters.Count)
            {
                MessageBox.Show(this, "这本书已跟打到末尾。", "电子书跟打");
                StopSendFun();
                return;
            }
            var chapter = bookText.Chapters[index];
            bookChapterIndex = index;
            bookCompletedPosition = chapter.TypingPosition(position);
            bookPendingText = null;
            bookPendingCompleted = true;
            NewSendText.发文状态 = true;
            NewSendText.类型 = "文章";
            NewSendText.ArticleSource = NewSendText.ArticleSourceValue.LegadoEbook;
            NewSendText.SentId = -1;
            NewSendText.trim = false;
            NewSendText.文章全文 = chapter.TypingText;
            NewSendText.发文全文 = chapter.TypingText;
            NewSendText.标题 = chapter.Title;
            NewSendText.标记 = bookCompletedPosition;
            NewSendText.字数 = bookSegmentLength;
            NewSendText.已发段数 = 0;
            NewSendText.已发字数 = bookCompletedPosition;
            NewSendText.是否自动 = true;
            NewSendText.AutoCondition = false;
            NewSendText.是否周期 = false;
            Glob.CurSegmentNum = 0;
            Glob.SendCursor = 0;
            Glob.TempSegmentRecord.Clear();
            SendNextFun();
        }

        // 在 CleanBeforeSending 中调用，保留发文原有的拆段与自动跳段链路。
        private void TrackBookSegment(string text)
        {
            if (bookText == null) return;
            bookPendingStart = NewSendText.标记 - text.Length;
            bookPendingEnd = NewSendText.标记;
            bookPendingText = text;
            bookPendingCompleted = false;
        }

        // 输入完成或用户明确跳过当前段时提交对应的段末阅读位置。
        private void CompleteBookSegment()
        {
            if (bookText == null || bookPendingCompleted || bookPendingText == null) return;
            bookPendingCompleted = true;
            if (bookPendingStart != bookCompletedPosition ||
                bookPendingEnd > bookText.Chapters[bookChapterIndex].TypingText.Length ||
                bookPendingText != richTextBox1.Text)
            {
                ShowFlowText("电子书段落位置不连续，已停止进度上传。请重新开书同步。");
                NewSendText.是否自动 = false;
                ResetBookMode();
                return;
            }
            bookCompletedPosition = bookPendingEnd;
            if (!bookProgressWriteBackEnabled) return;
            var chapter = bookText.Chapters[bookChapterIndex];
            var snapshot = new LegadoProgress
            {
                Name = bookIdentity.Name, Author = bookIdentity.Author,
                ChapterIndex = chapter.Index, ChapterPos = chapter.OriginalPosition(bookCompletedPosition),
                ChapterTitle = chapter.Title, ChapterTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            var dav = bookDav;
            var book = bookText;
            int session = bookSession;
            pendingBookUploads.TryGetValue(dav, out int count);
            pendingBookUploads[dav] = count + 1;
            _ = UploadCompletedSegmentAsync(dav, book, snapshot, session);
        }

        private async Task UploadCompletedSegmentAsync(LegadoWebDavClient dav, LegadoTxtBook book, LegadoProgress snapshot, int session)
        {
            try
            {
                string result = await dav.UploadIfForwardAsync(snapshot, book);
                if (session == bookSession && result != "进度已上传") ShowFlowText(result);
            }
            catch (Exception ex)
            {
                if (session == bookSession)
                    MessageBox.Show(this, ex.Message, "电子书进度未上传", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                int count = --pendingBookUploads[dav];
                if (count == 0)
                {
                    pendingBookUploads.Remove(dav);
                    if (retiredBookClients.Remove(dav)) dav.Dispose();
                }
            }
        }

        private void ResetBookMode(bool preserveResume = false)
        {
            if (!preserveResume)
            {
                try { File.Delete(GetLegadoResumePath()); }
                catch (Exception ex) { ShowFlowText("无法清除电子书恢复记录：" + ex.Message); }
            }
            bookSession++;
            if (bookDav != null)
            {
                if (pendingBookUploads.ContainsKey(bookDav)) retiredBookClients.Add(bookDav);
                else bookDav.Dispose();
            }
            bookDav = null;
            bookProgressWriteBackEnabled = false;
            bookResumeEnabled = false;
            bookText = null;
            bookIdentity = null;
            bookPendingText = null;
            bookFileName = null;
            bookRuleName = null;
            bookUrl = null;
            bookDirectory = null;
            bookAccount = null;
        }

        private bool AdvanceBookOnSendNext()
        {
            if (bookText == null) return false;
            if (!bookPendingCompleted)
            {
                if (bookPendingText == null)
                {
                    ShowFlowText("当前电子书段落尚未就绪，无法前进。");
                    return true;
                }
                // Ctrl+N/“发下一段”表示明确跳过当前段，提交该段末尾位置后再装载下一段。
                CompleteBookSegment();
                if (bookText == null) return true;
            }
            if (NewSendText.标记 >= bookText.Chapters[bookChapterIndex].TypingText.Length)
            {
                BeginBookChapter(bookChapterIndex + 1, 0);
                return true;
            }
            return false;
        }
    }
}
