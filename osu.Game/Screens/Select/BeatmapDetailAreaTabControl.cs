// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osuTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Framework.Graphics.Shapes;
using osu.Game.Screens.Select.Leaderboards;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapDetailAreaTabControl : Container
    {
        public const float HEIGHT = 24;

        public Bindable<BeatmapDetailAreaTabItem> Current
        {
            get => tabs.Current;
            set => tabs.Current = value;
        }

        public Bindable<bool> CurrentModsFilter
        {
            get => modsCheckbox.Current;
            set => modsCheckbox.Current = value;
        }

        public Bindable<BeatmapLeaderboardSort> CurrentSort
        {
            get => leSort;
            set => leSort = value;
        }

        public Action<BeatmapDetailAreaTabItem, bool, BeatmapLeaderboardSort> OnFilter; // passed the selected tab, if mods is checked and the sort

        public IReadOnlyList<BeatmapDetailAreaTabItem> TabItems
        {
            get => tabs.Items;
            set => tabs.Items = value;
        }

        private readonly OsuTabControlCheckbox modsCheckbox;
        private readonly OsuTabControl<BeatmapDetailAreaTabItem> tabs;
        //private readonly OsuTabSortCheckbox sort;
        private readonly Container tabsContainer;

        private Bindable<BeatmapLeaderboardSort> leSort = new Bindable<BeatmapLeaderboardSort>();
        private readonly OsuTabControl<BeatmapLeaderboardSort> sortTab;

        public BeatmapDetailAreaTabControl()
        {
            Height = HEIGHT;

            Children = new Drawable[]
            {
                new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 1,
                    Colour = Color4.White.Opacity(0.2f),
                },
                tabsContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = tabs = new OsuTabControl<BeatmapDetailAreaTabItem>
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.Both,
                        IsSwitchable = true,
                    },
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Spacing = new Vector2(5f, 0f),
                    Direction = FillDirection.Horizontal,
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Children = new Drawable[]
                    {
                        modsCheckbox = new OsuTabControlCheckbox
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                            Text = @"Selected Mods",
                            Alpha = 0,
                        },
                        sortTab = new OsuTabControl<BeatmapLeaderboardSort>
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                            Current = { BindTarget = leSort },
                            Size = new Vector2(100, 100),
                        },
                        // sort = new OsuTabSortCheckbox
                        // {
                        //     Anchor = Anchor.BottomRight,
                        //     Origin = Anchor.BottomRight,
                        // },
                    }
                }
            };

            tabs.Current.ValueChanged += _ => invokeOnFilter();
            modsCheckbox.Current.ValueChanged += _ => invokeOnFilter();
            leSort.ValueChanged += _ => invokeOnFilter();
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colour)
        {
            modsCheckbox.AccentColour = tabs.AccentColour = sortTab.AccentColour = colour.YellowLight;
        }

        private void invokeOnFilter()
        {
            OnFilter?.Invoke(tabs.Current.Value, modsCheckbox.Current.Value, leSort.Value);

            if (tabs.Current.Value.FilterableByMods)
            {
                modsCheckbox.FadeTo(1, 200, Easing.OutQuint);
                tabsContainer.Padding = new MarginPadding { Right = 100 };
            }
            else
            {
                modsCheckbox.FadeTo(0, 200, Easing.OutQuint);
                tabsContainer.Padding = new MarginPadding();
            }
        }
    }

    public partial class OsuTabSortCheckbox : Container
    {
        private OsuTabControlCheckbox checkbox;

        public Bindable<BeatmapLeaderboardSort> Sort = new Bindable<BeatmapLeaderboardSort>();

        public Color4 AccentColour
        {
            get => checkbox.AccentColour;
            set => checkbox.AccentColour = value;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Both;

            Child = checkbox = new OsuTabControlCheckbox
            {
                Text = formatSortText()
            };

            checkbox.Current.ValueChanged += checkbox => Sort.Value = checkbox.NewValue ? BeatmapLeaderboardSort.Score : BeatmapLeaderboardSort.PP;
            Sort.ValueChanged += _ => checkbox.Text = formatSortText();
        }

        private string formatSortText()
        {
            if (Sort.Value == BeatmapLeaderboardSort.Score)
                return "Order by Score";

            if (Sort.Value == BeatmapLeaderboardSort.PP)
                return "Order by PP";

            return string.Empty;
        }
    }
}
