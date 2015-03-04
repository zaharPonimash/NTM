﻿using System;
using NTM2.Memory;

namespace NTM2.Controller
{
    interface IController
    {
        void ForwardPropagation(double[] input, ReadData[] readData);
        void UpdateWeights(Action<Unit> updateAction);
        void BackwardErrorPropagation(double[] input, ReadData[] reads);
        IController Clone();
    }
}