// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    public class Movement
    {
        public Vector2 Start { get; set; }
        public double StartTime { get; set; }
        public Vector2 End { get; set; }
        public double EndTime { get; set; }
        public double StartRadius { get; set; }
        public double EndRadius { get; set; }

        public double Time => Math.Max(EndTime - StartTime, OsuDifficultyHitObject.MIN_DELTA_TIME);
        public double Distance => (End * (OsuDifficultyHitObject.NORMALISED_RADIUS / (float)Math.Max(StartRadius, EndRadius)) - Start * (OsuDifficultyHitObject.NORMALISED_RADIUS / (float)Math.Max(StartRadius, EndRadius))).Length;

        public override string ToString()
        {
            return $"{Start}->{End} ({Distance:N2}px, {Time:N2}ms)";
        }
    }
}
