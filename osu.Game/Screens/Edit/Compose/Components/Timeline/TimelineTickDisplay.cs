﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Caching;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Visualisations;
using osuTK;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    public partial class TimelineTickDisplay : TimelinePart<PointVisualisation>
    {
        public const float TICK_WIDTH = 3;

        // With current implementation every tick in the sub-tree should be visible, no need to check whether they are masked away.
        public override bool UpdateSubTreeMasking() => false;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> working { get; set; } = null!;

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public TimelineTickDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        private readonly BindableBool showTimingChanges = new BindableBool(true);

        private readonly Cached tickCache = new Cached();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager configManager)
        {
            beatDivisor.BindValueChanged(_ => invalidateTicks());

            beatmap.ControlPointInfo.ControlPointsChanged += invalidateTicks;

            configManager.BindWith(OsuSetting.EditorTimelineShowTimingChanges, showTimingChanges);
            showTimingChanges.BindValueChanged(_ => invalidateTicks());
        }

        private void invalidateTicks()
        {
            tickCache.Invalidate();
        }

        /// <summary>
        /// The visible time/position range of the timeline.
        /// </summary>
        private (float min, float max) visibleRange = (float.MinValue, float.MaxValue);

        /// <summary>
        /// The next time/position value to the left of the display when tick regeneration needs to be run.
        /// </summary>
        private float? nextMinTick;

        /// <summary>
        /// The next time/position value to the right of the display when tick regeneration needs to be run.
        /// </summary>
        private float? nextMaxTick;

        [Resolved]
        private Timeline? timeline { get; set; }

        protected override void Update()
        {
            base.Update();

            if (timeline == null || DrawWidth <= 0) return;

            (float, float) newRange = (
                (ToLocalSpace(timeline.ScreenSpaceDrawQuad.TopLeft).X - PointVisualisation.MAX_WIDTH * 2) / DrawWidth * Content.RelativeChildSize.X,
                (ToLocalSpace(timeline.ScreenSpaceDrawQuad.TopRight).X + PointVisualisation.MAX_WIDTH * 2) / DrawWidth * Content.RelativeChildSize.X);

            if (visibleRange != newRange)
            {
                visibleRange = newRange;

                // actual regeneration only needs to occur if we've passed one of the known next min/max tick boundaries.
                if (nextMinTick == null || nextMaxTick == null || (visibleRange.min < nextMinTick || visibleRange.max > nextMaxTick))
                    tickCache.Invalidate();
            }

            if (!tickCache.IsValid)
                createTicks();
        }

        private void createTicks()
        {
            int drawableIndex = 0;

            nextMinTick = null;
            nextMaxTick = null;

            for (int i = 0; i < beatmap.ControlPointInfo.TimingPoints.Count; i++)
            {
                var point = beatmap.ControlPointInfo.TimingPoints[i];
                double until = i + 1 < beatmap.ControlPointInfo.TimingPoints.Count ? beatmap.ControlPointInfo.TimingPoints[i + 1].Time : working.Value.Track.Length;

                int beat = 0;

                for (double t = point.Time; t < until; t += point.BeatLength / beatDivisor.Value)
                {
                    float xPos = (float)t;

                    if (t < visibleRange.min)
                        nextMinTick = xPos;
                    else if (t > visibleRange.max)
                        nextMaxTick ??= xPos;
                    else
                    {
                        // if this is the first beat in the beatmap, there is no next min tick
                        if (beat == 0 && i == 0)
                            nextMinTick = float.MinValue;

                        int indexInBar = beat % (point.TimeSignature.Numerator * beatDivisor.Value);

                        int divisor = BindableBeatDivisor.GetDivisorForBeatIndex(beat, beatDivisor.Value);
                        var colour = BindableBeatDivisor.GetColourFor(divisor, colours);

                        // even though "bar lines" take up the full vertical space, we render them in two pieces because it allows for less anchor/origin churn.

                        var size = indexInBar == 0
                            ? new Vector2(1.3f, 1)
                            : BindableBeatDivisor.GetSize(divisor);

                        var line = getNextUsableLine();
                        line.X = xPos;

                        line.Width = TICK_WIDTH * size.X;
                        line.Height = size.Y;
                        line.Colour = colour;
                    }

                    beat++;
                }
            }

            int usedDrawables = drawableIndex;

            // save a few drawables beyond the currently used for edge cases.
            while (drawableIndex < Math.Min(usedDrawables + 16, Count))
                Children[drawableIndex++].Alpha = 0;

            // expire any excess
            while (drawableIndex < Count)
                Children[drawableIndex++].Expire();

            tickCache.Validate();

            Drawable getNextUsableLine()
            {
                PointVisualisation point;

                if (drawableIndex >= Count)
                {
                    Add(point = new PointVisualisation(0)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                    });
                }
                else
                    point = Children[drawableIndex];

                drawableIndex++;
                point.Alpha = 1;

                return point;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (beatmap.IsNotNull())
                beatmap.ControlPointInfo.ControlPointsChanged -= invalidateTicks;
        }
    }
}
