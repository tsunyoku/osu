// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.0655;
        private const double performance_base_multiplier = 1.4; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things.

        public override int Version => 20241007;

        public OsuDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods };

            double aimRating = Math.Sqrt(skills[0].DifficultyValue()) * difficulty_multiplier;
            double aimRatingNoSliders = Math.Sqrt(skills[1].DifficultyValue()) * difficulty_multiplier;
            double speedRating = Math.Sqrt(skills[2].DifficultyValue()) * difficulty_multiplier;
            double speedNotes = ((Speed)skills[2]).RelevantNoteCount();
            double difficultSliders = ((Aim)skills[0]).GetDifficultSliders();
            double aimRelevantObjectCount = ((OsuStrainSkill)skills[0]).CountRelevantObjects();
            double aimNoSlidersRelevantObjectCount = ((OsuStrainSkill)skills[1]).CountRelevantObjects();
            double speedRelevantObjectCount = ((OsuStrainSkill)skills[2]).CountRelevantObjects();
            double flashlightRating = 0.0;

            if (mods.Any(h => h is OsuModFlashlight))
                flashlightRating = Math.Sqrt(skills[3].DifficultyValue()) * difficulty_multiplier;

            double aimDifficultyStrainCount = ((OsuStrainSkill)skills[0]).CountTopWeightedStrains();
            double speedDifficultyStrainCount = ((OsuStrainSkill)skills[2]).CountTopWeightedStrains();

            if (mods.Any(m => m is OsuModTouchDevice))
            {
                aimRating = Math.Pow(aimRating, 0.8);
                aimRatingNoSliders = Math.Pow(aimRatingNoSliders, 0.8);
                flashlightRating = Math.Pow(flashlightRating, 0.8);
            }

            if (mods.Any(h => h is OsuModRelax))
            {
                aimRating *= 0.9;
                aimRatingNoSliders *= 0.9;
                speedRating = 0.0;
                flashlightRating *= 0.7;
            }

            int totalHits = beatmap.HitObjects.Count;

            double preempt = IBeatmapDifficultyInfo.DifficultyRange(beatmap.Difficulty.ApproachRate, 1800, 1200, 450) / clockRate;
            double approachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5;

            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            double hitWindowGreat = hitWindows.WindowFor(HitResult.Great) / clockRate;
            double hitWindowOk = hitWindows.WindowFor(HitResult.Ok) / clockRate;
            double hitWindowMeh = hitWindows.WindowFor(HitResult.Meh) / clockRate;

            double overallDifficulty = (80 - hitWindowGreat) / 6;

            aimRating = transformAimDifficulty(aimRating, aimRelevantObjectCount, mods, approachRate, overallDifficulty);
            aimRatingNoSliders = transformAimDifficulty(aimRatingNoSliders, aimNoSlidersRelevantObjectCount, mods, approachRate, overallDifficulty);
            speedRating = transformSpeedDifficulty(speedRating, speedRelevantObjectCount, mods, approachRate);
            flashlightRating = transformFlashlightDifficulty(flashlightRating, totalHits, overallDifficulty);

            double sliderFactor = aimRating > 0 ? aimRatingNoSliders / aimRating : 1;

            double baseAimPerformance = OsuStrainSkill.DifficultyToPerformance(aimRating);
            double baseSpeedPerformance = OsuStrainSkill.DifficultyToPerformance(speedRating);
            double baseFlashlightPerformance = 0.0;

            if (mods.Any(h => h is OsuModFlashlight))
                baseFlashlightPerformance = Flashlight.DifficultyToPerformance(flashlightRating);

            double basePerformance =
                Math.Pow(
                    Math.Pow(baseAimPerformance, 1.1) +
                    Math.Pow(baseSpeedPerformance, 1.1) +
                    Math.Pow(baseFlashlightPerformance, 1.1), 1.0 / 1.1
                );

            int hitCirclesCount = beatmap.HitObjects.Count(h => h is HitCircle);
            int sliderCount = beatmap.HitObjects.Count(h => h is Slider);
            int spinnerCount = beatmap.HitObjects.Count(h => h is Spinner);

            double multiplier = CalculateMultiplier(mods, spinnerCount, totalHits);

            double starRating = basePerformance > 0.00001
                ? Math.Cbrt(multiplier) * 0.025 * (Math.Cbrt(100000 / Math.Pow(2, 1 / 1.1) * basePerformance) + 4)
                : 0;

            double drainRate = beatmap.Difficulty.DrainRate;

            OsuDifficultyAttributes attributes = new OsuDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                AimDifficulty = aimRating,
                AimDifficultSliderCount = difficultSliders,
                SpeedDifficulty = speedRating,
                SpeedNoteCount = speedNotes,
                FlashlightDifficulty = flashlightRating,
                SliderFactor = sliderFactor,
                AimDifficultStrainCount = aimDifficultyStrainCount,
                SpeedDifficultStrainCount = speedDifficultyStrainCount,
                ApproachRate = approachRate,
                OverallDifficulty = overallDifficulty,
                GreatHitWindow = hitWindowGreat,
                OkHitWindow = hitWindowOk,
                MehHitWindow = hitWindowMeh,
                DrainRate = drainRate,
                MaxCombo = beatmap.GetMaxCombo(),
                HitCircleCount = hitCirclesCount,
                SliderCount = sliderCount,
                SpinnerCount = spinnerCount,
            };

            return attributes;
        }

        public static double CalculateMultiplier(Mod[] mods, int spinnerCount, int totalHits)
        {
            double multiplier = performance_base_multiplier;

            if (mods.Any(m => m is OsuModSpunOut) && totalHits > 0)
                multiplier *= 1.0 - Math.Pow((double)spinnerCount / totalHits, 0.85);

            return multiplier;
        }

        private double transformAimDifficulty(double difficulty, double aimRelevantObjectCount, Mod[] mods, double approachRate, double overallDifficulty)
        {
            double difficultyMultiplier = 1.0;

            double lengthBonus = 1.0 + Math.Min(1.8, aimRelevantObjectCount / 300.0) +
                                 (aimRelevantObjectCount > 540.0 ? Math.Log10(aimRelevantObjectCount / 540.0) : 0);

            difficultyMultiplier *= lengthBonus;

            double approachRateFactor = 0.0;
            if (approachRate > 10.5)
                approachRateFactor = 0.25 * (approachRate - 10.5);
            else if (approachRate < 8.0)
                approachRateFactor = 0.05 * (8.0 - approachRate);

            if (mods.Any(h => h is OsuModRelax))
                approachRateFactor = 0.0;

            difficultyMultiplier *= 1.0 + approachRateFactor * lengthBonus;

            if (mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                difficultyMultiplier *= 0.97 + 0.06 * (12.0 - approachRate);
            }

            // It is important to consider accuracy difficulty when scaling with accuracy.
            difficultyMultiplier *= 0.98 + Math.Pow(overallDifficulty, 2) / 2500;

            return difficulty * Math.Cbrt(difficultyMultiplier);
        }

        private double transformSpeedDifficulty(double difficulty, double speedRelevantObjectCount, Mod[] mods, double approachRate)
        {
            double difficultyMultiplier = 1.0;

            double lengthBonus = 1.0 + 0.45 * Math.Min(0.8, speedRelevantObjectCount / 500.0) +
                                 (speedRelevantObjectCount > 400 ? 0.2 * Math.Log10(speedRelevantObjectCount / 400.0) : 0.0);

            difficultyMultiplier *= lengthBonus;

            double approachRateFactor = 0.0;
            if (approachRate > 10.5)
                approachRateFactor = 0.25 * (approachRate - 10.5);

            difficultyMultiplier *= 1.0 + approachRateFactor;

            if (mods.Any(m => m is OsuModBlinds))
            {
                // Increasing the speed value by object count for Blinds isn't ideal, so the minimum buff is given.
                difficultyMultiplier *= 1.12;
            }
            else if (mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                difficultyMultiplier *= 0.97 + 0.06 * (12.0 - approachRate);
            }

            return difficulty * Math.Cbrt(difficultyMultiplier);
        }

        private double transformFlashlightDifficulty(double difficulty, int totalHits, double overallDifficulty)
        {
            double difficultyMultiplier = 1.0;

            // It is important to consider accuracy difficulty when scaling with accuracy.
            difficultyMultiplier *= 0.98 + Math.Pow(overallDifficulty, 2) / 2500;

            // Account for shorter maps having a higher ratio of 0 combo/100 combo flashlight radius.
            difficultyMultiplier *= 0.7 + 0.1 * Math.Min(1.0, totalHits / 200.0) +
                                    (totalHits > 200 ? 0.2 * Math.Min(1.0, (totalHits - 200) / 200.0) : 0.0);

            return difficulty * Math.Cbrt(difficultyMultiplier);
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();

            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                var lastLast = i > 1 ? beatmap.HitObjects[i - 2] : null;
                objects.Add(new OsuDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], lastLast, clockRate, objects, objects.Count));
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            var skills = new List<Skill>
            {
                new Aim(mods, true),
                new Aim(mods, false),
                new Speed(mods)
            };

            if (mods.Any(h => h is OsuModFlashlight))
                skills.Add(new Flashlight(mods));

            return skills.ToArray();
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModTouchDevice(),
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
            new OsuModFlashlight(),
            new MultiMod(new OsuModFlashlight(), new OsuModHidden())
        };
    }
}
