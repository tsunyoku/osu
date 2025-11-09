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
        public float ScalingFactor { get; set; }
        public double Time => Math.Max(EndTime - StartTime, OsuDifficultyHitObject.MIN_DELTA_TIME);
        public double Distance => (End * ScalingFactor - Start * ScalingFactor).Length;
    }
}
