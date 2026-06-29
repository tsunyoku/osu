// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Taiko.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Taiko.Difficulty.Skills
{
    /// <summary>
    /// Calculates the rhythm coefficient of taiko difficulty.
    /// </summary>
    public class Rhythm : StrainSkill
    {
        private double currentStrain;

        private double strainDecay(double ms) => DiffUtils.Pow(0.4, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);

            double difficulty = RhythmEvaluator.EvaluateDifficultyOf(current);

            // To prevent abuse of exceedingly long intervals between awkward rhythms, we penalise its difficulty.
            double staminaDifficulty = StaminaEvaluator.EvaluateDifficultyOf(current) - 0.5; // Remove base strain
            difficulty *= DiffUtils.Logistic(staminaDifficulty, 1 / 15.0, 50.0);

            currentStrain += difficulty;

            return currentStrain;
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);
    }
}
