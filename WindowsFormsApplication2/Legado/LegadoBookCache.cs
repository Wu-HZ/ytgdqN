using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApplication2.Legado
{
    internal sealed record LegadoBookListItem(string FileName, bool IsCached);
    internal sealed record LegadoBookScanResult(IReadOnlyList<LegadoBookListItem> Books, int RequestCount);

    internal sealed class LegadoBookCache
    {
        private readonly string cacheDirectory;

        public LegadoBookCache(string cacheDirectory)
        {
            this.cacheDirectory = cacheDirectory;
        }

        public bool Contains(LegadoWebDavClient client, string fileName) =>
            File.Exists(GetCachePath(client.GetBookCacheKey(fileName)));

        public async Task<(byte[] Content, bool FromCache)> GetOrDownloadAsync(
            LegadoWebDavClient client, string fileName, bool forceDownload = false)
        {
            string path = GetCachePath(client.GetBookCacheKey(fileName));
            if (!forceDownload && File.Exists(path))
            {
                byte[] cached = await File.ReadAllBytesAsync(path);
                if (cached.Length > 0)
                    return (cached, true);
            }

            byte[] content = await client.DownloadBookAsync(fileName);
            if (content.Length == 0)
                throw new InvalidDataException("WebDAV 上的电子书文件为空。");
            Directory.CreateDirectory(cacheDirectory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, content);
                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            return (content, false);
        }

        private string GetCachePath(string cacheKey)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey));
            return Path.Combine(cacheDirectory, Convert.ToHexString(hash).ToLowerInvariant() + ".txt");
        }
    }
}
