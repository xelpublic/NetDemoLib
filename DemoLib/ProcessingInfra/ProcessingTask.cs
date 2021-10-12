using System.Threading;

namespace DemoLib.ProcessingInfra
{

    /// <summary>
    /// базовый класс задач процессинга
    /// </summary>
    public abstract class ProcessingTask
    {

        /// <summary>
        /// Признак завершения исполнения задачи
        /// </summary>
        public abstract bool IsComplete { get; }

        /// <summary>
        /// Имплементация логики задачи
        /// </summary>
        /// <param name="cancellationToken"></param>
        protected abstract void ExecuteInternal(CancellationToken cancellationToken);

        /// <summary>
        /// Сбросить признак завершения исполнения задачи
        /// </summary>
        public abstract void ResetIsComplete();


        /// <summary>
        /// Точка входа исполнения логики задачи
        /// </summary>
        /// <param name="cancellationToken"></param>
        public void Execute(CancellationToken cancellationToken)
        {
            try
            {
                this.ExecuteInternal(cancellationToken);
            }
            catch
            {
                // IGNORE
            }
        }

    }

}