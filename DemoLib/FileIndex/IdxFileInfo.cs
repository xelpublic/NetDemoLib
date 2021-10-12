using System;

namespace DemoLib.FileIndex
{

    /// <summary>
    /// Контекст файла индекса
    /// </summary>
    public sealed class IdxFileInfo
    {

        internal IdxFileInfo(IdxFileInfoInternal fileContextInternal)
        {
            this.name       = fileContextInternal.Name;
            this.size       = fileContextInternal.FileSize;
            this.changeTime = fileContextInternal.FileTime;
            this.fileState  = fileContextInternal.SourceFileState;
        }

        private readonly string name;
        private readonly long size;
        private readonly DateTime changeTime;
        private SourceFileState fileState;

        /// <summary>
        /// Полное наименование файла
        /// </summary>
        public string Name { get { return this.name; } }

        /// <summary>
        /// Размер
        /// </summary>
        public long Size { get { return this.size; } }

        /// <summary>
        /// Время изменения файла
        /// </summary>
        public DateTime ChangeTime { get { return this.changeTime; } }

        /// <summary>
        /// Состояние файла
        /// </summary>
        public SourceFileState FileState { get { return this.fileState; } internal set { this.fileState = value; } }

    }

}