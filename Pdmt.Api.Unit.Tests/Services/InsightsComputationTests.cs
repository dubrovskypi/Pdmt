using FluentAssertions;
using Pdmt.Api.Domain;
using Pdmt.Api.Dto.Insights;
using Pdmt.Api.Services;

namespace Pdmt.Api.Unit.Tests.Services;

public class InsightsComputationTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    // ── helpers ─────────────────────────────────────────────────────────────

    private static Tag NewTag(string name) =>
        new() { Id = Guid.NewGuid(), Name = name, UserId = Guid.NewGuid() };

    private static Event Neg(int intensity, DateTimeOffset ts, params Tag[] tags) =>
        MakeEvent(EventType.Negative, intensity, ts, false, tags);

    private static Event Pos(int intensity, DateTimeOffset ts, params Tag[] tags) =>
        MakeEvent(EventType.Positive, intensity, ts, false, tags);

    private static Event MakeEvent(EventType type, int intensity, DateTimeOffset ts, bool canInfluence, params Tag[] tags) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Title = "test",
            Timestamp = ts,
            Type = type,
            Intensity = intensity,
            CanInfluence = canInfluence,
            EventTags = tags.Select(t => new EventTag { Tag = t, TagId = t.Id }).ToList()
        };

    /// <summary>Jan N 2026 at 12:00 UTC — удобная фабрика дат для тестов.</summary>
    private static DateTimeOffset D(int day, int month = 1) =>
        new(2026, month, day, 12, 0, 0, TimeSpan.Zero);

    // ── ComputeMostIntenseTags ───────────────────────────────────────────────

    [Fact]
    public void ComputeMostIntenseTags_NoEvents_ReturnsEmptyLists()
    {
        var result = InsightsService.ComputeMostIntenseTags([]);

        result.TopPosTags.Should().BeEmpty();
        result.TopNegTags.Should().BeEmpty();
    }

    [Fact]
    public void ComputeMostIntenseTags_SeparatesPosByEventType()
    {
        var tag = NewTag("joy");
        var events = new List<Event> { Pos(8, D(1), tag), Neg(5, D(2), tag) };

        var result = InsightsService.ComputeMostIntenseTags(events);

        result.TopPosTags.Should().HaveCount(1);
        result.TopNegTags.Should().HaveCount(1);
    }

    [Fact]
    public void ComputeMostIntenseTags_SeparatesNegByEventType()
    {
        var posTag = NewTag("joy");
        var negTag = NewTag("stress");
        var events = new List<Event> { Pos(8, D(1), posTag), Neg(7, D(2), negTag) };

        var result = InsightsService.ComputeMostIntenseTags(events);

        result.TopPosTags.Single().TagName.Should().Be("joy");
        result.TopNegTags.Single().TagName.Should().Be("stress");
    }

    [Fact]
    public void ComputeMostIntenseTags_SortedByAvgIntensityDescending()
    {
        var low = NewTag("low");
        var high = NewTag("high");
        var events = new List<Event> { Pos(3, D(1), low), Pos(9, D(2), high) };

        var result = InsightsService.ComputeMostIntenseTags(events);

        result.TopPosTags[0].TagName.Should().Be("high");
        result.TopPosTags[1].TagName.Should().Be("low");
    }

    [Fact]
    public void ComputeMostIntenseTags_LimitsToTop5()
    {
        // 7 distinct positive tags — only 5 should appear
        var events = Enumerable.Range(1, 7)
            .Select(i => Pos(i, D(i), NewTag($"tag{i}")))
            .ToList();

        var result = InsightsService.ComputeMostIntenseTags(events);

        result.TopPosTags.Should().HaveCount(5);
    }

    // ── ComputeRepeatingTriggers ─────────────────────────────────────────────

    [Fact]
    public void ComputeRepeatingTriggers_EmptyEvents_ReturnsEmpty()
    {
        var result = InsightsService.ComputeRepeatingTriggers([], minCount: 3);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeRepeatingTriggers_TagBelowMinCount_IsExcluded()
    {
        var tag = NewTag("stress");
        var events = Enumerable.Range(0, 2).Select(_ => Neg(7, D(1), tag)).ToList();

        var result = InsightsService.ComputeRepeatingTriggers(events, minCount: 3);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeRepeatingTriggers_TagAtMinCount_IsIncluded()
    {
        var tag = NewTag("stress");
        var events = Enumerable.Range(0, 3).Select(_ => Neg(7, D(1), tag)).ToList();

        var result = InsightsService.ComputeRepeatingTriggers(events, minCount: 3);

        result.Should().HaveCount(1);
        result[0].TagName.Should().Be("stress");
    }

    [Fact]
    public void ComputeRepeatingTriggers_PositiveEvents_AreIgnored()
    {
        var tag = NewTag("joy");
        var events = Enumerable.Range(0, 5).Select(_ => Pos(8, D(1), tag)).ToList();

        var result = InsightsService.ComputeRepeatingTriggers(events, minCount: 3);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeRepeatingTriggers_SortedByAvgIntensityDescending()
    {
        var low = NewTag("low");
        var high = NewTag("high");
        var events = new List<Event>();
        events.AddRange(Enumerable.Range(0, 3).Select(_ => Neg(3, D(1), low)));
        events.AddRange(Enumerable.Range(0, 3).Select(_ => Neg(9, D(2), high)));

        var result = InsightsService.ComputeRepeatingTriggers(events, minCount: 3);

        result[0].TagName.Should().Be("high");
        result[1].TagName.Should().Be("low");
    }

    [Fact]
    public void ComputeRepeatingTriggers_OnlyTagsMeetingThresholdIncluded()
    {
        var rare = NewTag("rare"); // 2 events — excluded
        var freq = NewTag("freq"); // 4 events — included
        var events = new List<Event>();
        events.AddRange(Enumerable.Range(0, 2).Select(_ => Neg(5, D(1), rare)));
        events.AddRange(Enumerable.Range(0, 4).Select(_ => Neg(5, D(2), freq)));

        var result = InsightsService.ComputeRepeatingTriggers(events, minCount: 3);

        result.Should().HaveCount(1);
        result[0].TagName.Should().Be("freq");
    }

    // ── ComputeBalance ───────────────────────────────────────────────────────

    [Fact]
    public void ComputeBalance_NoEvents_ReturnsAllZeros()
    {
        var result = InsightsService.ComputeBalance([]);

        result.PosCount.Should().Be(0);
        result.NegCount.Should().Be(0);
        result.AvgPosIntensity.Should().Be(0.0);
        result.AvgNegIntensity.Should().Be(0.0);
    }

    [Fact]
    public void ComputeBalance_OnlyPositive_NegCountAndAvgAreZero()
    {
        var events = new List<Event> { Pos(7, D(1)) };

        var result = InsightsService.ComputeBalance(events);

        result.NegCount.Should().Be(0);
        result.AvgNegIntensity.Should().Be(0.0);
    }

    [Fact]
    public void ComputeBalance_OnlyNegative_PosCountAndAvgAreZero()
    {
        var events = new List<Event> { Neg(6, D(1)) };

        var result = InsightsService.ComputeBalance(events);

        result.PosCount.Should().Be(0);
        result.AvgPosIntensity.Should().Be(0.0);
    }

    [Fact]
    public void ComputeBalance_MixedEvents_CountsCorrect()
    {
        var events = new List<Event> { Pos(8, D(1)), Pos(6, D(2)), Neg(5, D(3)) };

        var result = InsightsService.ComputeBalance(events);

        result.PosCount.Should().Be(2);
        result.NegCount.Should().Be(1);
    }

    [Fact]
    public void ComputeBalance_MixedEvents_AvgIntensitiesCorrect()
    {
        var events = new List<Event> { Pos(8, D(1)), Pos(6, D(2)), Neg(4, D(3)) };

        var result = InsightsService.ComputeBalance(events);

        result.AvgPosIntensity.Should().BeApproximately(7.0, 0.001);
        result.AvgNegIntensity.Should().BeApproximately(4.0, 0.001);
    }

    // ── ComputeTrends ────────────────────────────────────────────────────────

    [Fact]
    public void ComputeTrends_NoEvents_ReturnsEmpty()
    {
        var result = InsightsService.ComputeTrends([], Granularity.Week, Utc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeTrends_WeekGranularity_GroupsByMonday()
    {
        // Jan 5 2026 = Mon, Jan 6 = Tue (same week), Jan 12 = Mon (next week)
        var events = new List<Event>
        {
            Pos(5, D(5)),  // Mon week1
            Neg(6, D(6)),  // Tue week1 — same group
            Pos(7, D(12))  // Mon week2
        };

        var result = InsightsService.ComputeTrends(events, Granularity.Week, Utc);

        result.Should().HaveCount(2);
        result[0].PeriodStart.Should().Be(new DateOnly(2026, 1, 5));
        result[1].PeriodStart.Should().Be(new DateOnly(2026, 1, 12));
    }

    [Fact]
    public void ComputeTrends_MonthGranularity_GroupsByFirstOfMonth()
    {
        var events = new List<Event>
        {
            Pos(5, D(10)),          // January
            Neg(6, D(20)),          // January — same group
            Pos(7, D(5, month: 2))  // February
        };

        var result = InsightsService.ComputeTrends(events, Granularity.Month, Utc);

        result.Should().HaveCount(2);
        result[0].PeriodStart.Should().Be(new DateOnly(2026, 1, 1));
        result[1].PeriodStart.Should().Be(new DateOnly(2026, 2, 1));
    }

    [Fact]
    public void ComputeTrends_CountsCorrectlyByType()
    {
        // All in same week (Jan 5–11 2026)
        var events = new List<Event> { Pos(8, D(5)), Pos(6, D(6)), Neg(5, D(7)) };

        var result = InsightsService.ComputeTrends(events, Granularity.Week, Utc);

        result.Should().HaveCount(1);
        result[0].PosCount.Should().Be(2);
        result[0].NegCount.Should().Be(1);
    }

    [Fact]
    public void ComputeTrends_AvgIntensityPerPeriodCorrect()
    {
        var events = new List<Event> { Pos(4, D(5)), Pos(8, D(6)) }; // same week

        var result = InsightsService.ComputeTrends(events, Granularity.Week, Utc);

        result[0].AvgIntensity.Should().BeApproximately(6.0, 0.001);
    }

    // ── ComputeDiscountedPositives ───────────────────────────────────────────

    [Fact]
    public void ComputeDiscountedPositives_EmptyEvents_ReturnsEmpty()
    {
        var result = InsightsService.ComputeDiscountedPositives([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiscountedPositives_CountBelow5_IsExcluded()
    {
        var tag = NewTag("yoga");
        var events = Enumerable.Range(0, 4).Select(_ => Pos(3, D(1), tag)).ToList();

        var result = InsightsService.ComputeDiscountedPositives(events);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiscountedPositives_Count5AndAvgBelow4_IsIncluded()
    {
        var tag = NewTag("yoga");
        var events = Enumerable.Range(0, 5).Select(_ => Pos(3, D(1), tag)).ToList();

        var result = InsightsService.ComputeDiscountedPositives(events);

        result.Should().HaveCount(1);
        result[0].TagName.Should().Be("yoga");
    }

    [Fact]
    public void ComputeDiscountedPositives_AvgExactly4_IsExcluded()
    {
        // avg == 4.0 — граничное значение, строгое неравенство < 4.0
        var tag = NewTag("yoga");
        var events = Enumerable.Range(0, 5).Select(_ => Pos(4, D(1), tag)).ToList();

        var result = InsightsService.ComputeDiscountedPositives(events);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiscountedPositives_AvgAbove4_IsExcluded()
    {
        var tag = NewTag("yoga");
        var events = Enumerable.Range(0, 5).Select(_ => Pos(7, D(1), tag)).ToList();

        var result = InsightsService.ComputeDiscountedPositives(events);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiscountedPositives_NegativeEvents_AreIgnored()
    {
        var tag = NewTag("stress");
        var events = Enumerable.Range(0, 5).Select(_ => Neg(3, D(1), tag)).ToList();

        var result = InsightsService.ComputeDiscountedPositives(events);

        result.Should().BeEmpty();
    }

    // ── ComputeWeekdayStats ──────────────────────────────────────────────────

    [Fact]
    public void ComputeWeekdayStats_NoEvents_ReturnsSevenZeroedDays()
    {
        var result = InsightsService.ComputeWeekdayStats([], Utc);

        result.Should().HaveCount(7);
        result.Should().AllSatisfy(d =>
        {
            d.PosCount.Should().Be(0);
            d.NegCount.Should().Be(0);
            d.AvgIntensity.Should().Be(0.0);
        });
    }

    [Fact]
    public void ComputeWeekdayStats_AlwaysReturnsExactlySevenDays()
    {
        var events = new List<Event> { Pos(5, D(5)) }; // just one Monday

        var result = InsightsService.ComputeWeekdayStats(events, Utc);

        result.Should().HaveCount(7);
    }

    [Fact]
    public void ComputeWeekdayStats_MondayIsFirst()
    {
        var result = InsightsService.ComputeWeekdayStats([], Utc);

        result[0].Day.Should().Be("Monday");
    }

    [Fact]
    public void ComputeWeekdayStats_SundayIsLast()
    {
        var result = InsightsService.ComputeWeekdayStats([], Utc);

        result[6].Day.Should().Be("Sunday");
    }

    [Fact]
    public void ComputeWeekdayStats_EventsOnOneDay_OtherDaysAreZero()
    {
        // Jan 5 2026 = Monday at 12:00 UTC → ToLocalDate(UTC) = Monday
        var events = new List<Event> { Pos(8, D(5)), Neg(6, D(5)) };

        var result = InsightsService.ComputeWeekdayStats(events, Utc);

        var monday = result[0];
        monday.Day.Should().Be("Monday");
        monday.PosCount.Should().Be(1);
        monday.NegCount.Should().Be(1);
        result.Skip(1).Should().AllSatisfy(d => d.PosCount.Should().Be(0));
    }

    // ── ComputeInfluenceabilitySplit ─────────────────────────────────────────

    [Fact]
    public void ComputeInfluenceabilitySplit_NoNegativeEvents_ReturnsZeros()
    {
        // Only a positive event — no negative events to split
        var events = new List<Event> { Pos(7, D(1)) };

        var result = InsightsService.ComputeInfluenceabilitySplit(events);

        result.CanInfluenceCount.Should().Be(0);
        result.CannotInfluenceCount.Should().Be(0);
        result.CanInfluenceAvgIntensity.Should().Be(0.0);
        result.CannotInfluenceAvgIntensity.Should().Be(0.0);
    }

    [Fact]
    public void ComputeInfluenceabilitySplit_PositiveEvents_AreIgnored()
    {
        var events = new List<Event>
        {
            MakeEvent(EventType.Positive, 8, D(1), canInfluence: true)
        };

        var result = InsightsService.ComputeInfluenceabilitySplit(events);

        result.CanInfluenceCount.Should().Be(0);
    }

    [Fact]
    public void ComputeInfluenceabilitySplit_AllCanInfluence_CannotInfluenceAvgIsZero()
    {
        var events = new List<Event>
        {
            MakeEvent(EventType.Negative, 7, D(1), canInfluence: true),
            MakeEvent(EventType.Negative, 5, D(2), canInfluence: true)
        };

        var result = InsightsService.ComputeInfluenceabilitySplit(events);

        result.CanInfluenceCount.Should().Be(2);
        result.CannotInfluenceCount.Should().Be(0);
        result.CannotInfluenceAvgIntensity.Should().Be(0.0);
    }

    [Fact]
    public void ComputeInfluenceabilitySplit_AllCannotInfluence_CanInfluenceAvgIsZero()
    {
        var events = new List<Event>
        {
            MakeEvent(EventType.Negative, 6, D(1), canInfluence: false),
            MakeEvent(EventType.Negative, 8, D(2), canInfluence: false)
        };

        var result = InsightsService.ComputeInfluenceabilitySplit(events);

        result.CannotInfluenceCount.Should().Be(2);
        result.CanInfluenceCount.Should().Be(0);
        result.CanInfluenceAvgIntensity.Should().Be(0.0);
    }

    [Fact]
    public void ComputeInfluenceabilitySplit_MixedEvents_SplitsCorrectly()
    {
        var events = new List<Event>
        {
            MakeEvent(EventType.Negative, 8, D(1), canInfluence: true),
            MakeEvent(EventType.Negative, 6, D(2), canInfluence: false),
            MakeEvent(EventType.Negative, 4, D(3), canInfluence: false)
        };

        var result = InsightsService.ComputeInfluenceabilitySplit(events);

        result.CanInfluenceCount.Should().Be(1);
        result.CanInfluenceAvgIntensity.Should().BeApproximately(8.0, 0.001);
        result.CannotInfluenceCount.Should().Be(2);
        result.CannotInfluenceAvgIntensity.Should().BeApproximately(5.0, 0.001);
    }

    // ── ComputeNextDayEffects ────────────────────────────────────────────────

    [Fact]
    public void ComputeNextDayEffects_EmptyEvents_ReturnsEmpty()
    {
        var result = InsightsService.ComputeNextDayEffects([], D(10), Utc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeNextDayEffects_TagOnTwoDays_IsExcluded()
    {
        var tag = NewTag("stress");
        var to = D(10);
        var events = new List<Event>
        {
            Neg(6, D(1), tag),
            Neg(6, D(3), tag),
            // next-day events so dayScores exist
            Pos(5, D(2)),
            Pos(5, D(4))
        };

        var result = InsightsService.ComputeNextDayEffects(events, to, Utc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeNextDayEffects_TagOnThreeDays_IsIncluded()
    {
        var tag = NewTag("stress");
        var to = D(10);
        var events = new List<Event>
        {
            Neg(6, D(1), tag),
            Neg(6, D(3), tag),
            Neg(6, D(5), tag),
            // next-day events so dayScores are non-empty
            Pos(8, D(2)),
            Pos(7, D(4)),
            Pos(6, D(6))
        };

        var result = InsightsService.ComputeNextDayEffects(events, to, Utc);

        result.Should().HaveCount(1);
        result[0].TagName.Should().Be("stress");
    }

    [Fact]
    public void ComputeNextDayEffects_TagWithNoNextDayData_IsExcluded()
    {
        // tag on 3 days, but none of the following days have any events
        var tag = NewTag("alone");
        var to = D(10);
        var events = new List<Event>
        {
            Neg(6, D(1), tag),
            Neg(6, D(3), tag),
            Neg(6, D(5), tag)
        };

        var result = InsightsService.ComputeNextDayEffects(events, to, Utc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeNextDayEffects_SortedByAbsoluteNextDayScore()
    {
        // tagA: next days are all strongly positive (+8)
        // tagB: next days are all strongly negative (−8)
        // Both |score| = 8; test verifies both are returned, ordered by absolute value
        var tagA = NewTag("alpha");
        var tagB = NewTag("beta");
        var to = D(20);

        var events = new List<Event>
        {
            // tagA on days 1, 3, 5; next days 2, 4, 6 are positive
            Neg(5, D(1), tagA), Pos(8, D(2)),
            Neg(5, D(3), tagA), Pos(8, D(4)),
            Neg(5, D(5), tagA), Pos(8, D(6)),
            // tagB on days 7, 9, 11; next days 8, 10, 12 are negative
            Neg(5, D(7), tagB), Neg(8, D(8)),
            Neg(5, D(9), tagB), Neg(8, D(10)),
            Neg(5, D(11), tagB), Neg(8, D(12))
        };

        var result = InsightsService.ComputeNextDayEffects(events, to, Utc);

        result.Should().HaveCount(2);
        result.Select(r => r.TagName).Should().BeEquivalentTo(new[] { "alpha", "beta" });
    }

    // ── ComputeTagCombos ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeTagCombos_NoEvents_ReturnsEmpty()
    {
        var result = InsightsService.ComputeTagCombos([], Utc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeTagCombos_PairCoOccursTwice_IsExcluded()
    {
        var a = NewTag("aaa");
        var b = NewTag("bbb");
        var events = new List<Event> { Pos(7, D(1), a, b), Pos(7, D(2), a, b) };

        var result = InsightsService.ComputeTagCombos(events, Utc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeTagCombos_PairCoOccursThreeTimes_IsIncluded()
    {
        var a = NewTag("aaa");
        var b = NewTag("bbb");
        var events = new List<Event>
        {
            Pos(7, D(1), a, b),
            Pos(7, D(2), a, b),
            Pos(7, D(3), a, b)
        };

        var result = InsightsService.ComputeTagCombos(events, Utc);

        result.Should().HaveCount(1);
        result[0].CoOccurrences.Should().Be(3);
    }

    [Fact]
    public void ComputeTagCombos_AloneScoresCalculatedCorrectly()
    {
        var a = NewTag("aaa");
        var b = NewTag("bbb");

        // 3 co-occurrence days (intensity 6 each → dayScore = +6)
        var combined = Enumerable.Range(1, 3).Select(i => Pos(6, D(i), a, b)).ToList();
        // 1 alone day for a: intensity 8 → dayScore = +8
        // 1 alone day for b: intensity 4 → dayScore = +4
        var events = combined.Concat([Pos(8, D(10), a), Pos(4, D(11), b)]).ToList();

        var result = InsightsService.ComputeTagCombos(events, Utc);

        result.Should().HaveCount(1);
        // key = ("aaa","bbb") — Tag1="aaa" alone on day 10, Tag2="bbb" alone on day 11
        result[0].Tag1AloneAvgScore.Should().BeApproximately(8.0, 0.001);
        result[0].Tag2AloneAvgScore.Should().BeApproximately(4.0, 0.001);
    }

    [Fact]
    public void ComputeTagCombos_PairKeyIsAlphabeticallySorted()
    {
        var z = NewTag("zebra");
        var a = NewTag("apple");
        var events = Enumerable.Range(1, 3).Select(i => Pos(5, D(i), z, a)).ToList();

        var result = InsightsService.ComputeTagCombos(events, Utc);

        result.Should().HaveCount(1);
        // Ordinal: "apple" < "zebra" → Tag1="apple", Tag2="zebra"
        result[0].Tag1.Should().Be("apple");
        result[0].Tag2.Should().Be("zebra");
    }

    // ── ComputeTagTrend ──────────────────────────────────────────────────────

    [Fact]
    public void ComputeTagTrend_NoEvents_ReturnsEmpty()
    {
        var result = InsightsService.ComputeTagTrend([], Granularity.Week, Utc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeTagTrend_ReturnsOnlyTop3Tags()
    {
        // 4 tags with 10, 8, 6, 2 occurrences — 4th tag excluded
        var t1 = NewTag("t1");
        var t2 = NewTag("t2");
        var t3 = NewTag("t3");
        var t4 = NewTag("t4");

        var events = new List<Event>();
        for (var i = 0; i < 10; i++) events.Add(Pos(5, D(i + 1), t1));
        for (var i = 0; i < 8; i++) events.Add(Pos(5, D(i + 1), t2));
        for (var i = 0; i < 6; i++) events.Add(Pos(5, D(i + 1), t3));
        for (var i = 0; i < 2; i++) events.Add(Pos(5, D(i + 1), t4));

        var result = InsightsService.ComputeTagTrend(events, Granularity.Week, Utc);

        result.Should().HaveCount(3);
        result.Select(s => s.TagName).Should().NotContain("t4");
    }

    [Fact]
    public void ComputeTagTrend_WeekGranularity_GroupsPointsByMonday()
    {
        // Jan 5 2026 = Mon, Jan 7 = Wed (same week), Jan 12 = next Mon
        var tag = NewTag("focus");
        var events = new List<Event>
        {
            Pos(5, D(5), tag),
            Pos(5, D(7), tag),  // same week as Jan 5
            Pos(5, D(12), tag)  // next week
        };

        var result = InsightsService.ComputeTagTrend(events, Granularity.Week, Utc);

        result.Should().HaveCount(1);
        result[0].Points.Should().HaveCount(2);
        result[0].Points[0].PeriodStart.Should().Be(new DateOnly(2026, 1, 5));
        result[0].Points[1].PeriodStart.Should().Be(new DateOnly(2026, 1, 12));
    }

    [Fact]
    public void ComputeTagTrend_AvgIntensityPerPeriodCorrect()
    {
        // Two events in same week — avg intensity should be (4+8)/2 = 6
        var tag = NewTag("focus");
        var events = new List<Event> { Pos(4, D(5), tag), Pos(8, D(6), tag) };

        var result = InsightsService.ComputeTagTrend(events, Granularity.Week, Utc);

        result[0].Points[0].AvgIntensity.Should().BeApproximately(6.0, 0.001);
    }
}
