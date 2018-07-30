using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace apak
{
    /// <summary>
    /// Provides the Ability to pack and unpack files
    /// </summary>
    public static class APak
    {
        /// <summary>
        /// Size of a 32 bit integer
        /// </summary>
        private const int I32_SIZE = sizeof(int);
        /// <summary>
        /// Size of a 64 bit integer
        /// </summary>
        private const int I64_SIZE = sizeof(long);
        /// <summary>
        /// PAK Header
        /// </summary>
        private const string HEADER = "APAK";

        /// <summary>
        /// Types of PAK Entries
        /// </summary>
        /// <remarks>Not all combinations are valid</remarks>
        [Flags]
        private enum EntryType : byte
        {
            /// <summary>
            /// Unsupported
            /// </summary>
            Regular = 0,
            /// <summary>
            /// Entry is a File
            /// </summary>
            File = 1,
            /// <summary>
            /// Entry is a Directory
            /// </summary>
            Directory = 2,
            /// <summary>
            /// The content is compressed
            /// </summary>
            /// <remarks>This can only be used for entries that have a content</remarks>
            GZip = 4
        }

        /// <summary>
        /// Possible Compression Types
        /// </summary>
        public enum CompressionType : byte
        {
            /// <summary>
            /// No Compression at all
            /// </summary>
            NoCompression = 0,
            /// <summary>
            /// Compress all files but individually
            /// </summary>
            IndividualCompression = 1,
            /// <summary>
            /// Compress all files but in one single blob (Solid Compression)
            /// </summary>
            AllCompressionGZip = 2
        }

        /// <summary>
        /// Gets a relative path (if applicable) from a full path
        /// </summary>
        /// <param name="Name">Full Name</param>
        /// <param name="Root">Root Directory</param>
        /// <returns>Relative String or <paramref name="Name"/> if not a sub-element of <paramref name="Root"/></returns>
        /// <remarks>
        /// This method provides no value validation at all. 
        /// Don't expose to other components
        /// </remarks>
        private static string GetRelPath(string Name, string Root)
        {
            if (Name.ToLower().StartsWith(Root.ToLower()))
            {
                return Name.Substring(Root.Length + 1);
            }
            return Name;
        }

        /// <summary>
        /// Writes an integer prefixed UTF-8 string to a <see cref="Stream"/>
        /// </summary>
        /// <param name="Output">Stream to write to</param>
        /// <param name="Value">String to write</param>
        private static void WS(Stream Output, string Value)
        {
            //Write a zero integer and exit for empty/null strings
            if (string.IsNullOrEmpty(Value))
            {
                Output.Write(BitConverter.GetBytes(0), 0, I32_SIZE);
            }
            else
            {
                var Bytes = Encoding.UTF8.GetBytes(Value);
                Output.Write(BitConverter.GetBytes(Bytes.Length), 0, I32_SIZE);
                Output.Write(Bytes, 0, Bytes.Length);
            }
        }

        /// <summary>
        /// Reads an integer prefixed UTF-8 string from a <see cref="Stream"/>
        /// </summary>
        /// <param name="BR">Reader</param>
        /// <returns>string</returns>
        private static string RS(BinaryReader BR)
        {
            int Count = BR.ReadInt32();
            if (Count > 0)
            {
                return Encoding.UTF8.GetString(BR.ReadBytes(Count));
            }
            if (Count < 0)
            {
                throw new InvalidDataException($"Invalid String Length Specifier. Got {Count}");
            }
            return string.Empty;
        }

        /// <summary>
        /// Copies a <see cref="Stream"/> onto another but only as many bytes as specified
        /// </summary>
        /// <param name="Input">Source <see cref="Stream"/></param>
        /// <param name="Output">Destination <see cref="Stream"/></param>
        /// <param name="Bytes">Number of bytes to copy</param>
        /// <remarks>This will work correctly on <paramref name="Input"/> Steams that are shorter than <paramref name="Bytes"/> specifies</remarks>
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

        /// <summary>
        /// Creates a FileSpec List from a Directory
        /// </summary>
        /// <param name="RootDirectory">Directory to start scanning</param>
        /// <returns><see cref="FileSpec"/> Array, sorted by Directory First</returns>
        /// <remarks>This implementation doesn't handles inaccessible directories and will fail.</remarks>
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

        /// <summary>
        /// Packs files together
        /// </summary>
        /// <param name="Files">File/Directory List</param>
        /// <param name="Destination">Destination stream. Left open after completion</param>
        /// <param name="Compression">Compression type</param>
        /// <param name="Log">Logger for messages</param>
        /// <remarks>
        /// This Implementation lacks a "per-file" selection for Compression.
        /// </remarks>
        public static void Pack(IEnumerable<FileSpec> Files, Stream Destination, CompressionType Compression = CompressionType.AllCompressionGZip, TextWriter Log = null)
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
            Output.Write(Encoding.ASCII.GetBytes(HEADER), 0, Encoding.ASCII.GetByteCount(HEADER));
            Output.WriteByte((byte)(Compression == CompressionType.AllCompressionGZip ? CompressionType.AllCompressionGZip : CompressionType.NoCompression));

            Log.WriteLine("Saving Directory Map");
            Output.Write(BitConverter.GetBytes(Files.Count()), 0, I32_SIZE);

            //Replace output stream if all content is to be compressed
            if (Compression == CompressionType.AllCompressionGZip)
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
                    Output.WriteByte((byte)(EntryType.File | EntryType.GZip));
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
            //If all content is to be compressed, dispose our own strem
            if (Compression == CompressionType.AllCompressionGZip)
            {
                Output.Dispose();
            }
            Log.WriteLine("Done");
        }

        /// <summary>
        /// Unpacks files from an APAK archive
        /// </summary>
        /// <param name="Input">Input stream. Must be set to the start of the header. Left open after completion</param>
        /// <param name="OutputDirectory">Output directory. If <see cref="null"/>, the file is parsed but no extraction is performed</param>
        /// <param name="CollectFiles"><see cref="true"/> to collect and return FileSpecs</param>
        /// <param name="Log">Logger for messages</param>
        /// <returns>File Specs if <paramref name="CollectFiles"/> is <see cref="true"/>, otherwise <see cref="null"/></returns>
        /// <remarks>
        /// Using <paramref name="OutputDirectory"/>=<see cref="null"/> and <paramref name="CollectFiles"/>=<see cref="true"/> will return files only, not extract.
        /// It's recommended to Leave <paramref name="CollectFiles"/> set to <see cref="false"/> to avoid memory problems when extracting pak files with millions of entries.
        /// </remarks>
        public static FileSpec[] Unpack(Stream Input, string OutputDirectory = null, bool CollectFiles = false, TextWriter Log = null)
        {
            if (Log == null)
            {
                Log = TextWriter.Null;
            }
            if (!Input.CanRead)
            {
                throw new ArgumentException("Input must be readable");
            }

            //This combination would do nothing and report nothing
            if (string.IsNullOrWhiteSpace(OutputDirectory) && !CollectFiles)
            {
                throw new ArgumentException("Either 'OutputDirectory' or 'CollectFiles' must be set (or both)");
            }

            string RootDirectory = string.IsNullOrWhiteSpace(OutputDirectory) ? "." : Path.GetFullPath(OutputDirectory);

            CompressionType Compression;
            FileSpec[] Files;
            int FileCount;
            using (var BR = new BinaryReader(Input, Encoding.UTF8, true))
            {
                var Header = Encoding.ASCII.GetString(BR.ReadBytes(Encoding.ASCII.GetByteCount(HEADER)));
                if (Header != HEADER)
                {
                    throw new ArgumentException("Input stream is not an APAK file");
                }
                Compression = (CompressionType)BR.ReadByte();
                FileCount = BR.ReadInt32();
                if (FileCount < 0)
                {
                    throw new InvalidDataException("Negative File Count speficied in Header");
                }
                if (CollectFiles)
                {
                    Files = new FileSpec[FileCount];
                }
                else
                {
                    Files = new FileSpec[0];
                }
            }
            //Verify that the compression type is valid
            if (!Enum.IsDefined(Compression.GetType(), Compression) || Compression == CompressionType.IndividualCompression)
            {
                throw new InvalidDataException($"Invalid Compression specified in File Header. Value was {Compression}");
            }
            //Sneakily replace source stream with GZip Stream to decompress if needed
            if (Compression == CompressionType.AllCompressionGZip)
            {
                Input = new GZipStream(Input, CompressionMode.Decompress);
            }
            using (var BR = new BinaryReader(Input, Encoding.UTF8, true))
            {
                Log.WriteLine("Processing {0} entries", FileCount);
                for (var i = 0; i < FileCount; i++)
                {
                    var Flags = (EntryType)BR.ReadByte();
                    var Name = RS(BR).TrimStart('/').Replace('/', '\\');
                    if (string.IsNullOrWhiteSpace(Name))
                    {
                        throw new InvalidDataException("Entry Name is Empty");
                    }
                    var CurrentFile = new FileSpec(Path.GetFullPath(Path.Combine(RootDirectory, Name)), Name, Flags.HasFlag(EntryType.Directory));
                    if (CollectFiles)
                    {
                        Files[i] = CurrentFile;
                    }
                    Log.WriteLine("Reading {0} Dir={1}", CurrentFile.EntryName, CurrentFile.IsDirectory);
                    if (Flags == EntryType.Directory)
                    {
                        //Don't seek on directory entries, they have no size and data.
                        if (!CurrentFile.RealName.StartsWith(RootDirectory))
                        {
                            throw new InvalidDataException($"Invalid Directory Name specified. Value doen't results in s aubdirectory of {RootDirectory}. Value was {CurrentFile.RealName}");
                        }
                        Directory.CreateDirectory(CurrentFile.RealName);
                    }
                    else if (Flags.HasFlag(EntryType.File))
                    {
                        if (Flags != EntryType.File && Flags != (EntryType.File | EntryType.GZip))
                        {
                            throw new InvalidDataException($"Invalid Flag Combination for File. Value was {Flags}");
                        }
                        var Size = BR.ReadInt64();
                        if (Size < 0)
                        {
                            throw new InvalidDataException($"Invalid File Size specified. Value was {Size}");
                        }
                        //Extract if output has been specified
                        if (!string.IsNullOrWhiteSpace(OutputDirectory))
                        {
                            Log.WriteLine("Extracting File...");
                            var DirName = Path.GetDirectoryName(CurrentFile.RealName);
                            if (!DirName.StartsWith(RootDirectory))
                            {
                                throw new InvalidDataException($"Invalid File Name specified. Value doen't results in s aubdirectory of {RootDirectory}. Value was {CurrentFile.RealName}");
                            }
                            //Create directories for files too
                            if (!Directory.Exists(DirName))
                            {
                                Directory.CreateDirectory(DirName);
                            }
                            using (var FS = File.Create(CurrentFile.RealName))
                            {
                                if (Flags.HasFlag(EntryType.GZip))
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
                            //Just seek forward, we are not extracting
                            BR.BaseStream.Seek(Size, SeekOrigin.Current);
                        }
                    }
                    else
                    {
                        throw new InvalidDataException($"Unexpected Flag Combination: {Flags}");
                    }
                }
                Log.WriteLine("Done");
            }
            //Dispose GZip stream created in this function
            if (Compression == CompressionType.AllCompressionGZip)
            {
                Input.Dispose();
            }
            return CollectFiles ? Files : null;
        }
    }
}
