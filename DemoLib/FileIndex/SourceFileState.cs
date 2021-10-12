namespace DemoLib.FileIndex
{

    /// <summary>
    /// Состояние файла источника в файловой системе
    /// </summary>
    public enum SourceFileState
    {

        /// <summary>
        /// Не определено
        /// </summary>
        None = 0,

        /// <summary>
        /// Новый
        /// </summary>
        New = 1,

        /// <summary>
        /// Файл изменен
        /// </summary>
        Changed = 2,

        /// <summary>
        /// файл удален
        /// </summary>
        Lost = 3

    }

}