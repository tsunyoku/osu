// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Taiko.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Taiko.Difficulty.Skills
{
    /// <summary>
    /// Calculates the colour coefficient of taiko difficulty.
    /// </summary>
    public class Colour : StrainSkill
    {
        private double currentStrain;

        // This is set to decay slower than other skills, due to the fact that only the first note of each encoding class
        // having any difficulty values, and we want to allow colour difficulty to be able to build up even on
        // slower maps.
        private double strainDecay(double ms) => DiffUtils.Pow(0.8, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            const double skill_multiplier = 0.12;

            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += ColourEvaluator.EvaluateDifficultyOf(current) * skill_multiplier;

            return currentStrain;
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);
    }
}
