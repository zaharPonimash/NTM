﻿using System;
using System.Runtime.Serialization;
using NTM2.Learning;
using NTM2.Memory;

namespace NTM2.Controller
{
    [KnownType(typeof(SigmoidActivationFunction))]
    [DataContract]
    internal class HiddenLayer
    {
        #region Fields and variables
        
        [DataMember]
        private readonly IDifferentiableFunction _activationFunction;

        [DataMember]
        private readonly int _controllerSize;
        [DataMember]
        private readonly int _inputSize;
        [DataMember]
        private readonly int _headCount;
        [DataMember]
        private readonly int _memoryUnitSizeM;

        //Controller hidden layer threshold weights
        [DataMember]
        private readonly Unit[] _hiddenLayerThresholds;

        //Weights from input to controller
        [DataMember]
        private readonly Unit[][] _inputToHiddenLayerWeights;

        //Weights from read data to controller
        [DataMember]
        private readonly Unit[][][] _readDataToHiddenLayerWeights;

        //Hidden layer weights
        [DataMember]
        internal readonly Unit[] HiddenLayerNeurons;

        #endregion

        #region Ctor

        public HiddenLayer(int controllerSize, int inputSize, int headCount, int memoryUnitSizeM)
        {
            _controllerSize = controllerSize;
            _inputSize = inputSize;
            _headCount = headCount;
            _memoryUnitSizeM = memoryUnitSizeM;
            _activationFunction = new SigmoidActivationFunction();

            _readDataToHiddenLayerWeights = UnitFactory.GetTensor3(controllerSize, headCount, memoryUnitSizeM);
            _inputToHiddenLayerWeights = UnitFactory.GetTensor2(controllerSize, inputSize);
            _hiddenLayerThresholds = UnitFactory.GetVector(controllerSize);
        }

        private HiddenLayer(Unit[][][] readDataToHiddenLayerWeights, Unit[][] inputToHiddenLayerWeights, Unit[] hiddenLayerThresholds, Unit[] hiddenLayer, int controllerSize, int inputSize, int headCount, int memoryUnitSizeM, IDifferentiableFunction activationFunction)
        {
            _readDataToHiddenLayerWeights = readDataToHiddenLayerWeights;
            _inputToHiddenLayerWeights = inputToHiddenLayerWeights;
            _hiddenLayerThresholds = hiddenLayerThresholds;
            HiddenLayerNeurons = hiddenLayer;
            _controllerSize = controllerSize;
            _inputSize = inputSize;
            _headCount = headCount;
            _memoryUnitSizeM = memoryUnitSizeM;
            _activationFunction = activationFunction;
        }
        
        public HiddenLayer Clone()
        {
            return new HiddenLayer(_readDataToHiddenLayerWeights, _inputToHiddenLayerWeights,
                                   _hiddenLayerThresholds, UnitFactory.GetVector(_controllerSize),
                                   _controllerSize, _inputSize, _headCount, _memoryUnitSizeM, _activationFunction);
        }

        #endregion

        #region Forward propagation

        //TODO refactor - do not use tempsum - but beware of rounding issues

        public void ForwardPropagation(double[] input, ReadData[] readData)
        {
            //Foreach neuron in hidden layer
            for (int neuronIndex = 0; neuronIndex < _controllerSize; neuronIndex++)
            {
                double sum = 0;
                sum = GetReadDataContributionToHiddenLayer(neuronIndex, readData, sum);
                sum = GetInputContributionToHiddenLayer(neuronIndex, input, sum);
                sum = GetThresholdContributionToHiddenLayer(neuronIndex, sum);

                //Set new controller unit value
                HiddenLayerNeurons[neuronIndex].Value = _activationFunction.Value(sum);
            }
        }

        private double GetReadDataContributionToHiddenLayer(int neuronIndex, ReadData[] readData, double tempSum)
        {
            Unit[][] readWeightsForEachHead = _readDataToHiddenLayerWeights[neuronIndex];
            for (int headIndex = 0; headIndex < _headCount; headIndex++)
            {
                Unit[] headWeights = readWeightsForEachHead[headIndex];
                ReadData read = readData[headIndex];

                for (int memoryCellIndex = 0; memoryCellIndex < _memoryUnitSizeM; memoryCellIndex++)
                {
                    tempSum += headWeights[memoryCellIndex].Value * read.ReadVector[memoryCellIndex].Value;
                }
            }
            return tempSum;
        }

        private double GetInputContributionToHiddenLayer(int neuronIndex, double[] input, double tempSum)
        {
            Unit[] inputWeights = _inputToHiddenLayerWeights[neuronIndex];
            for (int j = 0; j < inputWeights.Length; j++)
            {
                tempSum += inputWeights[j].Value * input[j];
            }
            return tempSum;
        }

