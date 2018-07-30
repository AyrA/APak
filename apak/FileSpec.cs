using System;
using System.IO;

namespace apak
{
    /// <summary>
    /// File Specification for a Pak file
    /// </summary>
    /// <remarks>This is probably where you want to implement a per-file basis compression</remarks>
    public class FileSpec
    {
        /// <summary>
        /// Gets the Real name used on the File system
        /// </summary>
        public string RealName
        { get; private set; }
        /// <summary>
        /// Gets the name used in the pak file
        /// </summary>
        public string EntryName
        { get; private set; }
        /// <summary>
        /// Gets if the Entry is a Directory
        /// </summary>
        public bool IsDirectory
        { get; private set; }
        /// <summary>
        /// Gets if the Entry is a File
        /// </summary>
        /// <remarks>
        /// Right now this is !<see cref="IsDirectory"/>
        /// but this can change in the feature if we start supporting symbolic links or any other 3rd type
        /// </remarks>
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
        /// <remarks>Use this constructor to construct a FileSpec from a PAK entry</remarks>
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
