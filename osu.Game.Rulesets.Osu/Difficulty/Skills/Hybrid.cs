// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Hybrid : HarmonicSkill
    {
        private double currentStrain;

        public Hybrid(Mod[] mods)
            : base(mods)
        {
        }

        private double strainDecay(double ms) => DiffUtils.Pow(0.2, ms / 1000);

        protected override double ObjectDifficultyOf(DifficultyHitObject current)
        {
            if (Mods.Any(m => m is OsuModRelax || m is OsuModAutopilot))
                return 0;

            const double skill_multiplier = 1;

            double decay = strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            currentStrain *= decay;
            currentStrain += HybridEvaluator.EvaluateDifficultyOf(current) * (1 - decay) * skill_multiplier;

            return currentStrain;
        }
    }
}
