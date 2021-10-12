using System;
using System.Collections.Generic;
using System.IO;

namespace DemoLib.FileIndex
{

    internal class IdxDirectoryInfo
    {

        public IdxDirectoryInfo(DirectoryInfo directoryInfo, IdxDirectoryInfo parent)
        {
            this.name           = directoryInfo.FullName;
            this.ChangeTime     = directoryInfo.LastWriteTimeUtc;
            this.parent         = parent;
            this.directoryInfo  = directoryInfo;
            this.files          = new Dictionary<string, IdxFileInfoInternal>();
            this.childs         = new Dictionary<string, IdxDirectoryInfo>();
            this.hasFileChanges = true;
            this.isAlive        = true;
            this.ReviewTime     = DateTime.MinValue;
            this.Rate           = 0;
        }

        private readonly string name;
        private readonly IdxDirectoryInfo parent;
        private readonly DirectoryInfo directoryInfo;
        private readonly Dictionary<string, IdxFileInfoInternal> files;
        private readonly Dictionary<string, IdxDirectoryInfo> childs;
        private bool hasFileChanges;
        private bool isAlive;

        public Dictionary<string, IdxFileInfoInternal> Files { get { return this.files; } }

        public string Name { get { return this.name; } }

        public bool HasFileChanges { get { return this.hasFileChanges; } set { this.hasFileChanges = value; } }

        public IdxDirectoryInfo Parent { get { return this.parent; } }

        public Dictionary<string, IdxDirectoryInfo> Childs { get { return this.childs; } }

        public DirectoryInfo DirectoryInfo { get { return this.directoryInfo; } }

        public bool IsAlive { get { return this.isAlive; } set { this.isAlive = value; } }

        public DateTime ReviewTime { get; set; }

        public DateTime ChangeTime { get; set; }

        /// <summary>
        /// оценка актуальности данных директории 0-10
        /// </summary>
        public int Rate { get; set; }

        /// <summary>
        /// счетчик изменений за последние 5 сканирований
        /// </summary>
        public int IterationChangesCount { get; set; }

        /// <summary>
        /// счетчик измененных файлов в последней итерации
        /// </summary>
        public int FileChangeCount { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.name}";
        }

    }

}