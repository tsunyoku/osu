// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Taiko.Difficulty.Evaluators;
using osu.Game.Rulesets.Taiko.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Taiko.Difficulty.Skills
{
    /// <summary>
    /// Calculates the stamina coefficient of taiko difficulty.
    /// </summary>
    public class Stamina : StrainSkill
    {
        private double skillMultiplier => 1.1;
        private double strainDecayBase => 0.4;

        public readonly bool SingleColourStamina;
        private readonly bool isConvert;

        private double currentStrain;
        private double lastStrain;

        /// <summary>
        /// Creates a <see cref="Stamina"/> skill.
        /// </summary>
        /// <param name="mods">Mods for use in skill calculations.</param>
        /// <param name="singleColourStamina">Reads when Stamina is from a single coloured pattern.</param>
        /// <param name="isConvert">Determines if the currently evaluated beatmap is converted.</param>
        public Stamina(Mod[] mods, bool singleColourStamina, bool isConvert)
            : base(mods)
        {
            SingleColourStamina = singleColourStamina;
            this.isConvert = isConvert;
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override IEnumerable<ObjectStrain> StrainValuesAt(DifficultyHitObject current)
        {
            lastStrain = currentStrain;

            currentStrain *= strainDecay(current.DeltaTime);
            double staminaDifficulty = StaminaEvaluator.EvaluateDifficultyOf(current) * skillMultiplier;

            // Safely prevents previous strains from shifting as new notes are added.
            var currentObject = current as TaikoDifficultyHitObject;
            int index = currentObject?.ColourData.MonoStreak?.HitObjects.IndexOf(currentObject) ?? 0;

            double monoLengthBonus = isConvert ? 1.0 : 1.0 + 0.5 * DifficultyCalculationUtils.ReverseLerp(index, 5, 20);

            // Mono-streak bonus is only applied to colour-based stamina to reward longer sequences of same-colour hits within patterns.
            if (!SingleColourStamina)
                staminaDifficulty *= monoLengthBonus;

            currentStrain += staminaDifficulty;

            // For converted maps, difficulty often comes entirely from long mono streams with no colour variation.
            // To avoid over-rewarding these maps based purely on stamina strain, we dampen the strain value once the index exceeds 10.
            double strainValue =
                SingleColourStamina
                    ? DifficultyCalculationUtils.Logistic(-(index - 10) / 2.0, currentStrain)
                    : currentStrain;

            yield return new ObjectStrain
            {
                Time = current.StartTime,
                PreviousTime = current.Previous(0)?.StartTime ?? 0,
                Value = strainValue,
            };
        }

        protected override double CalculateInitialStrain(double deltaTime) =>
            SingleColourStamina
                ? 0
                : lastStrain * strainDecay(deltaTime);
    }
}
