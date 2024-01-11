// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

// all code referenced from:
// https://github.com/mathnet/mathnet-numerics/blob/master/src/Numerics/Optimization/NelderMeadSimplex.cs

/*
 Copyright (c) 2002-2022 Math.NET

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

namespace osu.Game.Utils
{
    public static class NelderMeadSimplex
    {
        private const double jitter = 1e-10d;

        /// <summary>
        /// Finds the minimum of the objective function without an initial perturbation, the default values used
        /// by fminsearch() in Matlab are used instead
        /// http://se.mathworks.com/help/matlab/math/optimizing-nonlinear-functions.html#bsgpq6p-11
        /// </summary>
        /// <param name="objectiveFunction">The objective function, no gradient or hessian needed</param>
        /// <param name="initialGuess">The initial guess</param>
        /// <param name="convergenceTolerance">The convergence tolerance</param>
        /// <param name="maximumIterations">The maximum iterations</param>
        /// <returns>The minimum point</returns>
        public static double Minimum(ValueObjectiveFunction objectiveFunction, double initialGuess, double convergenceTolerance = 1e-8, int maximumIterations = 1000)
        {
            double initialPerturbation = initialGuess == 0.0 ? 0.00025 : initialGuess * 0.05;

            return Minimum(objectiveFunction, initialGuess, initialPerturbation, convergenceTolerance, maximumIterations);
        }

        public static double Minimum(ValueObjectiveFunction objectiveFunction, double initialGuess, double initialPerturbation, double convergenceTolerance = 1e-8, int maximumIterations = 1000)
        {
            // we only allow taking in a single guess, and `numVertices` is equal to `numDimensions + 1` which always calculates to 2 in this case.
            const int num_vertices = 2;

            double[][] vertices = initializeVertices(initialGuess, initialPerturbation);

            int evaluationCount = 0;
            ErrorProfile errorProfile;

            double[] errorValues = initializeErrorValues(vertices, objectiveFunction);
            int numTimesHasConverged = 0;

            // iterate until we converge, or complete our permitted number of iterations
            while (true)
            {
                errorProfile = evaluateSimplex(errorValues);

                if (hasConverged(convergenceTolerance, errorProfile, errorValues))
                {
                    numTimesHasConverged++;
                }
                else
                {
                    numTimesHasConverged = 0;
                }

                if (numTimesHasConverged == 2)
                {
                    break;
                }

                // attempt a reflection of the simplex
                double reflectionPointValue = tryToScaleSimplex(-1.0, ref errorProfile, vertices, errorValues, objectiveFunction);
                ++evaluationCount;

                if (reflectionPointValue <= errorValues[errorProfile.LowestIndex])
                {
                    // it's better than the be st point, so attempt an expansion of the simplex
                    tryToScaleSimplex(2.0, ref errorProfile, vertices, errorValues, objectiveFunction);
                    ++evaluationCount;
                }
                else if (reflectionPointValue >= errorValues[errorProfile.NextHighestIndex])
                {
                    // it would be worse than the second best point, so attempt a contraction to look
                    // for an intermediate point
                    double currentWorst = errorValues[errorProfile.HighestIndex];
                    double contractionPointValue = tryToScaleSimplex(0.5, ref errorProfile, vertices, errorValues, objectiveFunction);
                    ++evaluationCount;

                    if (contractionPointValue >= currentWorst)
                    {
                        // that would be even worse, so let's try to contract uniformly towards the low point;
                        // don't bother to update the error profile, we'll do it at the start of the
                        // next iteration
                        shrinkSimplex(errorProfile, vertices, errorValues, objectiveFunction);
                        evaluationCount += num_vertices; // that required one function evaluation for each vertex; keep track
                    }
                }

                // check to see if we have exceeded our alloted number of evaluations
                if (evaluationCount >= maximumIterations)
                {
                    throw new InvalidOperationException($"Exceeded maximum ({maximumIterations}) simplex evaluations");
                }
            }

            objectiveFunction.EvaluateAt(vertices[errorProfile.LowestIndex]);
            return objectiveFunction.Point[0];
        }

        private static void shrinkSimplex(ErrorProfile errorProfile, double[][] vertices, double[] errorValues, ValueObjectiveFunction objectiveFunction)
        {
            double[] lowestVertex = vertices[errorProfile.LowestIndex];

            for (int i = 0; i < vertices.Length; i++)
            {
                if (i != errorProfile.LowestIndex)
                {
                    vertices[i] = (vertices[i].AddVector(lowestVertex)).Multiply(0.5);
                    objectiveFunction.EvaluateAt(vertices[i]);
                    errorValues[i] = objectiveFunction.Value;
                }
            }
        }

        private static double tryToScaleSimplex(double scaleFactor, ref ErrorProfile errorProfile, double[][] vertices, double[] errorValues, ValueObjectiveFunction objectiveFunction)
        {
            // find the centroid through which we will reflect
            double[] centroid = computeCentroid(vertices, errorProfile);

            // define the vector from the centroid to the high point
            double[] centroidToHighPoint = vertices[errorProfile.HighestIndex].Subtract(centroid);

            // scale and position the vector to determine the new trial point
            double[] newPoint = centroidToHighPoint.Multiply(scaleFactor).AddVector(centroid);

            // evaluate the new point
            objectiveFunction.EvaluateAt(newPoint);
            double newErrorValue = objectiveFunction.Value;

            // if it's better, replace the old high point
            if (newErrorValue < errorValues[errorProfile.HighestIndex])
            {
                vertices[errorProfile.HighestIndex] = newPoint;
                errorValues[errorProfile.HighestIndex] = newErrorValue;
            }

            return newErrorValue;
        }

        private static double[] computeCentroid(double[][] vertices, ErrorProfile errorProfile)
        {
            int numVertices = vertices.Length;

            List<double> centroid = new List<double>(numVertices - 1);

            for (int i = 0; i < numVertices; i++)
            {
                if (i != errorProfile.HighestIndex)
                {
                    centroid.Add(0);
                    centroid = centroid.AddVector(vertices[i]);
                }
            }

            return centroid.Multiply(1.0d / (numVertices - 1));
        }

        private static ErrorProfile evaluateSimplex(double[] errorValues)
        {
            ErrorProfile errorProfile = new ErrorProfile();

            if (errorValues[0] > errorValues[1])
            {
                errorProfile.HighestIndex = 0;
                errorProfile.NextHighestIndex = 1;
            }
            else
            {
                errorProfile.HighestIndex = 1;
                errorProfile.NextHighestIndex = 0;
            }

            for (int index = 0; index < errorValues.Length; index++)
            {
                double errorValue = errorValues[index];

                if (errorValue <= errorValues[errorProfile.LowestIndex])
                {
                    errorProfile.LowestIndex = index;
                }

                if (errorValue > errorValues[errorProfile.HighestIndex])
                {
                    errorProfile.NextHighestIndex = errorProfile.HighestIndex; // downgrade the current highest to next highest
                    errorProfile.HighestIndex = index;
                }
                else if (errorValue > errorValues[errorProfile.NextHighestIndex] && index != errorProfile.HighestIndex)
                {
                    errorProfile.NextHighestIndex = index;
                }
            }

            return errorProfile;
        }

        private static bool hasConverged(double convergenceTolerance, ErrorProfile errorProfile, double[] errorValues)
        {
            double range = 2 * Math.Abs(errorValues[errorProfile.HighestIndex] - errorValues[errorProfile.LowestIndex]) /
                           (Math.Abs(errorValues[errorProfile.HighestIndex]) + Math.Abs(errorValues[errorProfile.LowestIndex]) + jitter);

            return range < convergenceTolerance;
        }

        private static double[] initializeErrorValues(double[][] vertices, ValueObjectiveFunction valueObjectiveFunction)
        {
            double[] errorValues = new double[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                valueObjectiveFunction.EvaluateAt(vertices[i]);
                errorValues[i] = valueObjectiveFunction.Value;
            }

            return errorValues;
        }

        private static double[][] initializeVertices(double value, double initialPerturbation)
        {
            // we only allow taking in a single guess, and `numVertices` is equal to `numDimensions + 1` which always calculates to 2 in this case.
            double[][] vertices = new[]
            {
                new[] { value },
                new[] { value + (1 * initialPerturbation) }
            };

            return vertices;
        }

        private sealed class ErrorProfile
        {
            public int HighestIndex { get; set; }
            public int NextHighestIndex { get; set; }
            public int LowestIndex { get; set; }
        }
    }

    public static class Extensions
    {
        public static double[] Subtract(this double[] current, double[] other)
        {
            var result = new List<double>();

            for (int i = 0; i < current.Length; i++)
            {
                double negated = current[i] - other[i];
                result.Add(negated);
            }

            return result.ToArray();
        }

        public static List<double> AddVector(this List<double> current, double[] other)
        {
            var result = new List<double>();

            for (int i = 0; i < current.Count; i++)
            {
                double sum = current[i] + other[i];
                result.Add(sum);
            }

            return result;
        }

        public static double[] AddVector(this double[] current, double[] other)
        {
            var result = new List<double>();

            for (int i = 0; i < current.Length; i++)
            {
                double sum = current[i] + other[i];
                result.Add(sum);
            }

            return result.ToArray();
        }

        public static double[] Multiply(this List<double> current, double other)
        {
            var result = new List<double>();

            for (int i = 0; i < current.Count; i++)
            {
                double sum = current[i] * other;
                result.Add(sum);
            }

            return result.ToArray();
        }

        public static double[] Multiply(this double[] current, double other)
        {
            var result = new List<double>();

            for (int i = 0; i < current.Length; i++)
            {
                double sum = current[i] * other;
                result.Add(sum);
            }

            return result.ToArray();
        }
    }
}
