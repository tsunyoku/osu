// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// all code referenced from:
// https://github.com/mathnet/mathnet-numerics/blob/f19641843048df073b80f6ecfcbb229d3258049b/src/Numerics/Optimization/ObjectiveFunctions/ValueObjectiveFunction.cs

using System;

namespace osu.Game.Utils
{
    public class ValueObjectiveFunction
    {
        private readonly Func<double[], double> function;

        public ValueObjectiveFunction(Func<double[], double> function)
        {
            this.function = function;
        }

        public bool IsGradientSupported => false;

        public bool IsHessianSupported => false;

        public void EvaluateAt(double[] point)
        {
            Point = point;
            Value = function(point);
        }

        public double[] Point { get; private set; } = new double[] { };
        public double Value { get; private set; }
    }
}
