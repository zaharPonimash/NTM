﻿using AForge.Genetic;
using AForge.Math.Metrics;
using NeuralTuringMachine.Memory;
using NeuralTuringMachine.Memory.Head;

namespace NeuralTuringMachine.GeneticsOptimalization
{
    public class IdealReadHeadOutputFitnessFunction : IFitnessFunction
    {
        private readonly double[] _idealWeightVector;
        private readonly NtmMemory _memory;
        private readonly ReadHeadWithFixedLastWeights _readHead;
        private readonly IDistance _distance;

        public IdealReadHeadOutputFitnessFunction(double[] idealWeightVector, double[] lastWeightVector, NtmMemory memory)
        {
            _idealWeightVector = idealWeightVector;
            _memory = memory;
            _readHead = new ReadHeadWithFixedLastWeights(lastWeightVector, memory.MemorySettings);
            _distance = new EuclideanDistance();
        }

        public double Evaluate(IChromosome chromosome)
        {
            DoubleArrayChromosome doubleArrayChromosome = (DoubleArrayChromosome)chromosome;
            double[] addressingData = doubleArrayChromosome.Value;

            _readHead.UpdateAddressingData(addressingData);
            double[] weightVector = _readHead.GetWeightVector(_memory);
            
            double fitness = _distance.GetDistance(weightVector, _idealWeightVector);
            
            return 1 / (1 + fitness);
        }
    }
}
