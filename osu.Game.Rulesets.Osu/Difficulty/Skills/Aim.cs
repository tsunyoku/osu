// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuStrainSkill
    {
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double currentStrain;

        private double skillMultiplier => 26;
        private double strainDecayBase => 0.15;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;
            var firstMovement = osuCurrent.Movements.First();

            currentStrain *= strainDecay(firstMovement.Time);
            currentStrain += AimEvaluator.EvaluateDifficultyOfMovement(current, firstMovement) * skillMultiplier;

            if (IncludeSliders)
                currentStrain += osuCurrent.Movements.Where(x => x.IsNested).Sum(x => AimEvaluator.EvaluateDifficultyOfMovement(current, x) * skillMultiplier * 1.50);

            for (int i = 1; i < osuCurrent.Movements.Count; i++)
            {
                var movement = osuCurrent.Movements[i];

                // always apply strain decay to make circle-only strains decay at the same speed as slider stains
                currentStrain *= strainDecay(movement.Time);

                if (IncludeSliders && movement.IsNested)
                {
                    //currentStrain += AimEvaluator.EvaluateDifficultyOfMovement(current, movement) * skillMultiplier * 0.10;
                }
            }

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            return currentStrain;
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
