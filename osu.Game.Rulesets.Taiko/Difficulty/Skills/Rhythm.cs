// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Taiko.Difficulty.Evaluators;
using osu.Game.Rulesets.Taiko.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Taiko.Difficulty.Skills
{
    /// <summary>
    /// Calculates the rhythm coefficient of taiko difficulty.
    /// </summary>
    public class Rhythm : StrainDecaySkill<TaikoDifficultyHitObject>
    {
        protected override double SkillMultiplier => 1.0;
        protected override double StrainDecayBase => 0.4;

        protected override double StrainValueOf(TaikoDifficultyHitObject current)
        {
            double difficulty = RhythmEvaluator.EvaluateDifficultyOf(current);

            // To prevent abuse of exceedingly long intervals between awkward rhythms, we penalise its difficulty.
            double staminaDifficulty = StaminaEvaluator.EvaluateDifficultyOf(current) - 0.5; // Remove base strain
            difficulty *= DifficultyCalculationUtils.Logistic(staminaDifficulty, 1 / 15.0, 50.0);

            return difficulty;
        }
    }
}
