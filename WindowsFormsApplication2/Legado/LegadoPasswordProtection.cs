using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsFormsApplication2.Legado
{
    // DPAPI ciphertext can be stored in config.ini; only the current Windows user can decrypt it.
    internal static class LegadoPasswordProtection
    {
        private const int UiForbidden = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Length;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(ref DataBlob input, string description, IntPtr entropy,
            IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy,
            IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        internal static string Protect(string password)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("密码不能为空。", nameof(password));
            return Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(password), encrypt: true));
        }

        internal static string Unprotect(string encrypted)
        {
            return Encoding.UTF8.GetString(Transform(Convert.FromBase64String(encrypted), encrypt: false));
        }

        private static byte[] Transform(byte[] data, bool encrypt)
        {
            var input = new DataBlob { Length = data.Length, Data = Marshal.AllocHGlobal(data.Length) };
            var output = new DataBlob();
            try
            {
                Marshal.Copy(data, 0, input.Data, data.Length);
                bool ok = encrypt
                    ? CryptProtectData(ref input, "RainTyping Legado WebDAV", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        UiForbidden, out output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        UiForbidden, out output);
                if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法使用当前 Windows 用户解密或保存 WebDAV 密码。");
                var result = new byte[output.Length];
                Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(input.Data);
                if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            }
        }
    }
}
