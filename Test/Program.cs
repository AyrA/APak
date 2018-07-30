using System;
using apak;
using System.Linq;
using System.IO;

namespace Test
{
    class Program
    {
        private struct CMD
        {
            public bool Valid;
            public APak.CompressionType Compression;
            public string Source;
            public string Destination;
        }

        static void Main(string[] args)
        {
            if (args == null || args.Length == 0 || args.Any(m => m == "/?"))
            {
                ShowHelp();
            }
            else
            {
                var C = ParseArgs(args);
                if (C.Valid)
                {
                    var IN = Path.GetFullPath(C.Source);
                    var OUT = Path.GetFullPath(C.Destination);

                    //Unpack mode
                    if (File.Exists(IN))
                    {
                        if (!Directory.Exists(OUT))
                        {
                            Directory.CreateDirectory(OUT);
                        }
                        using (var FS = File.OpenRead(IN))
                        {
                            APak.Unpack(FS, OUT, false, Console.Error);
                        }
                    }
                    //Pack mode
                    else if (Directory.Exists(IN))
                    {
                        //Grab files before creating the destination
                        var Files = APak.GetFiles(IN);
                        //If the Destination exists, make sure to exclude it from the list
                        if (File.Exists(OUT))
                        {
                            Files = Files
                                .Where(m => !FileTools.ComparePath(Path.GetFullPath(m.RealName), OUT))
                                .ToArray();
                        }
                        using (var FS = File.Create(OUT))
                        {
                            APak.Pack(Files, FS, C.Compression, Console.Error);
                        }
                    }
                    else
                    {
                        //That should not be possible due to Valid==false but race conditions exist.
                        throw new IOException("'in' is neither existing File nor Directory");
                    }
                }
            }
        }

        private static CMD ParseArgs(string[] args)
        {
            const APak.CompressionType INVALID = (APak.CompressionType)255;
            var Ret = new CMD();
            Ret.Compression = INVALID;
            Ret.Valid = true;

            foreach (var A in args)
            {
                switch (A.ToLower())
                {
                    case "/ca":
                        if (Ret.Compression == INVALID)
                        {
                            Ret.Compression = APak.CompressionType.AllCompressionGZip;
                        }
                        else
                        {
                            Console.Error.WriteLine("Unable to process '/CA'. Compression type already specified");
                            Ret.Valid = false;
                        }
                        break;
                    case "/cn":
                        if (Ret.Compression == INVALID)
                        {
                            Ret.Compression = APak.CompressionType.NoCompression;
                        }
                        else
                        {
                            Console.Error.WriteLine("Unable to process '/CN'. Compression type already specified");
                            Ret.Valid = false;
                        }
                        break;
                    case "/ci":
                        if (Ret.Compression == INVALID)
                        {
                            Ret.Compression = APak.CompressionType.IndividualCompression;
                        }
                        else
                        {
                            Console.Error.WriteLine("Unable to process '/CI'. Compression type already specified");
                            Ret.Valid = false;
                        }
                        break;
                    default:
                        if (Ret.Source == null)
                        {
                            Ret.Source = A;
                        }
                        else if (Ret.Destination == null)
                        {
                            Ret.Destination = A;
                        }
                        else
                        {
                            Console.Error.WriteLine("Too many arguments when parsing {0}.", A);
                            Ret.Valid = false;
                        }
                        break;
                }
            }
            if (Ret.Valid)
            {
                //Apply default if needed
                if (Ret.Compression == INVALID)
                {
                    Ret.Compression = APak.CompressionType.AllCompressionGZip;
                }
                //Make sure that 'in' is specified
                if (string.IsNullOrWhiteSpace(Ret.Source))
                {
                    Console.Error.WriteLine("Parameter 'in' not specified.");
                    Ret.Valid = false;
                }
                //Make sure that 'out' is specified
                else if (string.IsNullOrWhiteSpace(Ret.Destination))
                {
                    Console.Error.WriteLine("Parameter 'out' not specified.");
                    Ret.Valid = false;
                }
                //Make sure that 'in' exists
                else if (!File.Exists(Ret.Source) && !Directory.Exists(Ret.Source))
                {
                    Console.Error.WriteLine("'in' Parameter is neither existing file nor existing directory");
                    Ret.Valid = false;
                }
                //Make sure that 'out' is not a file if 'in' is
                else if (File.Exists(Ret.Source))
                {
                    if (File.Exists(Ret.Destination))
                    {
                        Console.Error.WriteLine("'out' can't be a file if 'in' is a file");
                        Ret.Valid = false;
                    }
                }
            }
            //Print Help Hint on errors
            if (!Ret.Valid)
            {
                Console.Error.WriteLine("Error parsing Command line. Use /? for details");
            }
            return Ret;
        }

        static void ShowHelp()
        {
            Console.Error.WriteLine(@"apak [/C{A|N|I}] <in> <out>

Packs and unpacks files into a minimalistic archive with optional compression.
The primary use of this is to provide a way to bundle your application with
multiple resources. Instead of bundling 500 files you can add one that
contains these 500 files and even compresses them using GZip

/CA   - Compress all data into a single stream
        This is the default if the argument is not specified. This type of
        compression is widely known as 'solid compression'. This achieves a
        smaller file size on the cost of speed. You can't randomly access any
        file you want and need to process the entire compressed segment in one
        single pass, even decompressing files you don't want to.
        This also compresses the directory listing.
        This type is recommended if you plan on always extracting everything.
/CN   - Don't compress data at all
        This completely disables compression. This is the fastest way to pack
        and unpack data and is recommended if your files are mostly made up of
        already compressed formats like zip, 7z or mp3
/CI   - Compress files individually
        This compression type is not as good as the full compression but
        allows almost instantaneous skipping over files you don't want to
        unpack.
in    - Input file or directory
out   - Output file or directory

If in is a file, out must be a directory. The file is decompressed
If in is a directory, out must be a file. The directory is compressed

Existing files are overwritten

The /C argument has no effect on decompression");
        }
    }
}
