using System;

namespace DemoLib.FileIndex
{

    /// <summary>
    /// конфигурация балансировщика нагрузки
    /// </summary>
    public class ObserverBalancerConfiguration
    {

        private TimeSpan firstGenerationAge = TimeSpan.FromSeconds(10);
        private TimeSpan secondGeneRationAge = TimeSpan.FromSeconds(100);

        private int iterationSize = 100;
        private int minIterationSize = 10;
        private double[] generationWeight = new[] {0.5, 0.3, 0.2};

        private TimeSpan preferIterationDuration = TimeSpan.FromSeconds(5);
        private TimeSpan iterationDurationLimit = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Количество диркеторий входящих в итерацию сканирования
        /// </summary>
        public int IterationSize { get { return this.iterationSize; } set { this.iterationSize = value; } }

        /// <summary>
        /// Вес элементов поколения входящих в итерацию
        /// </summary>
        public double[] GenerationWeight { get { return this.generationWeight; } set { this.generationWeight = value; } }

        /// <summary>
        /// возраст первого поколения
        /// </summary>
        public TimeSpan FirstGenerationAge { get { return this.firstGenerationAge; } set { this.firstGenerationAge = value; } }

        /// <summary>
        /// Возраст второго поколения
        /// </summary>
        public TimeSpan SecondGeneRationAge { get { return this.secondGeneRationAge; } set { this.secondGeneRationAge = value; } }

        /// <summary>
        /// Минимальный размер итерации
        /// </summary>
        public int MinIterationSize { get { return this.minIterationSize; } set { this.minIterationSize = value; } }

        /// <summary>
        /// Предпочтительное время итерации сканирования
        /// </summary>
        public TimeSpan PreferIterationDuration { get { return this.preferIterationDuration; } set { this.preferIterationDuration = value; } }

        /// <summary>
        /// Максимальное время итерации сканироавния
        /// </summary>
        public TimeSpan IterationDurationLimit { get { return this.iterationDurationLimit; } set { this.iterationDurationLimit = value; } }

    }

}