// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AimEvaluator
    {
        private const double wide_angle_multiplier = 1.25;
        private const double slider_multiplier = 1.35;
        private const double velocity_change_multiplier = 0.75;
        private const double wiggle_multiplier = 2.0;

        /// <summary>
        /// Evaluates the difficulty of aiming the current object, based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>angle difficulty,</description></item>
        /// <item><description>sharp velocity increases,</description></item>
        /// <item><description>and slider difficulty.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance, bool withCheesability)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuLastLastObj = (OsuDifficultyHitObject)current.Previous(1);
            var osuLast2Obj = (OsuDifficultyHitObject)current.Previous(2);
            var osuLast3Obj = (OsuDifficultyHitObject)current.Previous(3);

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            double currStrainTime = osuCurrObj.AdjustedDeltaTime;
            double lastStrainTime = osuLastObj.AdjustedDeltaTime;

            double currMinimumJumpTime = osuCurrObj.MinimumJumpTime;
            double lastMinimumJumpTime = osuLastObj.MinimumJumpTime;

            if (withCheesability)
            {
                currStrainTime += osuCurrObj.ExtraDeltaTime;
                currMinimumJumpTime += osuCurrObj.ExtraDeltaTime;
                lastMinimumJumpTime += osuLastObj.ExtraDeltaTime;
                lastStrainTime += osuLastObj.ExtraDeltaTime;
            }

            // Calculate the velocity to the current hitobject, which starts with a base distance / time assuming the last object is a hitcircle.
            double currVelocity = osuCurrObj.LazyJumpDistance / currStrainTime;

            // But if the last object is a slider, then we extend the travel velocity through the slider into the current object.
            if (osuLastObj.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = osuLastObj.TravelDistance / osuLastObj.TravelTime; // calculate the slider velocity from slider head to slider end.
                double movementVelocity = osuCurrObj.MinimumJumpDistance / currMinimumJumpTime; // calculate the movement velocity from slider end to current object

                currVelocity = Math.Max(currVelocity, movementVelocity + travelVelocity); // take the larger total combined velocity.
            }

            // As above, do the same for the previous hitobject.
            double prevVelocity = osuLastObj.LazyJumpDistance / lastStrainTime;

            if (osuLastLastObj.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = osuLastLastObj.TravelDistance / osuLastLastObj.TravelTime;
                double movementVelocity = osuLastObj.MinimumJumpDistance / lastMinimumJumpTime;

                prevVelocity = Math.Max(prevVelocity, movementVelocity + travelVelocity);
            }

            double wideAngleBonus = 0;
            double sliderBonus = 0;
            double velocityChangeBonus = 0;
            double wiggleBonus = 0;
            double angleRepetitionNerf = 1;

            double aimStrain = currVelocity; // Start strain with regular velocity.

            if (osuCurrObj.Angle != null && osuLastObj.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuLastObj.Angle.Value;

                double baseFactor = 1 - 0.15 * DifficultyCalculationUtils.Smoothstep(lastAngle, double.DegreesToRadians(90), double.DegreesToRadians(40)) * angleDifference(currAngle, lastAngle);

                angleRepetitionNerf = Math.Pow(baseFactor + (1 - baseFactor) * angleVectorRepetition(osuCurrObj), 2);

                // Rewarding angles, take the smaller velocity as base.
                double angleBonus = Math.Min(currVelocity, prevVelocity);

                wideAngleBonus = calcWideAngleBonus(currAngle);

                double wideBaseFactor = 1 - 0.3 * DifficultyCalculationUtils.Smoothstep(lastAngle, double.DegreesToRadians(140), double.DegreesToRadians(90)) * angleDifference(currAngle, lastAngle);

                // Penalize angle repetition.
                wideAngleBonus *= angleBonus * Math.Pow(wideBaseFactor + (1 - wideBaseFactor) * angleVectorRepetition(osuCurrObj), 2);

                // Apply wiggle bonus for jumps that are [radius, 3*diameter] in distance, with < 110 angle
                // https://www.desmos.com/calculator/dp0v0nvowc
                wiggleBonus = angleBonus
                              * DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance, radius, diameter)
                              * Math.Pow(DifficultyCalculationUtils.ReverseLerp(osuCurrObj.LazyJumpDistance, diameter * 3, diameter), 1.8)
                              * DifficultyCalculationUtils.Smootherstep(currAngle, double.DegreesToRadians(110), double.DegreesToRadians(60))
                              * DifficultyCalculationUtils.Smootherstep(osuLastObj.LazyJumpDistance, radius, diameter)
                              * Math.Pow(DifficultyCalculationUtils.ReverseLerp(osuLastObj.LazyJumpDistance, diameter * 3, diameter), 1.8)
                              * DifficultyCalculationUtils.Smootherstep(lastAngle, double.DegreesToRadians(110), double.DegreesToRadians(60));

                if (osuLast2Obj != null)
                {
                    // If objects just go back and forth through a middle point - don't give as much wide bonus
                    // Use Previous(2) and Previous(0) because angles calculation is done prevprev-prev-curr, so any object's angle's center point is always the previous object
                    var lastBaseObject = (OsuHitObject)osuLastObj.BaseObject;
                    var last2BaseObject = (OsuHitObject)osuLast2Obj.BaseObject;

                    float distance = (last2BaseObject.StackedPosition - lastBaseObject.StackedPosition).Length;

                    if (distance < 1)
                    {
                        wideAngleBonus *= 1 - 0.35 * (1 - distance);
                    }
                }
            }

            if (Math.Max(prevVelocity, currVelocity) != 0)
            {
                // We want to use the average velocity over the whole object when awarding differences, not the individual jump and slider path velocities.
                prevVelocity = (osuLastObj.LazyJumpDistance + osuLastLastObj.TravelDistance) / lastStrainTime;
                currVelocity = (osuCurrObj.LazyJumpDistance + osuLastObj.TravelDistance) / currStrainTime;

                // Scale with ratio of difference compared to 0.5 * max dist.
                double distRatio = DifficultyCalculationUtils.Smoothstep(Math.Abs(prevVelocity - currVelocity) / Math.Max(prevVelocity, currVelocity), 0, 1);

                // Reward for % distance up to 125 / strainTime for overlaps where velocity is still changing.
                double overlapVelocityBuff = Math.Min(diameter * 1.25 / Math.Min(currStrainTime, lastStrainTime), Math.Abs(prevVelocity - currVelocity));

                velocityChangeBonus = overlapVelocityBuff * distRatio;

                // Penalize for rhythm changes.
                velocityChangeBonus *= Math.Pow(Math.Min(currStrainTime, lastStrainTime) / Math.Max(currStrainTime, lastStrainTime), 2);
            }

            if (osuLastObj.BaseObject is Slider)
            {
                // Reward sliders based on velocity.
                sliderBonus = osuLastObj.TravelDistance / osuLastObj.TravelTime;
            }

            if (osuLast3Obj != null)
            {
                aimStrain *= calculateJumpOverlapCorrection((OsuHitObject)osuCurrObj.BaseObject,
                    (OsuHitObject)osuLastObj.BaseObject,
                    (OsuHitObject)osuLastLastObj.BaseObject,
                    (OsuHitObject)osuLast3Obj.BaseObject);
            }

            aimStrain *= calculateSmallJumpNerf(osuCurrObj);
            aimStrain *= calculateBigJumpBuff(osuCurrObj);

            aimStrain *= angleRepetitionNerf;

            aimStrain += wideAngleBonus * wide_angle_multiplier;

            aimStrain += wiggleBonus * wiggle_multiplier;
            aimStrain += velocityChangeBonus * velocity_change_multiplier;

            // Apply high circle size bonus
            aimStrain *= osuCurrObj.SmallCircleBonus;

            // Add in additional slider velocity bonus.
            if (withSliderTravelDistance)
                aimStrain += sliderBonus * slider_multiplier;

            return aimStrain;
        }

        private static double angleDifference(double curAngle, double lastAngle)
        {
            return Math.Cos(2 * Math.Min(Math.PI / 4, Math.Abs(curAngle - lastAngle)));
        }

        private static double angleVectorRepetition(OsuDifficultyHitObject current)
        {
            const double note_limit = 6;

            double constantAngleCount = 0;
            int index = 0;
            double notesProcessed = 0;

            while (notesProcessed < note_limit)
            {
                var loopObj = (OsuDifficultyHitObject)current.Previous(index);

                if (loopObj.IsNull())
                    break;

                if (Math.Abs(current.DeltaTime - loopObj.DeltaTime) > 25)
                    break;

                if (loopObj.NormalisedVectorAngle.IsNotNull() && current.NormalisedVectorAngle.IsNotNull())
                {
                    double angleDifference = Math.Abs(current.NormalisedVectorAngle.Value - loopObj.NormalisedVectorAngle.Value);
                    constantAngleCount += Math.Cos(8 * Math.Min(Math.PI / 16, angleDifference));
                }

                notesProcessed++;
                index++;
            }

            return Math.Pow(Math.Min(0.5 / constantAngleCount, 1), 2);
        }

        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(40), double.DegreesToRadians(140));

        /// <summary>
        /// We apply nerf to jumps within ~1-3.5 distance (with peak at 2.2) depending on BPM.
        /// Graphs: https://www.desmos.com/calculator/lbwtkv1qom
        /// </summary>
        private static double calculateSmallJumpNerf(OsuDifficultyHitObject curr)
        {
            var effectiveBPM = 30 / (curr.AdjustedDeltaTime / 1000.0);

            // this applies nerf up to 300 bpm and starts deminishing it at ~200 bpm
            var bpmCutoff = DifficultyCalculationUtils.Logistic(-((255 - effectiveBPM) / 10.0));

            var distanceNerf = Math.Exp(-Math.Pow(((curr.LazyJumpDistance / OsuDifficultyHitObject.NORMALISED_DIAMETER) - 2.2) / 0.7, 2.0));

            return 1.0 - distanceNerf * bpmCutoff * 0.25;
        }

        /// <summary>
        /// We apply buff to jumps with distance starting from ~4 on low BPMs.
        /// Graphs: https://www.desmos.com/calculator/fmewz0foql
        /// </summary>
        private static double calculateBigJumpBuff(OsuDifficultyHitObject curr)
        {
            var effectiveBPM = 30 / (curr.AdjustedDeltaTime / 1000.0);

            // this applies buff up until ~250 bpm
            var bpmCutoff = DifficultyCalculationUtils.Logistic(-((210 - effectiveBPM) / 8.0));

            var distanceBuff = DifficultyCalculationUtils.Logistic(-(((curr.LazyJumpDistance / OsuDifficultyHitObject.NORMALISED_DIAMETER) - 6.0) / 0.5));

            return 1.0 + distanceBuff * bpmCutoff * 0.5;
        }

        /// <summary>
        /// We apply a nerf to big jumps where second-last or fourth-last and current objects are close.
        /// This mainly targets repeating jumps such as
        /// 1  3
        ///  \/
        ///  /\
        /// 4  2
        /// </summary>
        private static double calculateJumpOverlapCorrection(OsuHitObject curr, OsuHitObject last, OsuHitObject lastLast, OsuHitObject last3)
        {
            float scalingFactor = OsuDifficultyHitObject.NORMALISED_RADIUS / (float)curr.Radius;

            var lastLastToCurrDist = (curr.StackedPosition * scalingFactor - lastLast.StackedPosition * scalingFactor).Length / OsuDifficultyHitObject.NORMALISED_DIAMETER;
            var last3ToCurrDist = (curr.StackedPosition * scalingFactor - last3.StackedPosition * scalingFactor).Length / OsuDifficultyHitObject.NORMALISED_DIAMETER;
            var lastToCurrDist = (curr.StackedPosition * scalingFactor - last.StackedPosition * scalingFactor).Length / OsuDifficultyHitObject.NORMALISED_DIAMETER;

            var secondLastToCurrentNerf = Math.Max(0.15 - 0.1 * lastLastToCurrDist, 0.0);
            var fourthLastToCurrentNerf = Math.Max(0.1125 - 0.075 * last3ToCurrDist, 0.0);

            var distanceCutoff = DifficultyCalculationUtils.Logistic(-((lastToCurrDist - 3.3) / 0.25));

            return 1.0 - (secondLastToCurrentNerf + fourthLastToCurrentNerf) * distanceCutoff;
        }
    }
}
