using System;
using System.IO;

namespace apak
{
    public class FileSpec
    {
        public string RealName
        { get; private set; }
        public string EntryName
        { get; private set; }
        public bool IsDirectory
        { get; private set; }
        public bool IsFile
        {
            get
            {
                return !IsDirectory;
            }
        }

        /// <summary>
        /// Create A FileSpec from a file that does not exists
        /// </summary>
        /// <param name="RealName">Real File name</param>
        /// <param name="PakName">PAK File Name</param>
        /// <param name="IsDirectory">File name is Directory, not file</param>
        public FileSpec(string RealName,string PakName, bool IsDirectory)
        {
            EntryName = PakName.Replace('\\', '/');
            this.RealName = RealName;
            this.IsDirectory = IsDirectory;
        }

        /// <summary>
        /// Create a FileSpec from a real File
        /// </summary>
        /// <param name="RealName">Real File name</param>
        /// <param name="PakName">PAK File name</param>
        /// <remarks>The file/Directory must exist for this constructor</remarks>
        public FileSpec(string RealName, string PakName)
        {
            if (!File.Exists(RealName) && !Directory.Exists(RealName))
            {
                throw new ArgumentException("RealName is not a valid Directory or File name");
            }
            EntryName = PakName.Replace('\\', '/');
            this.RealName = RealName;
            IsDirectory = !File.Exists(RealName);
        }
    }
}
