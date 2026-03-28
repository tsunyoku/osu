// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Difficulty
{
    public interface IDifficultyCalculator
    {
        int Version { get; }

        /// <summary>
        /// Calculates the difficulty of the beatmap using a specific mod combination.
        /// </summary>
        /// <param name="mods">The mods that should be applied to the beatmap.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A structure describing the difficulty of the beatmap.</returns>
        DifficultyAttributes Calculate(IEnumerable<Mod> mods, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the difficulty of the beatmap using a specific mod combination and returns a set of <see cref="TimedDifficultyAttributes"/> representing the difficulty at every relevant time value in the beatmap.
        /// </summary>
        /// <param name="mods">The mods that should be applied to the beatmap.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The set of <see cref="TimedDifficultyAttributes"/>.</returns>
        List<TimedDifficultyAttributes> CalculateTimed(IEnumerable<Mod> mods, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the difficulty of the beatmap using all mod combinations applicable to the beatmap.
        /// </summary>
        /// <remarks>
        /// This can only be used to compute difficulties for legacy mod combinations.
        /// </remarks>
        /// <returns>A collection of structures describing the difficulty of the beatmap for each mod combination.</returns>
        IEnumerable<DifficultyAttributes> CalculateAllLegacyCombinations(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates all <see cref="Mod"/> combinations which adjust the <see cref="Beatmaps.Beatmap"/> difficulty.
        /// </summary>
        Mod[] CreateDifficultyAdjustmentModCombinations();
    }
}
