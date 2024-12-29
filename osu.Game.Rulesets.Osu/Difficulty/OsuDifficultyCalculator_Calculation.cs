// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public partial class OsuDifficultyCalculator
    {
        private const double difficulty_multiplier = 0.0675;

        private OsuDifficultyAttributes calculateDifficultyAttributes(DifficultyInfo difficultyInfo)
        {
            double speedNotes = speed.RelevantNoteCount();
            double aimDifficultyStrainCount = aim.CountTopWeightedStrains();
            double speedDifficultyStrainCount = speed.CountTopWeightedStrains();

            double aimRating = computeAimRating(aim.DifficultyValue(), difficultyInfo);
            double aimRatingNoSliders = computeAimRating(aimNoSliders.DifficultyValue(), difficultyInfo);

            double sliderFactor = aimRating > 0 ? aimRatingNoSliders / aimRating : 1;

            double speedRating = computeSpeedRating(speed.DifficultyValue(), difficultyInfo);

            double flashlightRating = 0.0;

            if (flashlight is not null)
                flashlightRating = computeFlashlightRating(flashlight.DifficultyValue(), difficultyInfo);

            double baseAimPerformance = OsuStrainSkill.DifficultyToPerformance(aimRating);
            double baseSpeedPerformance = OsuStrainSkill.DifficultyToPerformance(speedRating);
            double baseFlashlightPerformance = Flashlight.DifficultyToPerformance(flashlightRating);

            double basePerformance =
                Math.Pow(
                    Math.Pow(baseAimPerformance, 1.1) +
                    Math.Pow(baseSpeedPerformance, 1.1) +
                    Math.Pow(baseFlashlightPerformance, 1.1), 1.0 / 1.1
                );

            double multiplier = CalculateMultiplier(Mods, difficultyInfo.SpinnerCount, difficultyInfo.TotalHits);

            double starRating = basePerformance > 0.00001
                ? Math.Cbrt(multiplier) * 0.026 * (Math.Cbrt(100000 / Math.Pow(2, 1 / 1.1) * basePerformance) + 4)
                : 0;

            return new OsuDifficultyAttributes
            {
                StarRating = starRating,
                Mods = Mods,
                AimDifficulty = aimRating,
                SpeedDifficulty = speedRating,
                SpeedNoteCount = speedNotes,
                FlashlightDifficulty = flashlightRating,
                SliderFactor = sliderFactor,
                AimDifficultStrainCount = aimDifficultyStrainCount,
                SpeedDifficultStrainCount = speedDifficultyStrainCount,
                ApproachRate = difficultyInfo.ApproachRate,
                OverallDifficulty = difficultyInfo.OverallDifficulty,
                DrainRate = difficultyInfo.DrainRate,
                MaxCombo = difficultyInfo.MaxCombo,
                HitCircleCount = difficultyInfo.HitCircleCount,
                SliderCount = difficultyInfo.SliderCount,
                SpinnerCount = difficultyInfo.SpinnerCount,
            };
        }

        private double computeAimRating(double difficultyValue, DifficultyInfo difficultyInfo)
        {
            double aimRating = Math.Sqrt(difficultyValue) * difficulty_multiplier;

            if (Mods.Any(m => m is OsuModTouchDevice))
                aimRating *= Math.Pow(aimRating, 0.8);

            if (Mods.Any(h => h is OsuModRelax))
                aimRating *= 0.9;

            double ratingMultiplier = 1.0;

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, difficultyInfo.TotalHits / 2000.0) +
                                 (difficultyInfo.TotalHits > 2000 ? Math.Log10(difficultyInfo.TotalHits / 2000.0) * 0.5 : 0.0);

            ratingMultiplier *= lengthBonus;

            double approachRateFactor = 0.0;
            if (difficultyInfo.ApproachRate > 10.33)
                approachRateFactor = 0.3 * (difficultyInfo.ApproachRate - 10.33);
            else if (difficultyInfo.ApproachRate < 8.0)
                approachRateFactor = 0.05 * (8.0 - difficultyInfo.ApproachRate);

            if (Mods.Any(h => h is OsuModRelax))
                approachRateFactor = 0.0;

            ratingMultiplier *= 1.0 + approachRateFactor * lengthBonus; // Buff for longer maps with high AR.

            if (Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                ratingMultiplier *= 1.0 + 0.04 * (12.0 - difficultyInfo.ApproachRate);
            }

            // It is important to consider accuracy difficulty when scaling with accuracy.
            ratingMultiplier *= 0.98 + Math.Pow(difficultyInfo.OverallDifficulty, 2) / 2500;

            return aimRating * Math.Cbrt(ratingMultiplier);
        }

        private double computeSpeedRating(double difficultyValue, DifficultyInfo difficultyInfo)
        {
            if (Mods.Any(m => m is OsuModRelax))
                return 0;

            double speedRating = Math.Sqrt(difficultyValue) * difficulty_multiplier;

            double ratingMultiplier = 1.0;

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, difficultyInfo.TotalHits / 2000.0) +
                                 (difficultyInfo.TotalHits > 2000 ? Math.Log10(difficultyInfo.TotalHits / 2000.0) * 0.5 : 0.0);

            ratingMultiplier *= lengthBonus;

            double approachRateFactor = 0.0;
            if (difficultyInfo.ApproachRate > 10.33)
                approachRateFactor = 0.3 * (difficultyInfo.ApproachRate - 10.33);

            ratingMultiplier *= 1.0 + approachRateFactor * lengthBonus; // Buff for longer maps with high AR.

            if (Mods.Any(m => m is OsuModBlinds))
            {
                // Increasing the speed value by object count for Blinds isn't ideal, so the minimum buff is given.
                ratingMultiplier *= 1.12;
            }
            else if (Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                ratingMultiplier *= 1.0 + 0.04 * (12.0 - difficultyInfo.ApproachRate);
            }

            return speedRating * Math.Cbrt(ratingMultiplier);
        }

        private double computeFlashlightRating(double difficultyValue, DifficultyInfo difficultyInfo)
        {
            double flashlightRating = Math.Sqrt(difficultyValue) * difficulty_multiplier;

            if (Mods.Any(m => m is OsuModTouchDevice))
                flashlightRating *= Math.Pow(flashlightRating, 0.8);

            if (Mods.Any(h => h is OsuModRelax))
                flashlightRating *= 0.7;

            double ratingMultiplier = 1.0;

            // It is important to consider accuracy difficulty when scaling with accuracy.
            ratingMultiplier *= 0.98 + Math.Pow(difficultyInfo.OverallDifficulty, 2) / 2500;

            // Account for shorter maps having a higher ratio of 0 combo/100 combo flashlight radius.
            ratingMultiplier *= 0.7 + 0.1 * Math.Min(1.0, difficultyInfo.TotalHits / 200.0) +
                                (difficultyInfo.TotalHits > 200 ? 0.2 * Math.Min(1.0, (difficultyInfo.TotalHits - 200) / 200.0) : 0.0);

            return flashlightRating * Math.Cbrt(ratingMultiplier);
        }
    }
}
