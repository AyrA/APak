using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Test
{
    /// <summary>
    /// Provides Low-Level File API Wrappers
    /// </summary>
    public static class FileTools
    {
        #region WIN_API

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFileHandle CreateFile([MarshalAs(UnmanagedType.LPTStr)] string filename, [MarshalAs(UnmanagedType.U4)] FileAccess access, [MarshalAs(UnmanagedType.U4)] FileShare share, IntPtr securityAttributes, [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition, [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes, IntPtr templateFile);

        [StructLayout(LayoutKind.Explicit)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            [FieldOffset(0)]
            public uint FileAttributes;
            [FieldOffset(4)]
            public ulong CreationTime;
            [FieldOffset(12)]
            public ulong LastAccessTime;
            [FieldOffset(20)]
            public ulong LastWriteTime;
            [FieldOffset(28)]
            public uint VolumeSerialNumber;
            [FieldOffset(32)]
            public uint FileSizeHigh;
            [FieldOffset(36)]
            public uint FileSizeLow;
            [FieldOffset(40)]
            public uint NumberOfLinks;
            [FieldOffset(44)]
            public uint FileIndexHigh;
            [FieldOffset(48)]
            public uint FileIndexLow;

            public bool FileEquals(BY_HANDLE_FILE_INFORMATION f)
            {
                return FileIndexHigh == f.FileIndexHigh &&
                    FileIndexLow == f.FileIndexLow &&
                    VolumeSerialNumber == f.VolumeSerialNumber;
            }

            public DateTime CreationDateTime
            {
                get
                {
                    return DateTime.FromFileTime((long)CreationTime);
                }
            }
            public DateTime LastAccessDateTime
            {
                get
                {
                    return DateTime.FromFileTime((long)LastAccessTime);
                }
            }

            public DateTime LastWriteDateTime
            {
                get
                {
                    return DateTime.FromFileTime((long)LastWriteTime);
                }
            }
        }

        #endregion

        /// <summary>
        /// Checks if two file paths are identical.
        /// </summary>
        /// <param name="P1">Path 1</param>
        /// <param name="P2">Path 2</param>
        /// <remarks>
        /// At least one file has to exist
        /// </remarks>
        /// <returns>true, if identical</returns>
        public static bool ComparePath(string P1, string P2)
        {
            //Consider two null strings identical
            if (P1 == null && P2 == null)
            {
                return true;
            }
            //Throw if only one arg is null
            if (P1 == null)
            {
                throw new ArgumentNullException("P1");
            }
            //Throw if only one arg is null
            if (P2 == null)
            {
                throw new ArgumentNullException("P1");
            }
            //Consider Paths equal if strings are
            if (P1 == P2)
            {
                return true;
            }
            //Fail if both files don't exist
            if (!File.Exists(P1) && !File.Exists(P2))
            {
                throw new ArgumentException("At least one argument needs to point to an existing file");
            }

            //Consider Paths unequal if one of the files doesn't exists
            if (!File.Exists(P1) || !File.Exists(P2))
            {
                return false;
            }
            using (SafeFileHandle sfh1 = CreateFile(P1, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, (FileAttributes)0x02000080, IntPtr.Zero))
            {
                if (sfh1.IsInvalid)
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
                using (SafeFileHandle sfh2 = CreateFile(P2, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, (FileAttributes)0x02000080, IntPtr.Zero))
                {
                    if (sfh2.IsInvalid)
                    {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    }
                    var fileInfo1 = new BY_HANDLE_FILE_INFORMATION();
                    var fileInfo2 = new BY_HANDLE_FILE_INFORMATION();
                    if (!GetFileInformationByHandle(sfh1, out fileInfo1))
                    {
                        throw new Win32Exception();
                    }
                    if (!GetFileInformationByHandle(sfh2, out fileInfo2))
                    {
                        throw new Win32Exception();
                    }
                    return fileInfo1.FileEquals(fileInfo2);
                }
            }
        }
    }
}
