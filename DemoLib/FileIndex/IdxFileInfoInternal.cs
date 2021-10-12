using System;
using System.Diagnostics;

namespace DemoLib.FileIndex
{

    /// <summary>
    /// Контекст файла
    /// </summary>
    [DebuggerDisplay("{Name} {FileTime}")]
    internal sealed class IdxFileInfoInternal
    {

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fileTime"></param>
        /// <param name="fileSize"></param>
        /// <param name="sourceFileState"></param>
        public IdxFileInfoInternal(string name, long fileSize, DateTime fileTime, SourceFileState sourceFileState)
        {
            this.name = name ?? throw new ArgumentNullException(nameof(name));

            this.fileSize        = fileSize;
            this.FileTime        = fileTime;
            this.sourceFileState = sourceFileState;
            this.isUpdated       = true;
        }

        private readonly string name;


        private long fileSize;
        private DateTime fileTime;
        private SourceFileState sourceFileState;

        private bool isUpdated;


        /// <summary>
        /// состояние файла источника
        /// </summary>
        public SourceFileState SourceFileState { get { return this.sourceFileState; } set { this.sourceFileState = value; } }

        /// <summary>
        /// Размер файла (байт)
        /// </summary>
        public long FileSize
        {
            get { return this.fileSize; }
            set
            {
                this.fileSize        = value;
                this.sourceFileState = SourceFileState.Changed;
            }
        }


        public string Name => this.name;

        public DateTime FileTime { get => this.fileTime; set => this.fileTime = value; }

        internal void SetIsUpdatedFlag()
        {
            this.isUpdated = true;
        }

        internal bool GetAndResetIsUpdatedFlag()
        {
            var res = this.isUpdated;
            this.isUpdated = false;
            return res;
        }

    }

}