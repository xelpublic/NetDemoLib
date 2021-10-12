using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DemoLib.FileIndex
{

    /// <summary>
    /// внутренняя логики исполнения синхронизации с файловой системой
    /// </summary>
    internal sealed class FileSystemObserverAgent
    {

        public FileSystemObserverAgent(FileGraderConfiguration observerConfiguration)
        {
            this.observerConfiguration = observerConfiguration ?? throw new ArgumentNullException(nameof(observerConfiguration));

            this.balancerConfiguration = observerConfiguration.BalancerConfiguration;

            this.firstGenerationAge   = this.balancerConfiguration.FirstGenerationAge;
            this.secondGenerationAge  = this.balancerConfiguration.SecondGeneRationAge;
            this.currentIterationSize = this.balancerConfiguration.IterationSize;
            this.generationWeight     = this.balancerConfiguration.GenerationWeight;

            this.processStopwatch = new Stopwatch();
        }

        private readonly Dictionary<string, IdxDirectoryInfo> rootDirectories = new Dictionary<string, IdxDirectoryInfo>();
        private readonly FileGraderConfiguration observerConfiguration;
        private readonly Stopwatch processStopwatch;

        private readonly List<IdxDirectoryInfo> currentIterationScope = new List<IdxDirectoryInfo>();


        private readonly TimeSpan firstGenerationAge;
        private readonly TimeSpan secondGenerationAge;

        private readonly ConcurrentQueue<IdxDirectoryInfo> firstGenerationQueue = new ConcurrentQueue<IdxDirectoryInfo>();
        private readonly ConcurrentQueue<IdxDirectoryInfo> secondGenerationQueue = new ConcurrentQueue<IdxDirectoryInfo>();
        private readonly ConcurrentQueue<IdxDirectoryInfo> thirdGenerationQueue = new ConcurrentQueue<IdxDirectoryInfo>();
        private readonly double[] generationWeight;
        private readonly ObserverBalancerConfiguration balancerConfiguration;

        private int currentIterationSize;

        private int processedFileInIteration;

        private void UpdateIndexInternal(CancellationToken cancellationToken, IProducerConsumerCollection<IdxFileInfo> targetCollection)
        {
            this.processedFileInIteration = 0;
            this.processStopwatch.Restart();

            this.PrepareIterationScope();

            this.ProcessIterationScope(cancellationToken);

            this.processStopwatch.Stop();

            this.ProcessIterationResult(targetCollection);

            this.RebalanceIterationPredictions();
        }

        private void RebalanceIterationPredictions()
        {
            var iterationDuration = this.processStopwatch.Elapsed;

            this.currentIterationSize = (int) (this.balancerConfiguration.PreferIterationDuration.TotalMilliseconds / iterationDuration.TotalMilliseconds * this.currentIterationSize);

            if (this.currentIterationSize > this.balancerConfiguration.IterationSize)
            {
                this.currentIterationSize = this.balancerConfiguration.IterationSize;
            }
            else if (this.currentIterationSize < this.balancerConfiguration.MinIterationSize)
            {
                this.currentIterationSize = this.balancerConfiguration.MinIterationSize;
            }
        }

        private void ProcessIterationResult(IProducerConsumerCollection<IdxFileInfo> targetCollection)
        {
            var timePoint = DateTime.UtcNow;
            foreach (var currentDirectory in this.currentIterationScope)
            {
                if (!currentDirectory.IsAlive)
                {
                    //если директория была удалена. опубликовать все дочерние файлы как lost
                    //пометить дочерние директории как удаленные
                    var lostDirectoriesStack = new Stack<IdxDirectoryInfo>();
                    lostDirectoriesStack.Push(currentDirectory);
                    while (lostDirectoriesStack.Count > 0)
                    {
                        var currentLostDirectory = lostDirectoriesStack.Pop();
                        currentLostDirectory.IsAlive = false;

                        foreach (var childDirectory in currentLostDirectory.Childs.Values)
                        {
                            lostDirectoriesStack.Push(childDirectory);
                        }

                        foreach (var lostFileContextInternal in currentLostDirectory.Files.Values)
                        {
                            var fileContext = new IdxFileInfo(lostFileContextInternal);
                            fileContext.FileState = SourceFileState.Lost;
                            targetCollection.TryAdd(fileContext);
                        }
                    }

                    //удалить удаленную директорию из чайлдов парента
                    if (currentDirectory.Parent != null)
                    {
                        currentDirectory.Parent.Childs.Remove(currentDirectory.Name);
                    }

                    //если парента нет, это должна быть корневая директория
                    else if (this.rootDirectories.ContainsKey(currentDirectory.Name))
                    {
                        this.rootDirectories.Remove(currentDirectory.Name);
                    }

                    continue;
                }

                if (currentDirectory.HasFileChanges)
                {
                    var killList = new List<string>();

                    foreach (var fileContextInternal in currentDirectory.Files.Values)
                    {
                        if (fileContextInternal.SourceFileState == SourceFileState.None)
                        {
                            continue;
                        }

                        var fileContext = new IdxFileInfo(fileContextInternal);
                        targetCollection.TryAdd(fileContext);

                        if (fileContextInternal.SourceFileState == SourceFileState.Lost)
                        {
                            killList.Add(fileContextInternal.Name);
                        }
                        else
                        {
                            fileContextInternal.SourceFileState = SourceFileState.None;
                        }
                    }

                    //зачистка лост файлов
                    foreach (var fileName in killList)
                    {
                        currentDirectory.Files.Remove(fileName);
                    }
                }

                currentDirectory.HasFileChanges = false;

                //вернуть директорию в очередь
                switch (this.RateDirectoryGeneration(timePoint, currentDirectory.ChangeTime))
                {
                    case 0:
                        this.firstGenerationQueue.Enqueue(currentDirectory);
                        break;
                    case 1:
                        this.secondGenerationQueue.Enqueue(currentDirectory);
                        break;
                    default:
                        this.thirdGenerationQueue.Enqueue(currentDirectory);
                        break;
                }
            }

            Console.WriteLine(
                $"Время обновления индекса {this.processStopwatch.ElapsedMilliseconds} мс;" +
                $"Обработано: директорий {this.currentIterationScope.Count};" +
                $" файлов  {this.processedFileInIteration} " +
                $"Изменений найдено {targetCollection.Count}");
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int RateDirectoryGeneration(DateTime timeNow, DateTime dirChangeTime)
        {
            var age = timeNow - dirChangeTime;

            if (age <= this.firstGenerationAge)
            {
                return 0;
            }

            if (age <= this.secondGenerationAge)
            {
                return 1;
            }

            return 2;
        }

        private void PrepareIterationScope()
        {
            this.currentIterationScope.Clear();

            var fgCount = this.currentIterationSize * this.generationWeight[0];

            Console.WriteLine($" первое пололение {this.firstGenerationQueue.Count}");
            Console.WriteLine($" второе пололение {this.secondGenerationQueue.Count}");
            Console.WriteLine($" третье пололение {this.thirdGenerationQueue.Count}");


            while (fgCount > 0 && this.firstGenerationQueue.TryDequeue(out var dir))
            {
                //если директория помечена как удаленная. логика ее публикации уже отработала. забыть эту директорию
                if (!dir.IsAlive)
                {
                    continue;
                }

                if (dir.Rate >= 10)
                {
                    fgCount--;

                    this.currentIterationScope.Add(dir);
                }
                else
                {
                    //оценка маркера количества изменений содержимого директории за последние 5 проходов
                    dir.Rate += dir.IterationChangesCount * 2 + 1;
                    this.firstGenerationQueue.Enqueue(dir);
                }
            }

            var sgCount = this.currentIterationSize * this.generationWeight[1] + fgCount;

            var timePoint       = DateTime.UtcNow;
            var ageBorderFactor = (this.secondGenerationAge.Ticks - this.firstGenerationAge.Ticks) / 3;

            while (sgCount > 0 && this.secondGenerationQueue.TryDequeue(out var dir))
            {
                if (!dir.IsAlive)
                {
                    continue;
                }

                if (dir.Rate >= 10)
                {
                    sgCount--;

                    this.currentIterationScope.Add(dir);
                }
                else
                {
                    //оценка маркера количества изменений содержимого директории за последние 5 проходов
                    dir.Rate += dir.IterationChangesCount * 2 + 1;

                    //оценка маркера количества измененных файлов в последний проход
                    dir.Rate += dir.FileChangeCount > 3 ? 3 : dir.FileChangeCount;

                    //оценка маркера близости возраста директории к границам 1-2 поколений
                    var dirAge = timePoint - dir.ChangeTime;
                    dir.Rate += (int) ((dirAge.Ticks - this.firstGenerationAge.Ticks) / ageBorderFactor + 1);

                    this.secondGenerationQueue.Enqueue(dir);
                }
            }

            var tgCount = this.currentIterationSize * this.generationWeight[2] + sgCount;

            while (tgCount > 0 && this.thirdGenerationQueue.TryDequeue(out var dir))
            {
                if (!dir.IsAlive)
                {
                    continue;
                }

                if (dir.Rate >= 10)
                {
                    tgCount--;

                    this.currentIterationScope.Add(dir);
                }
                else
                {
                    dir.Rate += dir.IterationChangesCount * 2 + 1;
                    this.thirdGenerationQueue.Enqueue(dir);
                }
            }
        }

        private void ProcessIterationScope(CancellationToken cancellationToken)
        {
            var directoryFilter = this.observerConfiguration.DirectoryFilterPredicate;

            int index = 0;


            while (index < this.currentIterationScope.Count && !cancellationToken.IsCancellationRequested)
            {
                var currentDirectory = this.currentIterationScope[index];
                index++;
                currentDirectory.FileChangeCount = 0;

                try
                {
                    using (var fileSystemItemsEnumerator = currentDirectory.DirectoryInfo.EnumerateFileSystemInfos().GetEnumerator())
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                if (!fileSystemItemsEnumerator.MoveNext())
                                {
                                    //когда заканчивается перебор актуальных элементов фс. производится поиск удаленных файлов текущей директории
                                    this.DetectLostFiles(currentDirectory);
                                    break;
                                }
                            }
                            catch (DirectoryNotFoundException)
                            {
                                currentDirectory.IsAlive = false;
                                continue;
                            }
                            catch
                            {
                                // IGNORE
                                //игнорировать состояния объектов fs кроме тех, что можем обработать сейчас
                                continue;
                            }

                            var currentItem = fileSystemItemsEnumerator.Current;

                            if (currentItem == null)
                            {
                                continue;
                            }

                            //директория или файл
                            if ((currentItem.Attributes & FileAttributes.Directory) != 0)
                            {
                                if (directoryFilter != null && !directoryFilter.Invoke((DirectoryInfo) currentItem))
                                {
                                    continue;
                                }

                                if (!currentDirectory.Childs.ContainsKey(currentItem.FullName))
                                {
                                    var childDirectory = DirectoryInfoToDirectoryContext((DirectoryInfo) currentItem, currentDirectory);
                                    currentDirectory.Childs.Add(childDirectory.Name, childDirectory);
                                    this.currentIterationScope.Add(childDirectory);
                                }
                            }
                            else
                            {
                                this.ProcessFile((FileInfo) currentItem, currentDirectory);
                            }
                        }
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    currentDirectory.IsAlive = false;
                    continue;
                }

                catch
                {
                    // IGNORE
                    //здесь могут быть исключения интерпретации атрибутивного состава элементов fs. условно не критично
                    //ToDo add to log
                }


                //логика счетчика изменений директории за последние пять сканирований
                if (currentDirectory.HasFileChanges)
                {
                    currentDirectory.IterationChangesCount =
                        currentDirectory.IterationChangesCount >= 5 ? 5 : currentDirectory.IterationChangesCount + 1;
                }
                else if (currentDirectory.IterationChangesCount > 0)
                {
                    currentDirectory.IterationChangesCount--;
                }
            }
        }

        private void ProcessFile(FileInfo fileInfo, IdxDirectoryInfo directoryContext)
        {
            this.processedFileInIteration++;
            if (this.observerConfiguration.FileFilterPredicate != null)
            {
                if (!this.observerConfiguration.FileFilterPredicate.Invoke(fileInfo))
                {
                    return;
                }
            }
            //   fileInfo.Refresh();

            //время изменения директории задается по времени самого нового файла в ней
            if (directoryContext.ChangeTime < fileInfo.LastWriteTimeUtc)
            {
                directoryContext.ChangeTime = fileInfo.LastWriteTimeUtc;
            }


            if (!directoryContext.Files.TryGetValue(fileInfo.FullName, out var fileContext))
            {
                fileContext = FileInfoToFileContext(fileInfo);
                directoryContext.Files.Add(fileContext.Name, fileContext);
                directoryContext.HasFileChanges = true;
                directoryContext.FileChangeCount++;

                return;
            }

            fileContext.SetIsUpdatedFlag();

            if (fileContext.FileSize != fileInfo.Length || fileContext.FileTime != fileInfo.LastWriteTimeUtc)
            {
                fileContext.FileSize            = fileInfo.Length;
                fileContext.FileTime            = fileInfo.LastWriteTimeUtc;
                fileContext.SourceFileState     = SourceFileState.Changed;
                directoryContext.HasFileChanges = true;
                directoryContext.FileChangeCount++;
            }
        }

        private void DetectLostFiles(IdxDirectoryInfo directoryContext)
        {
            foreach (var fileContext in directoryContext.Files.Values)
            {
                if (!fileContext.GetAndResetIsUpdatedFlag())
                {
                    fileContext.SourceFileState     = SourceFileState.Lost;
                    directoryContext.HasFileChanges = true;
                }
            }
        }

        public void AddRootDirectory(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (this.rootDirectories.ContainsKey(name) || !Directory.Exists(name))
            {
                return;
            }

            var directoryInfo = new DirectoryInfo(name);

            var dirContext = DirectoryInfoToDirectoryContext(directoryInfo, null);
            dirContext.Rate = 10;

            this.rootDirectories.Add(name, dirContext);
            this.firstGenerationQueue.Enqueue(dirContext);
        }

        public void RemoveRootDirectory(string name)
        {
            if (!this.rootDirectories.ContainsKey(name))
            {
                return;
            }

            var rootDirectory = this.rootDirectories[name];
            this.rootDirectories.Remove(name);

            rootDirectory.IsAlive = false;

            //дочерние директории помечаются как удаленные. при обработке очередей они будут забыты
            var lostDirectoriesStack = new Stack<IdxDirectoryInfo>();
            lostDirectoriesStack.Push(rootDirectory);
            while (lostDirectoriesStack.Count > 0)
            {
                var currentLostDirectory = lostDirectoriesStack.Pop();
                currentLostDirectory.IsAlive = false;

                foreach (var childDirectory in currentLostDirectory.Childs.Values)
                {
                    lostDirectoriesStack.Push(childDirectory);
                }
            }
        }

        public void UpdateIndex(CancellationToken cancellationToken, IProducerConsumerCollection<IdxFileInfo> targetCollection)
        {
            this.UpdateIndexInternal(cancellationToken, targetCollection);
        }


        #region FileRoutines

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IdxFileInfoInternal FileInfoToFileContext(FileInfo fileInfo)
        {
            var newFileContext = new IdxFileInfoInternal(
                fileInfo.FullName,
                fileInfo.Length, fileInfo.LastWriteTimeUtc
                , SourceFileState.New);

            return newFileContext;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IdxDirectoryInfo DirectoryInfoToDirectoryContext(DirectoryInfo dirInfo, IdxDirectoryInfo parentContext)
        {
            var dirContext = new IdxDirectoryInfo(dirInfo, parentContext);
            return dirContext;
        }

        #endregion

    }

}