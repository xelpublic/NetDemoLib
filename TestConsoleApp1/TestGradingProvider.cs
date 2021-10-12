using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using DemoLib.FileIndex;

namespace TestConsoleApp1
{

    public static class TestGradingProvider
    {

        private static string testPath;
        private const int DirCount = 10;
        private const int FileCount = 1000;
        private const int TestIterationCount = 100;

        public static void DoTestGrader()
        {

            testPath = Path.Combine(Path.GetTempPath(), @"\TestGradingProvider");
            Directory.CreateDirectory(testPath);

            var observerConfig = new FileGraderConfiguration(null, null, new ObserverBalancerConfiguration());

            var grader = new DrivenGradingProvider(observerConfig);

            grader.AddScanDirectory(testPath);

            var cancellationToken = new CancellationTokenSource();
            var processStopwatch  = new Stopwatch();

            ChangeFile(TimeSpan.FromSeconds(500000));

            var iterationCount = TestIterationCount;
            while (iterationCount>0)
            {
                var targetList = new ConcurrentQueue<IdxFileInfo>();
                MakeTestData(DirCount, FileCount);
                ChangeFile(TimeSpan.FromSeconds(50));

                processStopwatch.Restart();
                grader.UpdateIndex(cancellationToken.Token, targetList);
                processStopwatch.Stop();
                Console.WriteLine($"создано файлов {FileCount}, найдено {targetList.Count}, время {processStopwatch.ElapsedMilliseconds} ms");

                Thread.Sleep(2000);//задержка чтобы прошла смена поколений
                //  Console.ReadLine();

                iterationCount--;
            }
            Directory.Delete(testPath);

        }

        private static void MakeTestData(int dirCount, int fileCount)
        {
            var rootDir = new DirectoryInfo(testPath);

            var currentDir = SelectRandomDir(rootDir, -1);
            for (int a = 0; a < dirCount; a++)
            {
                var childDir = currentDir.CreateSubdirectory(Guid.NewGuid().ToString());

                for (int b = 0; b < fileCount / dirCount; b++)
                {
                    MakeFile(childDir);
                }
            }

        }

        private static readonly Random Rnd = new Random();

        private static void MakeFile(DirectoryInfo directory)
        {
            try
            {
                var fileSize = Rnd.Next(500);
                var buffer   = new byte[fileSize];
                new Random().NextBytes(buffer);
                var text = System.Text.Encoding.Default.GetString(buffer);
                File.WriteAllText(Path.Combine(directory.FullName, Guid.NewGuid().ToString() + ".txt"), text);

            }
            catch
            {
                //IGNORE
            }
        }

        private static void ChangeFile(TimeSpan targetAge)
        {
            var targetFile = SelectFreshFile(new DirectoryInfo(testPath), targetAge);
            if (ReferenceEquals(targetFile, null))
            {
                return;
            }

            var fileSize = Rnd.Next(5);
            var buffer   = new byte[fileSize];
            new Random().NextBytes(buffer);
            var text = System.Text.Encoding.Default.GetString(buffer);
            File.AppendAllText(targetFile.FullName, text);
            Console.WriteLine($"файл {targetFile.Name} изменен");

        }

        private static FileInfo SelectFreshFile(DirectoryInfo rootDir, TimeSpan targetAge)
        {
            var dirStack = new Stack<DirectoryInfo>();
            dirStack.Push(rootDir);
            var timePoint = DateTime.UtcNow;

            while (dirStack.Count > 0)
            {
                var currentDir = dirStack.Pop();
                using (var fileSystemItemsEnumerator = currentDir.EnumerateFileSystemInfos().GetEnumerator())
                {
                    try
                    {
                        if (!fileSystemItemsEnumerator.MoveNext())
                        {
                            break;
                        }
                    }
                    catch
                    {
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
                        dirStack.Push((DirectoryInfo) currentItem);
                    }
                    else
                    {
                        if (timePoint - currentItem.LastWriteTimeUtc <= targetAge)
                        {
                            return (FileInfo) currentItem;
                        }

                    }


                }

            }

            return null;
        }

        private static DirectoryInfo SelectRandomDir(DirectoryInfo rootDir, int deep)
        {
            if (deep == -1)
            {
                deep = Rnd.Next(5);
            }

            foreach (var directory in rootDir.EnumerateDirectories())
            {
                if (Rnd.Next(3) == 2)
                {
                    if (deep == 0)
                    {
                        return directory;
                    }

                    return SelectRandomDir(directory, deep - 1);
                }

            }

            return rootDir;

        }


    }

}