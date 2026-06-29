// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Taiko.Difficulty.Evaluators;
using osu.Game.Rulesets.Taiko.Difficulty.Preprocessing;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.Taiko.Difficulty.Skills
{
    /// <summary>
    /// Calculates the reading coefficient of taiko difficulty.
    /// </summary>
    public class Reading : StrainSkill
    {
        private double currentStrain;

        // TODO: this almost certainly should not exist.
        private double currentDifficulty;

        private const double strain_decay_base = 0.4;

        private double strainDecay(double ms) => DiffUtils.Pow(strain_decay_base, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);

            // Drum Rolls and Swells are exempt.
            if (current.BaseObject is not Hit)
            {
                return 0.0;
            }

            var taikoObject = (TaikoDifficultyHitObject)current;
            int index = taikoObject.ColourData.MonoStreak?.HitObjects.IndexOf(taikoObject) ?? 0;

            currentDifficulty *= DiffUtils.Logistic(index, 4, -1 / 25.0, 0.5) + 0.5;

            // TODO: wtf?????????????
            currentDifficulty *= strain_decay_base;

            currentDifficulty += ReadingEvaluator.EvaluateDifficultyOf(taikoObject);

            currentStrain += currentDifficulty;

            return currentStrain;
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);
    }
}
