using System;
using System.IO;

namespace DemoLib.FileIndex
{

    /// <summary>
    /// Конфигурация индексации файловой системы
    /// </summary>
    public sealed class FileGraderConfiguration
    {

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="directoryFilterPredicate">Предикат фильтрации директорий</param>
        /// <param name="fileFilterPredicate">Предикат фильтрации файлов</param>
        /// <param name="observerBalancerConfiguration"></param>
        public FileGraderConfiguration(
            Predicate<DirectoryInfo> directoryFilterPredicate, Predicate<FileInfo> fileFilterPredicate
            , ObserverBalancerConfiguration observerBalancerConfiguration)
        {
            this.directoryFilterPredicate      = directoryFilterPredicate;
            this.fileFilterPredicate           = fileFilterPredicate;
            this.observerBalancerConfiguration = observerBalancerConfiguration ?? throw new ArgumentNullException(nameof(observerBalancerConfiguration));
        }

        private readonly Predicate<DirectoryInfo> directoryFilterPredicate;
        private readonly Predicate<FileInfo> fileFilterPredicate;
        private readonly ObserverBalancerConfiguration observerBalancerConfiguration;

        /// <summary>
        /// Предикат фильтрации файлов
        /// </summary>
        public Predicate<FileInfo> FileFilterPredicate { get { return this.fileFilterPredicate; } }

        /// <summary>
        /// Предикат фильтрации директорий
        /// </summary>
        public Predicate<DirectoryInfo> DirectoryFilterPredicate { get { return this.directoryFilterPredicate; } }

        /// <summary>
        /// Конфигурация балансировщика нагрузки
        /// </summary>
        public ObserverBalancerConfiguration BalancerConfiguration { get { return this.observerBalancerConfiguration; } }

    }

}