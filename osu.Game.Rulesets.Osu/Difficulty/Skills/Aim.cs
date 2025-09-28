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
    public class Aim : OsuVariableLengthStrainSkill
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

        private double aimMultiplier => 0.98;
        private double snapMultiplier => 31.3;
        private double flowMultiplier => 8;
        private double agilityMultiplier => 0.25;
        private double strainDecayBase => 0.15;

        private readonly List<double> sliderStrains = new List<double>();
        private readonly List<(double Time, double Diff)> previousStrains = new List<(double Time, double Diff)>();

        private const double backwards_strain_influence = 1000;

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double offset, DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;

            double strain = getCurrentStrainValue(offset);

            return strain;
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double currentDifficulty;

            double snapDifficulty = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders, Cheese);
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current);
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current);

            bool isFlow = (flowDifficulty) < (snapDifficulty + agilityDifficulty);

            if (isFlow)
            {
                currentDifficulty = flowDifficulty * flowMultiplier;
            }
            else
            {
                currentDifficulty = snapDifficulty * snapMultiplier + agilityDifficulty * agilityDifficulty;
            }

            currentStrain = getCurrentStrainValue(current.StartTime) * 2.25;
            previousStrains.Add((current.StartTime, currentDifficulty));

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            inaccuraciesWhileCheesing += isInaccurateWhileCheesed(current) * currentStrain;
            if (currentStrain > maxStrain)
                maxStrain = currentStrain;

            return (currentStrain + currentDifficulty) * aimMultiplier;
        }

        private double getCurrentStrainValue(double endTime)
        {
            if (previousStrains.Count < 2)
                return 0;

            double sum = 0;

            double highestNoteVal = 0;
            double prevDeltaTime = 0;

            int index = 1;

            while (index < previousStrains.Count)
            {
                double prevTime = previousStrains[index - 1].Time;
                double currTime = previousStrains[index].Time;

                double deltaTime = currTime - prevTime;
                double prevDifficulty = previousStrains[index - 1].Diff;

                // How much of the current deltaTime does not fall under the backwards strain influence value.
                double startTimeOffset = Math.Max(0, endTime - prevTime - backwards_strain_influence);

                // If the deltaTime doesn't fall into the backwards strain influence value at all, we can remove its corresponding difficulty.
                // We don't iterate index because the list moves backwards.
                if (startTimeOffset > deltaTime)
                {
                    previousStrains.RemoveAt(0);

                    continue;
                }

                highestNoteVal = Math.Max(prevDifficulty, strainDecay(prevDeltaTime));
                prevDeltaTime = deltaTime;

                sum += highestNoteVal * (strainDecayAntiderivative(startTimeOffset) - strainDecayAntiderivative(deltaTime));

                index++;
            }

            // CalculateInitialStrain stuff
            highestNoteVal = Math.Max(previousStrains.Last().Diff, highestNoteVal);
            double lastTime = previousStrains.Last().Time;
            sum += (strainDecayAntiderivative(0) - strainDecayAntiderivative(endTime - lastTime)) * highestNoteVal;

            return sum;

            double strainDecayAntiderivative(double t) => Math.Pow(strainDecayBase, t / 1000) / Math.Log(1.0 / strainDecayBase);
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
