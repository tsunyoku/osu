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
        public readonly bool Cheese;
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders, bool cheese)
            : base(mods)
        {
            Cheese = cheese;
            IncludeSliders = includeSliders;
        }

        private double inaccuraciesWhileCheesing = 0;
        private double maxStrain = 0;
        private double currentStrain;

        private double currentAgilityStrain;
        private double aimMultiplier => 0.98;
        private double snapMultiplier => 31.3;
        private double flowMultiplier => 8;
        private double agilityMultiplier => 0.25;
        private double strainDecayBase => 0.15;
        private double agilityStrainDecayBase => 0.1;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        private double agilityStrainDecay(double ms) => Math.Pow(agilityStrainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentAgilityStrain *= agilityStrainDecay(current.DeltaTime);

            double currentDifficulty;
            double snapDifficulty = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders, Cheese);
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current);
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current);

            bool isFlow = (flowDifficulty) < (snapDifficulty + agilityDifficulty);

            if (isFlow)
            {
                currentDifficulty = flowDifficulty * flowMultiplier;
                currentStrain += currentDifficulty;
            }
            else
            {
                currentDifficulty = snapDifficulty * snapMultiplier;
                currentAgilityStrain += agilityDifficulty * agilityMultiplier;
                currentStrain += currentDifficulty + currentAgilityStrain;
            }

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            inaccuraciesWhileCheesing += isInaccurateWhileCheesed(current) * currentStrain;
            if (currentStrain > maxStrain)
                maxStrain = currentStrain;

            return currentStrain * aimMultiplier;
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

        public double GetInaccuraciesWithCheesing() => maxStrain > 0 ? inaccuraciesWhileCheesing / maxStrain : 0;

        private static int isInaccurateWhileCheesed(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;

            // Assume even on Lazer that cheesing does not happen on sliders
            if (osuCurrObj.BaseObject is Slider)
                return 0;

            return osuCurrObj.ExtraDeltaTime > osuCurrObj.HitWindowGreat ? 1 : 0;
        }
    }
}
