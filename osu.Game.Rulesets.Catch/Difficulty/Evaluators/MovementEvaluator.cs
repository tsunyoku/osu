// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class MovementEvaluator
    {
        private const double direction_change_bonus = 21.0;

        public static double EvaluateDifficultyOf(CatchDifficultyHitObject current)
        {
            var last = (CatchDifficultyHitObject)current.Previous(0);
            var lastLast = (CatchDifficultyHitObject)current.Previous(1);

            // In catch, clockrate adjustments do not only affect the timings of hitobjects,
            // but also the speed of the player's catcher, which has an impact on difficulty
            double catcherSpeedMultiplier = current.ClockRate;

            double weightedStrainTime = current.StrainTime + 13 + (3 / catcherSpeedMultiplier);

            double distanceAddition = (Math.Pow(Math.Abs(current.DistanceMoved), 1.3) / 510);
            double sqrtStrain = Math.Sqrt(weightedStrainTime);

            double edgeDashBonus = 0;

            // Direction change bonus.
            if (Math.Abs(current.DistanceMoved) > 0.1)
            {
                if (current.Index >= 1 && Math.Abs(last.DistanceMoved) > 0.1 && Math.Sign(current.DistanceMoved) != Math.Sign(last.DistanceMoved))
                {
                    double bonusFactor = Math.Min(50, Math.Abs(current.DistanceMoved)) / 50;
                    double antiflowFactor = Math.Max(Math.Min(70, Math.Abs(last.DistanceMoved)) / 70, 0.38);

                    distanceAddition += direction_change_bonus / Math.Sqrt(last.StrainTime + 16) * bonusFactor * antiflowFactor * Math.Max(1 - Math.Pow(weightedStrainTime / 1000, 3), 0);
                }

                // Base bonus for every movement, giving some weight to streams.
                distanceAddition += 12.5 * Math.Min(Math.Abs(current.DistanceMoved), CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2)
                                    / (CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 6) / sqrtStrain;
            }

            // Bonus for edge dashes.
            if (current.LastObject.DistanceToHyperDash <= 20.0f)
            {
                if (!current.LastObject.HyperDash)
                    edgeDashBonus += 5.7;

                distanceAddition *= 1.0 + edgeDashBonus * ((20 - current.LastObject.DistanceToHyperDash) / 20)
                                                        * Math.Pow((Math.Min(current.StrainTime * catcherSpeedMultiplier, 265) / 265), 1.5); // Edge Dashes are easier at lower ms values
            }

            // There is an edge case where horizontal back and forth sliders create "buzz" patterns which are repeated "movements" with a distance lower than
            // the platter's width but high enough to be considered a movement due to the absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH offsets
            // We are detecting this exact scenario. The first back and forth is counted but all subsequent ones are nullified.
            // To achieve that, we need to store the exact distances (distance ignoring absolute_player_positioning_error and NORMALIZED_HALF_CATCHER_WIDTH)
            if (current.Index >= 2 && Math.Abs(current.ExactDistanceMoved) <= CatchDifficultyHitObject.NORMALIZED_HALF_CATCHER_WIDTH * 2
                                   && current.ExactDistanceMoved == -last.ExactDistanceMoved && last.ExactDistanceMoved == -lastLast.ExactDistanceMoved
                                   && current.StrainTime == last.StrainTime && last.StrainTime == lastLast.StrainTime)
                distanceAddition = 0;

            return distanceAddition / weightedStrainTime;
        }
    }
}
