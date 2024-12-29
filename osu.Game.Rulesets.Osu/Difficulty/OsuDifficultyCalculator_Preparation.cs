// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
    public partial class OsuDifficultyCalculator : DifficultyCalculator
    {
        private const double performance_base_multiplier = 1.15; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things.

        public override int Version => 20241007;

        private readonly Aim aim;
        private readonly Aim aimNoSliders;
        private readonly Speed speed;
        private readonly Flashlight? flashlight;

        public OsuDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap, IEnumerable<Mod> mods)
            : base(ruleset, beatmap, mods)
        {
            aim = new Aim(Mods, withSliders: true);
            aimNoSliders = new Aim(Mods, withSliders: false);
            speed = new Speed(Mods);

            if (Mods.Any(m => m is OsuModFlashlight))
                flashlight = new Flashlight(Mods);
        }

        public static double CalculateMultiplier(Mod[] mods, int spinnerCount, int totalHits)
        {
            double multiplier = performance_base_multiplier;

            if (mods.Any(m => m is OsuModSpunOut) && totalHits > 0)
                multiplier *= 1.0 - Math.Pow((double)spinnerCount / totalHits, 0.85);

            return multiplier;
        }

        protected override DifficultyAttributes CreateDifficultyAttributes()
        {
            if (Beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = Mods };

            DifficultyInfo difficultyInfo = createDifficultyInfo();
            return calculateDifficultyAttributes(difficultyInfo);
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects()
        {
            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();

            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < Beatmap.HitObjects.Count; i++)
            {
                var lastLast = i > 1 ? Beatmap.HitObjects[i - 2] : null;
                objects.Add(new OsuDifficultyHitObject(Beatmap.HitObjects[i], Beatmap.HitObjects[i - 1], lastLast, ClockRate, objects, objects.Count));
            }

            return objects;
        }

        private DifficultyInfo createDifficultyInfo()
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(Beatmap.Difficulty.ApproachRate, 1800, 1200, 450) / ClockRate;
            double approachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5;

            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(Beatmap.Difficulty.OverallDifficulty);

            double hitWindowGreat = hitWindows.WindowFor(HitResult.Great) / ClockRate;

            double overallDifficulty = (80 - hitWindowGreat) / 6;

            int hitCircleCount = Beatmap.HitObjects.Count(h => h is HitCircle);
            int sliderCount = Beatmap.HitObjects.Count(h => h is Slider);
            int spinnerCount = Beatmap.HitObjects.Count(h => h is Spinner);

            double drainRate = Beatmap.Difficulty.DrainRate;

            return new DifficultyInfo
            {
                TotalHits = Beatmap.HitObjects.Count,
                ApproachRate = approachRate,
                HitWindowGreat = hitWindowGreat,
                OverallDifficulty = overallDifficulty,
                HitCircleCount = hitCircleCount,
                SliderCount = sliderCount,
                SpinnerCount = spinnerCount,
                DrainRate = drainRate,
                MaxCombo = Beatmap.GetMaxCombo(),
            };
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

        protected override Skill[] Skills
        {
            get
            {
                List<Skill> skills = [aim, aimNoSliders, speed];

                if (flashlight is not null)
                    skills.Add(flashlight);

                return skills.ToArray();
            }
        }

        private class DifficultyInfo
        {
            public required int TotalHits { get; init; }

            public required double ApproachRate { get; init; }

            public required double HitWindowGreat { get; init; }

            public required double OverallDifficulty { get; init; }

            public required int HitCircleCount { get; init; }

            public required int SliderCount { get; init; }

            public required int SpinnerCount { get; init; }

            public required double DrainRate { get; init; }

            public required int MaxCombo { get; init; }
        }
    }
}
