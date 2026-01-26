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
using osu.Game.Rulesets.Osu.Mods;
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

        private double currentAimStrain;

        private double skillMultiplierAim => 26.0;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecayAim(double ms) => Math.Pow(0.15, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current)
            => currentAimStrain * strainDecayAim(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double decayAim = strainDecayAim(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            double snapAimDifficulty = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);
            double flowAimDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);

            double aimDifficulty = Math.Min(snapAimDifficulty, flowAimDifficulty);

            if (Mods.Any(m => m is OsuModTouchDevice))
            {
                aimDifficulty = Math.Pow(aimDifficulty, 0.8);
            }

            currentAimStrain *= decayAim;
            currentAimStrain += aimDifficulty * (1 - decayAim) * skillMultiplierAim;

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentAimStrain);

            return currentAimStrain;
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

        public double CountTopWeightedSliders(double difficultyValue)
            => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, difficultyValue);
    }
}
