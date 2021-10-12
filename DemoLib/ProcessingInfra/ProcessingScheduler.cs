using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DemoLib.ProcessingInfra
{

    /// <summary>
    /// планировщик исполнения процессов обработки
    /// </summary>
    public sealed class ProcessingScheduler
    {

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="threadPoolSize">размер пула рабочих потоков</param>
        /// <param name="cancellationToken">токен прерывания исполнения</param>
        /// <param name="taskCollection">коллекция задач</param>
        public ProcessingScheduler(int threadPoolSize, CancellationToken cancellationToken, IProducerConsumerCollection<ProcessingTask> taskCollection)
        {
            if (threadPoolSize < 3 || threadPoolSize > 50) throw new ArgumentOutOfRangeException(nameof(threadPoolSize));
            this.threadPoolSize    = threadPoolSize;
            this.cancellationToken = cancellationToken;
            this.taskCollection    = taskCollection ?? throw new ArgumentNullException(nameof(taskCollection));

            this.threadPool = new ConcurrentBag<ProcessingThread>();
            this.StartThreadPool();

            var threadScheduler = new Thread(this.SafeThreadScheduler)
            {
                IsBackground = true,

                Name = nameof(ProcessingScheduler) + "-" + nameof(this.ThreadScheduler),
            };

            threadScheduler.Start();
        }

        private readonly int threadPoolSize;

        private readonly CancellationToken cancellationToken;

        private readonly IProducerConsumerCollection<ProcessingTask> taskCollection;

        private readonly ConcurrentBag<ProcessingThread> threadPool;

        private readonly object waitSyncObject = new object();


        private void StartThreadPool()
        {
            while (this.threadPool.Count < this.threadPoolSize)
            {
                var thread = new ProcessingThread(this.cancellationToken, this);
                this.threadPool.Add(thread);
            }
        }

        private void ReturnTaskToCollection(ProcessingTask task)
        {
            this.taskCollection.TryAdd(task);
        }

        private void ReturnThreadToPool(ProcessingThread thread)
        {
            this.threadPool.Add(thread);

            this.WakeUp();
        }


        private void SafeThreadScheduler()
        {
            try
            {
                this.ThreadScheduler();
            }
            catch (Exception)
            {
                // IGNORE
                // TODO: Write 2 Log
            }
        }


        private void ThreadScheduler()
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                ProcessingThread thread = null;
                ProcessingTask   task   = null;
                try
                {
                    if (!this.threadPool.TryTake(out thread))
                    {
                        lock (this.waitSyncObject)
                        {
                            Monitor.Wait(this.waitSyncObject, 10);
                        }

                        continue;
                    }


                    if (!this.taskCollection.TryTake(out task) || task == null)
                    {
                        this.ReturnThreadToPool(thread);

                        lock (this.waitSyncObject)
                        {
                            Monitor.Wait(this.waitSyncObject, 10);
                        }

                        continue;
                    }
                }
                catch (Exception)
                {
                    if (thread != null)
                    {
                        this.ReturnThreadToPool(thread);
                    }

                    continue;

                    // IGNORE
                    // TODO: Write 2 Log
                }

                try
                {
                    if (task.IsComplete)
                    {
                        this.ReturnTaskToCollection(task);
                        this.ReturnThreadToPool(thread);

                        continue;
                    }

                    thread.RunTask(task);
                }
                catch (Exception)
                {
                    this.ReturnTaskToCollection(task);

                    if (thread != null)
                    {
                        this.ReturnThreadToPool(thread);
                    }

                    // IGNORE
                    // TODO: Write 2 Log                }
                }
            }
        }

        /// <summary>
        /// </summary>
        public void WakeUp()
        {
            lock (this.waitSyncObject)
            {
                Monitor.Pulse(this.waitSyncObject);
            }
        }


        private class ProcessingThread
        {

            public ProcessingThread(CancellationToken cancellationToken, ProcessingScheduler scheduler)
            {
                this.cancellationToken = cancellationToken;
                this.scheduler         = scheduler;

                ThreadPool.QueueUserWorkItem(this.SafeThreadPump);
            }

            private readonly CancellationToken cancellationToken;

            private readonly ProcessingScheduler scheduler;

            private readonly ManualResetEvent waiter = new ManualResetEvent(false);

            private ProcessingTask task;

            public int waitCount;

            private void SafeThreadPump(object state)
            {
                ProcessingTask currentTask = null;

                try
                {
                    start:
                    {
                        if (this.cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        try
                        {
                            currentTask = Interlocked.Exchange(ref this.task, null);

                            if (currentTask != null)
                            {
                                currentTask.Execute(this.cancellationToken);
                            }
                        }
                        catch (Exception)
                        {
                            // TODO: Write 2 Log
                            // IGNORE
                        }

                        if (currentTask != null)
                        {
                            this.scheduler.ReturnTaskToCollection(currentTask);
                            this.scheduler.ReturnThreadToPool(this);
                        }
                        else
                        {
                            this.waitCount++;
                            this.waiter.Reset();

                            WaitHandle.WaitAny(new[] {this.cancellationToken.WaitHandle, this.waiter}, 10);
                        }

                        goto start;
                    }
                }
                catch (Exception)
                {
                    // IGNORE
                    // TODO: Write 2 Log
                }
            }

            private void CountStatisticOnBatchComplete()
            {
                //ToDo показать статистику потоков по окончании исполнения пачки тасков
            }


            public void RunTask(ProcessingTask nextTask)
            {
                this.task = nextTask ?? throw new ArgumentNullException(nameof(nextTask));

                this.waiter.Set();
            }

        }

    }

}