using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace apak
{
    public static class APak
    {
        private const int I32_SIZE = 4;
        private const int I64_SIZE = 8;
        private const string HEADER = "APAK";

        [Flags]
        private enum EntryType : byte
        {
            Regular = 0,
            File = 1,
            Directory = 2,
            Compressed = 4
        }

        public enum CompressionType : byte
        {
            NoCompression = 0,
            IndividualCompression = 1,
            AllCompression = 2
        }

        private static string GetRelPath(string Name, string Root)
        {
            if (Name.ToLower().StartsWith(Root.ToLower()))
            {
                return Name.Substring(Root.Length + 1);
            }
            return Name;
        }

        private static void WS(Stream Output, string FileName)
        {
            if (string.IsNullOrEmpty(FileName))
            {
                Output.Write(BitConverter.GetBytes(0), 0, I32_SIZE);
            }
            else
            {
                int Len = Encoding.UTF8.GetByteCount(FileName);
                Output.Write(BitConverter.GetBytes(Len), 0, I32_SIZE);
                Output.Write(Encoding.UTF8.GetBytes(FileName), 0, Len);
            }
        }

        private static string RS(BinaryReader BR)
        {
            int Count = BR.ReadInt32();
            if (Count > 0)
            {
                return Encoding.UTF8.GetString(BR.ReadBytes(Count));
            }
            return string.Empty;
        }

        private static void CopyStream(Stream Input, Stream Output, long Bytes)
        {
            byte[] buffer = new byte[1000000];
            int read;
            while (Bytes > 0 && (read = Input.Read(buffer, 0, (int)Math.Min(buffer.Length, Bytes))) > 0)
            {
                Output.Write(buffer, 0, read);
                Bytes -= read;
            }
        }

        public static FileSpec[] GetFiles(string RootDirectory)
        {
            var FullPath = Path.GetFullPath(RootDirectory);
            var Files = new List<FileSpec>();
            return
                Directory
                    .GetDirectories(FullPath, "*", SearchOption.AllDirectories)
                    .Select(m => new FileSpec(m, GetRelPath(m, FullPath)))
                    .Concat(
                    Directory
                        .GetFiles(RootDirectory, "*", SearchOption.AllDirectories)
                        .Select(m => new FileSpec(m, GetRelPath(m, FullPath)))
                )
                .ToArray();

        }

        public static void Pack(IEnumerable<FileSpec> Files, Stream Destination, CompressionType Compression = CompressionType.AllCompression, TextWriter Log = null)
        {
            Stream Output = Destination;
            if (Log == null)
            {
                Log = TextWriter.Null;
            }
            if (!Output.CanWrite)
            {
                throw new ArgumentException("Output must be writable");
            }
            Output.Write(Encoding.ASCII.GetBytes(HEADER), 0, 4);
            Output.WriteByte((byte)(Compression == CompressionType.AllCompression ? CompressionType.AllCompression : CompressionType.NoCompression));

            Log.WriteLine("Saving Directory Map");
            Output.Write(BitConverter.GetBytes(Files.Count()), 0, I32_SIZE);

            if (Compression == CompressionType.AllCompression)
            {
                Output.Flush();
                Output = new GZipStream(Output, CompressionLevel.Optimal, true);
            }

            foreach (var Dir in Files.Where(m => m.IsDirectory))
            {
                Log.WriteLine(Dir.EntryName);
                Output.WriteByte((byte)EntryType.Directory);
                WS(Output, Dir.EntryName);
            }
            Log.WriteLine("Saving Files. Compress={0}", Compression);
            foreach (var F in Files.Where(m => m.IsFile))
            {
                Log.WriteLine(F.EntryName);
                if (Compression == CompressionType.IndividualCompression)
                {
                    Output.WriteByte((byte)(EntryType.File | EntryType.Compressed));
                    WS(Output, F.EntryName);
                    //Create temporary file
                    var TempName = Path.GetTempFileName();
                    using (var FTemp = File.Open(TempName, FileMode.Open, FileAccess.ReadWrite))
                    {
                        using (var GZ = new GZipStream(FTemp, CompressionLevel.Optimal, true))
                        {
                            //Compress Entry into temp file
                            using (var FS = File.OpenRead(F.RealName))
                            {
                                Log.WriteLine("Compressing {0} to {1}", F.EntryName, TempName);
                                FS.CopyTo(GZ);
                            }
                        }
                        //Copy temp file to real destination
                        Log.WriteLine("Copying {0} to Output", TempName);
                        Output.Write(BitConverter.GetBytes(FTemp.Length), 0, I64_SIZE);
                        FTemp.Seek(0, SeekOrigin.Begin);
                        FTemp.CopyTo(Output);
                    }
                    //Delete temporary file
                    File.Delete(TempName);
                }
                else
                {
                    Output.WriteByte((byte)EntryType.File);
                    WS(Output, F.EntryName);
                    using (var FS = File.OpenRead(F.RealName))
                    {
                        Output.Write(BitConverter.GetBytes(FS.Length), 0, I64_SIZE);
                        FS.CopyTo(Output);
                    }
                }
            }
            if (Compression == CompressionType.AllCompression)
            {
                Output.Dispose();
            }
            Log.WriteLine("Done");
        }

        public static FileSpec[] Unpack(Stream Input, string OutputDirectory = null, TextWriter Log = null)
        {
            if (Log == null)
            {
                Log = TextWriter.Null;
            }
            if (!Input.CanRead)
            {
                throw new ArgumentException("Input must be readable");
            }

            string RootDirectory = string.IsNullOrWhiteSpace(OutputDirectory) ? "." : Path.GetFullPath(OutputDirectory);

            CompressionType Compression;
            FileSpec[] Files;
            using (var BR = new BinaryReader(Input, Encoding.UTF8, true))
            {
                var Header = Encoding.ASCII.GetString(BR.ReadBytes(4));
                if (Header != HEADER)
                {
                    throw new ArgumentException("Input stream is not an APAK file");
                }
                Compression = (CompressionType)BR.ReadByte();
                Files = new FileSpec[BR.ReadInt32()];
            }
            //Sneakily replace source stream with GZip Stream to decompress if needed
            if (Compression == CompressionType.AllCompression)
            {
                Input = new GZipStream(Input, CompressionMode.Decompress);
            }
            using (var BR = new BinaryReader(Input, Encoding.UTF8, true))
            {
                Log.WriteLine("Processing {0} entries", Files.Length);
                for (var i = 0; i < Files.Length; i++)
                {
                    var Flags = (EntryType)BR.ReadByte();
                    var Name = RS(BR).Replace('/', '\\');
                    Files[i] = new FileSpec(Path.Combine(RootDirectory, Name), Name, Flags.HasFlag(EntryType.Directory));
                    Log.WriteLine("Reading {0} Dir={1}", Files[i].EntryName, Files[i].IsDirectory);
                    if (Flags.HasFlag(EntryType.Directory))
                    {
                        //Don't seek on directory entries
                        Directory.CreateDirectory(Files[i].RealName);
                    }
                    else
                    {
                        var Size = BR.ReadInt64();
                        //Extract if output has been specified
                        if (!string.IsNullOrWhiteSpace(OutputDirectory))
                        {
                            Log.WriteLine("Extracting File...");
                            var DirName = Path.GetDirectoryName(Files[i].RealName);
                            if(!Directory.Exists(DirName))
                            {
                                Directory.CreateDirectory(DirName);
                            }
                            using (var FS = File.Create(Files[i].RealName))
                            {
                                if (Flags.HasFlag(EntryType.Compressed))
                                {
                                    //Decompress data
                                    using (var RS = new RangedStream(Input, Size))
                                    {
                                        using (var GZ = new GZipStream(RS, CompressionMode.Decompress))
                                        {
                                            GZ.CopyTo(FS);
                                        }
                                    }
                                }
                                else
                                {
                                    //Copy uncompressed data directly
                                    CopyStream(Input, FS, Size);
                                }
                            }
                        }
                        else
                        {
                            BR.BaseStream.Seek(Size, SeekOrigin.Current);
                        }
                    }
                }
                Log.WriteLine("Done");
            }
            if (Compression == CompressionType.AllCompression)
            {
                Input.Dispose();
            }
            return Files;
        }
    }
}
