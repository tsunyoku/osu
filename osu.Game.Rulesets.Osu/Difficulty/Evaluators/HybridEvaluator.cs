// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class HybridEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous().BaseObject is Spinner)
                return 0;

            double aimDifficulty = evaluateAimDifficultyOf(current);
            double speedDifficulty = SpeedEvaluator.EvaluateDifficultyOf(current);

            if (aimDifficulty <= 0 || speedDifficulty <= 0)
                return 0;

            return Math.Sqrt(aimDifficulty * speedDifficulty);
        }

        private static double evaluateAimDifficultyOf(DifficultyHitObject current)
        {
            const double skill_multiplier_snap = 70.9;
            const double skill_multiplier_agility = 2.35;
            const double skill_multiplier_flow = 242.0;
            const double combined_snap_norm_exponent = 1.2;

            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, true) * skill_multiplier_snap;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current) * skill_multiplier_agility;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, true) * skill_multiplier_flow;

            double combinedSnapDifficulty = DiffUtils.Norm(combined_snap_norm_exponent, snapDifficulty, agilityDifficulty);

            double pSnap = calculateSnapFlowProbability(flowDifficulty / combinedSnapDifficulty);

            return combinedSnapDifficulty * pSnap;
        }

        private static double calculateSnapFlowProbability(double ratio)
        {
            const double k = 7.27;

            if (ratio == 0)
                return 0;

            if (double.IsNaN(ratio))
                return 1;

            return DiffUtils.Logistic(-k * Math.Log(ratio));
        }
    }
}
