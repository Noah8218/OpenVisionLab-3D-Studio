using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OpenVisionLab.Provenance
{
    // Metadata only. No UI/process enumeration, permission changes, or file writes.
    public static class ReportFileIdentity
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks;
            public uint FileIndexHigh, FileIndexLow;
        }

        public sealed class Facts
        {
            public string Path;
            public string Identity;
            public bool Exists;
            public bool IsDirectory;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string path, uint access,
            uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file,
            out FileInformation information);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file,
            StringBuilder path, uint length, uint flags);

        public static Facts Read(string path)
        {
            string full = System.IO.Path.GetFullPath(path);
            FileAttributes attributes;
            try { attributes = File.GetAttributes(full); }
            catch (FileNotFoundException) { return new Facts { Path = full }; }
            catch (DirectoryNotFoundException) { return new Facts { Path = full }; }
            using (SafeFileHandle file = CreateFileW(full, 0, 7, IntPtr.Zero, 3,
                0x02000000, IntPtr.Zero))
            {
                if (file.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
                FileInformation info;
                if (!GetFileInformationByHandle(file, out info))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var final = new StringBuilder(32768);
                uint count = GetFinalPathNameByHandleW(file, final, (uint)final.Capacity, 0);
                if (count == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (count >= final.Capacity) throw new IOException("Canonical path exceeds buffer.");
                string resolved = final.ToString();
                if (resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                    resolved = @"\\" + resolved.Substring(8);
                else if (resolved.StartsWith(@"\\?\", StringComparison.Ordinal))
                    resolved = resolved.Substring(4);
                return new Facts {
                    Path = resolved, Exists = true,
                    IsDirectory = (attributes & FileAttributes.Directory) != 0,
                    Identity = info.VolumeSerialNumber.ToString("X8") + ":" +
                        info.FileIndexHigh.ToString("X8") + info.FileIndexLow.ToString("X8")
                };
            }
        }
    }
}
