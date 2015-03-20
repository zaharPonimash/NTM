﻿namespace NTM2.Learning
{
    interface INTMTeacher
    {
        double[][] Train(double[][] input, double[][] knownOutput);
        double[][][] Train(double[][][] inputs, double[][][] knownOutputs);
    }
}
