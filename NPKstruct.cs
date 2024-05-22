using System;

namespace KobaltBuilder
{
    public struct ArchiveHeader
    {
        public uint Header;
        public uint Version;
        public uint FileDataOffset;
    }

    public struct DirectoryEntry
    {
        public string EntryType;
        public uint EntryLength;
        public string DirectoryName;
        public uint FileOffset;
        public uint FileLength;
        public string FileName;
        public string FullPath;

        public bool IsRootFile => string.IsNullOrEmpty(DirectoryName);
    }

    public struct FileData
    {
        public string FullPath;
        public string FileName;
        public uint DataOffset;
        public uint DataLength;
        public byte[] Data;
    }
}
