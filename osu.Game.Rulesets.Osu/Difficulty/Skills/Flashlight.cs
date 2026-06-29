// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    /// <summary>
    /// Represents the skill required to memorise and hit every object in a map with the Flashlight mod enabled.
    /// </summary>
    public class Flashlight : StrainSkill
    {
        private readonly int totalObjects;

        private readonly bool hasFlashlightMod;
        private readonly bool hasTouchDeviceMod;
        private readonly bool hasRelaxMod;
        private readonly bool hasAutopilotMod;

        private readonly OsuModHidden? hiddenMod;
        private readonly OsuModMagnetised? magnetisedMod;
        private readonly OsuModDeflate? deflateMod;

        public Flashlight(Mod[] mods, int totalObjects)
        {
            this.totalObjects = totalObjects;

            hasFlashlightMod = mods.Any(m => m is OsuModFlashlight);
            hasTouchDeviceMod = mods.Any(m => m is OsuModTouchDevice);
            hasRelaxMod = mods.Any(m => m is OsuModRelax);
            hasAutopilotMod = mods.Any(m => m is OsuModAutopilot);

            hiddenMod = mods.OfType<OsuModHidden>().SingleOrDefault();
            magnetisedMod = mods.OfType<OsuModMagnetised>().SingleOrDefault();
            deflateMod = mods.OfType<OsuModDeflate>().SingleOrDefault();
        }

        private double currentStrain;

        private double strainDecay(double ms) => DiffUtils.Pow(0.15, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            const double skill_multiplier = 0.058;

            if (!hasFlashlightMod)
                return 0;

            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += calculateAdjustedDifficulty(current) * skill_multiplier;

            return currentStrain;
        }

        private double calculateAdjustedDifficulty(DifficultyHitObject current)
        {
            double difficulty = FlashlightEvaluator.EvaluateDifficultyOf(current, hiddenMod);

            if (hasTouchDeviceMod)
                difficulty = DiffUtils.Pow(difficulty, 0.9);

            if (magnetisedMod != null)
            {
                float magnetisedStrength = magnetisedMod.AttractionStrength.Value;
                difficulty *= 1.0 - magnetisedStrength;
            }

            if (deflateMod != null)
            {
                float deflateInitialScale = deflateMod.StartScale.Value;
                difficulty *= Math.Clamp(DiffUtils.ReverseLerp(deflateInitialScale, 11, 1), 0.1, 1);
            }

            if (hasRelaxMod)
                difficulty *= 0.7;

            if (hasAutopilotMod)
                difficulty *= 0.4;

            difficulty *= 0.985 + DiffUtils.Pow(Math.Max(0, ((OsuDifficultyHitObject)current).OverallDifficulty), 2) / 4000;

            return difficulty;
        }

        public override double DifficultyValue()
        {
            double sum = GetCurrentStrainPeaks().Sum();

            // Account for shorter maps having a higher ratio of 0 combo/100 combo flashlight radius.
            sum *= 0.7 + 0.1 * Math.Min(1.0, totalObjects / 200.0) +
                   (totalObjects > 200 ? 0.2 * Math.Min(1.0, (totalObjects - 200) / 200.0) : 0.0);

            return sum;
        }

        public static double DifficultyToPerformance(double difficulty) => 25 * DiffUtils.Pow(difficulty, 2);
    }
}
