using System.Collections.Concurrent;
using System.Threading;

namespace DemoLib.FileIndex
{

    /// <summary>
    /// Провайдер индексации файлов с ручным вызовом синхронизации
    /// </summary>
    public sealed class DrivenGradingProvider
    {

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="configuration">Конфигурация</param>
        public DrivenGradingProvider(FileGraderConfiguration configuration)
        {
            this.fsObserver = new FileSystemObserverAgent(configuration);
        }

        private readonly FileSystemObserverAgent fsObserver;

        /// <summary>
        /// Обновление индекса файлов
        /// <returns>Дельта изменений индекса</returns>
        /// </summary>
        public void UpdateIndex(CancellationToken cancellationToken, IProducerConsumerCollection<IdxFileInfo> targetCollection)
        {
            this.fsObserver.UpdateIndex(cancellationToken, targetCollection);
        }

        /// <summary>
        /// Добавить корневую директорию для сканирования
        /// </summary>
        /// <param name="name"></param>
        public void AddScanDirectory(string name)
        {
            this.fsObserver.AddRootDirectory(name);
        }

        /// <summary>
        /// Удалить корневую директорию сканирования
        /// </summary>
        /// <param name="name"></param>
        public void RemoveScanDirectory(string name)
        {
            this.fsObserver.RemoveRootDirectory(name);
        }

    }

}