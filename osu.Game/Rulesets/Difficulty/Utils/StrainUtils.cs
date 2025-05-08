// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Difficulty.Utils
{
    public static class StrainUtils
    {
        public static double CountTopWeightedStrains(IReadOnlyCollection<double> objectStrains, double difficultyValue)
        {
            if (objectStrains.Count == 0)
                return 0.0;

            double consistentTopStrain = difficultyValue / 10; // What would the top strain be if all strain values were identical

            if (consistentTopStrain == 0)
                return objectStrains.Count;

            // Use a weighted sum of all strains. Constants are arbitrary and give nice values
            return objectStrains.Sum(s => 1.1 / (1 + Math.Exp(-10 * (s / consistentTopStrain - 0.88))));
        }
    }
}
