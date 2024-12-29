// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.EmptyScrolling
{
    public class EmptyScrollingDifficultyCalculator : DifficultyCalculator
    {
        public EmptyScrollingDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap, IEnumerable<Mod> mods)
            : base(ruleset, beatmap, mods)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes()
        {
            return new DifficultyAttributes(Mods, 0);
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects() => Enumerable.Empty<DifficultyHitObject>();

        public override int Version => 0;
        protected override Skill[] Skills => [];
    }
}