        private double GetThresholdContributionToHiddenLayer(int neuronIndex, double tempSum)
        {
            tempSum += _hiddenLayerThresholds[neuronIndex].Value;
            return tempSum;
        }

        #endregion

        #region Update weights

        public void UpdateWeights(Action<Unit> updateAction)
        {
            Action<Unit[]> vectorUpdateAction = Unit.GetVectorUpdateAction(updateAction);
            Action<Unit[][]> tensor2UpdateAction = Unit.GetTensor2UpdateAction(updateAction);
            Action<Unit[][][]> tensor3UpdateAction = Unit.GetTensor3UpdateAction(updateAction);

            tensor3UpdateAction(_readDataToHiddenLayerWeights);
            tensor2UpdateAction(_inputToHiddenLayerWeights);
            vectorUpdateAction(_hiddenLayerThresholds);
        }


        public void UpdateWeights(IWeightUpdater weightUpdater)
        {
            weightUpdater.UpdateWeight(_readDataToHiddenLayerWeights);
            weightUpdater.UpdateWeight(_inputToHiddenLayerWeights);
            weightUpdater.UpdateWeight(_hiddenLayerThresholds);
        }

        #endregion

        #region BackwardErrorPropagation

        public void BackwardErrorPropagation(double[] input, ReadData[] reads)
        {
            double[] hiddenLayerGradients = CalculateHiddenLayerGradinets();

            UpdateReadDataGradient(hiddenLayerGradients, reads);

            UpdateInputToHiddenWeightsGradients(hiddenLayerGradients, input);

            UpdateHiddenLayerThresholdsGradients(hiddenLayerGradients);
        }

        private double[] CalculateHiddenLayerGradinets()
        {
            double[] hiddenLayerGradients = new double[HiddenLayerNeurons.Length];
            for (int i = 0; i < HiddenLayerNeurons.Length; i++)
            {
                Unit unit = HiddenLayerNeurons[i];
                //TODO use derivative of activation function
                //hiddenLayerGradients[i] = unit.Gradient * _activationFunction.Derivative(unit.Value)
                hiddenLayerGradients[i] = unit.Gradient * unit.Value * (1 - unit.Value);
            }
            return hiddenLayerGradients;
        }

        private void UpdateReadDataGradient(double[] hiddenLayerGradients, ReadData[] reads)
        {
            for (int neuronIndex = 0; neuronIndex < _controllerSize; neuronIndex++)
            {
                Unit[][] neuronToReadDataWeights = _readDataToHiddenLayerWeights[neuronIndex];
                double hiddenLayerGradient = hiddenLayerGradients[neuronIndex];

                for (int headIndex = 0; headIndex < _headCount; headIndex++)
                {
                    ReadData readData = reads[headIndex];
                    Unit[] neuronToHeadReadDataWeights = neuronToReadDataWeights[headIndex];
                    for (int memoryCellIndex = 0; memoryCellIndex < _memoryUnitSizeM; memoryCellIndex++)
                    {
                        readData.ReadVector[memoryCellIndex].Gradient += hiddenLayerGradient * neuronToHeadReadDataWeights[memoryCellIndex].Value;

                        neuronToHeadReadDataWeights[memoryCellIndex].Gradient += hiddenLayerGradient * readData.ReadVector[memoryCellIndex].Value;
                    }
                }
            }
        }

        private void UpdateInputToHiddenWeightsGradients(double[] hiddenLayerGradients, double[] input)
        {
            for (int neuronIndex = 0; neuronIndex < _controllerSize; neuronIndex++)
            {
                double hiddenGradient = hiddenLayerGradients[neuronIndex];
                Unit[] inputToHiddenNeuronWeights = _inputToHiddenLayerWeights[neuronIndex];

                UpdateInputGradient(hiddenGradient, inputToHiddenNeuronWeights, input);
            }
        }

        private void UpdateInputGradient(double hiddenLayerGradient, Unit[] inputToHiddenNeuronWeights, double[] input)
        {
            for (int inputIndex = 0; inputIndex < _inputSize; inputIndex++)
            {
                inputToHiddenNeuronWeights[inputIndex].Gradient += hiddenLayerGradient * input[inputIndex];
            }
        }

        private void UpdateHiddenLayerThresholdsGradients(double[] hiddenLayerGradients)
        {
            for (int neuronIndex = 0; neuronIndex < _controllerSize; neuronIndex++)
            {
                _hiddenLayerThresholds[neuronIndex].Gradient += hiddenLayerGradients[neuronIndex];
            }
        }

        #endregion

    }
}
