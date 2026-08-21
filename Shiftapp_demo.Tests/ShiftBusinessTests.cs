using Shiftapp_demo.Business;
using Shiftapp_demo.Models;
using Xunit;
using static Shiftapp_demo.DataAccess.MainDatabaseHelper;

namespace Shiftapp_demo.Tests;

// ShiftBusiness の DB非依存な純粋関数（internal static に引き上げ済み）に対するユニットテスト。
// GenerateNightDutiesForMonth/UpdateSaturdayShifts 等、DBアクセスを伴うメソッドはこのプロジェクトの対象外。
public class ShiftBusinessTests
{
    // ===== GetWorkingClass =====
    // Tier1で修正した「日曜日の班判定が翌週の班になってしまう」バグの回帰テスト。
    // 修正前は GetWorkingClass(day.AddDays(1), baselineIsA) が日曜日に対して使われていたため、
    // 「その週末に本来働いていた班」と逆の班を参照してしまっていた。

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void GetWorkingClass_SundayMatchesPrecedingSaturday(int weeksFromBaseline, bool baselineIsA)
    {
        var saturday = ShiftBusiness._baselineSaturday.AddDays(7 * weeksFromBaseline);
        var sunday = saturday.AddDays(1);

        var satClass = ShiftBusiness.GetWorkingClass(saturday, baselineIsA);
        var sunClass = ShiftBusiness.GetWorkingClass(sunday, baselineIsA);

        Assert.Equal(satClass, sunClass);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    public void GetWorkingClass_UsingDayPlusOne_ForSunday_GivesOppositeClass_ThisWasTheBug(int weeksFromBaseline, bool baselineIsA)
    {
        // 修正前の実装が事実上行っていた計算 (day.AddDays(1) を経由した GetWorkingClass 呼び出し) を再現し、
        // 正しい値 (dayを直接渡した場合) と食い違うことを明示する。
        var saturday = ShiftBusiness._baselineSaturday.AddDays(7 * weeksFromBaseline);
        var sunday = saturday.AddDays(1);

        var correctClass = ShiftBusiness.GetWorkingClass(sunday, baselineIsA);
        var buggyClass = ShiftBusiness.GetWorkingClass(sunday.AddDays(1), baselineIsA); // 修正前のコードが実質使っていた値

        Assert.NotEqual(correctClass, buggyClass);
    }

    [Fact]
    public void GetWorkingClass_FridayMatchesFollowingSaturday()
    {
        var saturday = ShiftBusiness._baselineSaturday.AddDays(14);
        var friday = saturday.AddDays(-1);

        Assert.Equal(
            ShiftBusiness.GetWorkingClass(saturday, false),
            ShiftBusiness.GetWorkingClass(friday, false));
    }

    [Fact]
    public void GetWorkingClass_AlternatesEveryWeek()
    {
        var week0 = ShiftBusiness.GetWorkingClass(ShiftBusiness._baselineSaturday, false);
        var week1 = ShiftBusiness.GetWorkingClass(ShiftBusiness._baselineSaturday.AddDays(7), false);
        var week2 = ShiftBusiness.GetWorkingClass(ShiftBusiness._baselineSaturday.AddDays(14), false);

        Assert.NotEqual(week0, week1);
        Assert.Equal(week0, week2);
    }

    [Fact]
    public void GetWorkingClass_BaselineWeek_ReflectsBaselineIsA()
    {
        Assert.Equal("A", ShiftBusiness.GetWorkingClass(ShiftBusiness._baselineSaturday, true));
        Assert.Equal("B", ShiftBusiness.GetWorkingClass(ShiftBusiness._baselineSaturday, false));
    }

    // ===== FilterBySaturday =====

    [Fact]
    public void FilterBySaturday_CanSatWorkTrue_ReturnsOnlyMatchingClass()
    {
        var employees = new List<Employee>
        {
            new() { EmployeeId = 1, SaturdayClass = "A" },
            new() { EmployeeId = 2, SaturdayClass = "B" },
            new() { EmployeeId = 3, SaturdayClass = "a" }, // 大文字小文字は無視される
        };

        var result = ShiftBusiness.FilterBySaturday(employees, "A", DateTime.Today, canSatWork: true);

        Assert.Equal(new[] { 1, 3 }, result.Select(e => e.EmployeeId).OrderBy(x => x));
    }

    [Fact]
    public void FilterBySaturday_CanSatWorkFalse_ReturnsOnlyNonMatchingClass()
    {
        var employees = new List<Employee>
        {
            new() { EmployeeId = 1, SaturdayClass = "A" },
            new() { EmployeeId = 2, SaturdayClass = "B" },
        };

        var result = ShiftBusiness.FilterBySaturday(employees, "A", DateTime.Today, canSatWork: false);

        Assert.Equal(new[] { 2 }, result.Select(e => e.EmployeeId));
    }

    [Fact]
    public void FilterBySaturday_NoMatches_FallsBackToOriginalPool()
    {
        var employees = new List<Employee>
        {
            new() { EmployeeId = 1, SaturdayClass = "A" },
            new() { EmployeeId = 2, SaturdayClass = "A" },
        };

        // canSatWork:true かつ satClass:"B" -> 誰もマッチしない -> フォールバックで全員返る
        var result = ShiftBusiness.FilterBySaturday(employees, "B", DateTime.Today, canSatWork: true);

        Assert.Equal(2, result.Count);
    }

    // ===== TrySetWithPriority =====

    [Fact]
    public void TrySetWithPriority_KeyAbsent_SetsValueAndReturnsTrue()
    {
        var map = new Dictionary<(int EmployeeId, DateTime Date), int>();
        var upserts = new List<ShiftWrite>();
        var date = DateTime.Today;

        var result = ShiftBusiness.TrySetWithPriority(map, upserts, eid: 1, d: date, newStid: 10);

        Assert.True(result);
        Assert.Equal(10, map[(1, date.Date)]);
        Assert.Single(upserts);
    }

    [Fact]
    public void TrySetWithPriority_KeyPresent_NoGuard_Overwrites()
    {
        var date = DateTime.Today;
        var map = new Dictionary<(int EmployeeId, DateTime Date), int> { [(1, date.Date)] = 5 };
        var upserts = new List<ShiftWrite>();

        var result = ShiftBusiness.TrySetWithPriority(map, upserts, eid: 1, d: date, newStid: 10);

        Assert.True(result);
        Assert.Equal(10, map[(1, date.Date)]);
    }

    [Fact]
    public void TrySetWithPriority_CannotOverwriteTrue_RefusesAndLeavesMapUnchanged()
    {
        var date = DateTime.Today;
        var map = new Dictionary<(int EmployeeId, DateTime Date), int> { [(1, date.Date)] = 5 };
        var upserts = new List<ShiftWrite>();

        var result = ShiftBusiness.TrySetWithPriority(map, upserts, eid: 1, d: date, newStid: 10, cannotOverwrite: cur => true);

        Assert.False(result);
        Assert.Equal(5, map[(1, date.Date)]);
        Assert.Empty(upserts);
    }

    [Fact]
    public void TrySetWithPriority_CannotOverwriteFalse_Overwrites()
    {
        var date = DateTime.Today;
        var map = new Dictionary<(int EmployeeId, DateTime Date), int> { [(1, date.Date)] = 5 };
        var upserts = new List<ShiftWrite>();

        var result = ShiftBusiness.TrySetWithPriority(map, upserts, eid: 1, d: date, newStid: 10, cannotOverwrite: cur => false);

        Assert.True(result);
        Assert.Equal(10, map[(1, date.Date)]);
    }

    // ===== NextWeekday =====

    [Fact]
    public void NextWeekday_ReturnsNextOccurrence_NotSameDay()
    {
        var monday = new DateTime(2025, 9, 1); // Monday
        var result = ShiftBusiness.NextWeekday(monday, DayOfWeek.Monday);

        Assert.Equal(monday.AddDays(7), result);
    }

    [Fact]
    public void NextWeekday_Friday_ToWednesday_Is5DaysLater()
    {
        var friday = new DateTime(2025, 9, 26);
        Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);

        var result = ShiftBusiness.NextWeekday(friday, DayOfWeek.Wednesday);

        Assert.Equal(new DateTime(2025, 10, 1), result);
    }

    // ===== GetCompWorkOff =====

    [Fact]
    public void GetCompWorkOff_Friday_ReturnsFollowingWednesday()
    {
        var friday = new DateTime(2025, 9, 5);
        Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);

        var result = ShiftBusiness.GetCompWorkOff(friday, new List<DateTime>());

        Assert.Equal(new DateTime(2025, 9, 10), result);
    }

    [Fact]
    public void GetCompWorkOff_Saturday_ReturnsTwoDaysLater_EvenAcrossMonthBoundary()
    {
        var saturday = new DateTime(2025, 8, 30);
        Assert.Equal(DayOfWeek.Saturday, saturday.DayOfWeek);

        var result = ShiftBusiness.GetCompWorkOff(saturday, new List<DateTime>());

        // 土曜/日曜は翌月跨ぎOK（フォールバック対象外）
        Assert.Equal(new DateTime(2025, 9, 1), result);
    }

    [Fact]
    public void GetCompWorkOff_Sunday_ReturnsTwoDaysLater()
    {
        var sunday = new DateTime(2025, 9, 7);
        Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);

        var result = ShiftBusiness.GetCompWorkOff(sunday, new List<DateTime>());

        Assert.Equal(new DateTime(2025, 9, 9), result);
    }

    [Fact]
    public void GetCompWorkOff_WeekdayHoliday_ReturnsThreeDaysLater()
    {
        var tuesday = new DateTime(2025, 9, 2);
        Assert.Equal(DayOfWeek.Tuesday, tuesday.DayOfWeek);

        var result = ShiftBusiness.GetCompWorkOff(tuesday, new List<DateTime> { tuesday });

        Assert.Equal(new DateTime(2025, 9, 5), result);
    }

    [Fact]
    public void GetCompWorkOff_NonHolidayWeekday_ReturnsNull()
    {
        var tuesday = new DateTime(2025, 9, 2);

        var result = ShiftBusiness.GetCompWorkOff(tuesday, new List<DateTime>());

        Assert.Null(result);
    }

    [Fact]
    public void GetCompWorkOff_FridayNearMonthEnd_FallsBackToLastBusinessDayOfSameMonth()
    {
        // 翌水曜が翌月に出るケース: 直前の営業日（同月内）に付け替えられる
        var friday = new DateTime(2025, 9, 26);
        Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);

        var result = ShiftBusiness.GetCompWorkOff(friday, new List<DateTime>());

        Assert.NotNull(result);
        Assert.Equal(9, result!.Value.Month);
        Assert.Equal(new DateTime(2025, 9, 30), result);
    }

    [Fact]
    public void GetCompWorkOff_FridayNearMonthEnd_SkipsMultipleDaysIntoNextMonth_StillFallsBackWithinSameMonth()
    {
        // PickPrevBusinessDayWithin の回帰テスト:
        // 付け替え候補日(candidate)が翌月に2日以上入り込む場合、以前は最初の1日だけを見て
        // 探索を打ち切ってしまい、同月内に有効な営業日があっても null を返してしまっていた。
        var friday = new DateTime(2025, 9, 26); // 次の水曜は 10/1 だが、それを祝日にして 10/2 まで押し出す
        var holidays = new List<DateTime> { new DateTime(2025, 10, 1) };

        var result = ShiftBusiness.GetCompWorkOff(friday, holidays);

        Assert.Equal(new DateTime(2025, 9, 30), result);
    }

    [Fact]
    public void GetCompWorkOff_NoValidFallbackWithinSameMonth_ReturnsNull_RatherThanCollidingWithDutyDay()
    {
        // 月末最後の金曜の直後が土日で月が終わる場合、同月内に有効な代休日が存在しない。
        // このとき当直日自身や当直日より前を代休日として返すと当直シフトを上書きしてしまうため、
        // 代休なし(null)を返すのが安全な挙動。
        var friday = new DateTime(2025, 8, 29); // 8/30(土), 8/31(日) で月が終わる
        Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);
        Assert.Equal(new DateTime(2025, 8, 31), ShiftBusiness.GetMonthRange(friday).last);

        var result = ShiftBusiness.GetCompWorkOff(friday, new List<DateTime>());

        Assert.Null(result);
    }

    // ===== GetMonthRange / GetSaturdaysInMonth / GetSundaysInMonth =====

    [Fact]
    public void GetMonthRange_ReturnsFirstAndLastDayOfMonth()
    {
        var (first, last) = ShiftBusiness.GetMonthRange(new DateTime(2025, 9, 15));

        Assert.Equal(new DateTime(2025, 9, 1), first);
        Assert.Equal(new DateTime(2025, 9, 30), last);
    }

    [Fact]
    public void GetSaturdaysInMonth_ReturnsAllSaturdays()
    {
        var result = ShiftBusiness.GetSaturdaysInMonth(new DateTime(2025, 9, 1));

        Assert.All(result, d => Assert.Equal(DayOfWeek.Saturday, d.DayOfWeek));
        Assert.Equal(new[] { 6, 13, 20, 27 }, result.Select(d => d.Day));
    }

    [Fact]
    public void GetSundaysInMonth_ReturnsAllSundays()
    {
        var result = ShiftBusiness.GetSundaysInMonth(new DateTime(2025, 9, 1));

        Assert.All(result, d => Assert.Equal(DayOfWeek.Sunday, d.DayOfWeek));
        Assert.Equal(new[] { 7, 14, 21, 28 }, result.Select(d => d.Day));
    }

    // ===== AkeAlsoLandsOnHoliday（二重代休の対象判定：当直日が日曜/祝日で明けも祝日のときだけ） =====

    [Fact]
    public void AkeAlsoLandsOnHoliday_SundayDuty_NextDayIsHoliday_True()
    {
        var sunday = ShiftBusiness._baselineSaturday.AddDays(1);
        var monday = sunday.AddDays(1);
        var holidays = new List<DateTime> { monday };

        Assert.True(ShiftBusiness.AkeAlsoLandsOnHoliday(sunday, holidays));
    }

    [Fact]
    public void AkeAlsoLandsOnHoliday_SundayDuty_NextDayIsNotHoliday_False()
    {
        var sunday = ShiftBusiness._baselineSaturday.AddDays(1);

        Assert.False(ShiftBusiness.AkeAlsoLandsOnHoliday(sunday, new List<DateTime>()));
    }

    [Fact]
    public void AkeAlsoLandsOnHoliday_ConsecutiveHolidays_LikeSilverWeek_True()
    {
        // 当直日自体が祝日(月)で、明け(火)も祝日というシルバーウィーク型の連続祝日パターン
        var monday = ShiftBusiness._baselineSaturday.AddDays(2);
        var tuesday = monday.AddDays(1);
        var holidays = new List<DateTime> { monday, tuesday };

        Assert.True(ShiftBusiness.AkeAlsoLandsOnHoliday(monday, holidays));
    }

    [Fact]
    public void AkeAlsoLandsOnHoliday_HolidayDuty_NextDayNotHoliday_False()
    {
        var monday = ShiftBusiness._baselineSaturday.AddDays(2);
        var holidays = new List<DateTime> { monday }; // 翌日(火)は祝日でない

        Assert.False(ShiftBusiness.AkeAlsoLandsOnHoliday(monday, holidays));
    }

    [Fact]
    public void AkeAlsoLandsOnHoliday_RegularWeekdayDuty_EvenIfNextDayIsHoliday_False()
    {
        // 当直日自体が日曜でも祝日でもない場合、明けが祝日でも対象外
        var tuesday = ShiftBusiness._baselineSaturday.AddDays(3);
        var wednesday = tuesday.AddDays(1);
        var holidays = new List<DateTime> { wednesday };

        Assert.False(ShiftBusiness.AkeAlsoLandsOnHoliday(tuesday, holidays));
    }

    [Fact]
    public void AkeAlsoLandsOnHoliday_SaturdayDuty_NeverTrue_EvenIfSundayIsHoliday()
    {
        // 土曜当直（明けは常に日曜）はそもそも当直日側の条件(日曜/祝日)を満たさないため対象外
        var saturday = ShiftBusiness._baselineSaturday;
        var sunday = saturday.AddDays(1);
        var holidays = new List<DateTime> { sunday };

        Assert.False(ShiftBusiness.AkeAlsoLandsOnHoliday(saturday, holidays));
    }

    // ===== GetExtraCompWorkOffForRestfulAke（明けが休みの日の追加代休の対象日） =====

    [Fact]
    public void GetExtraCompWorkOffForRestfulAke_SaturdayDuty_ReturnsDistinctDateAfterPrimary()
    {
        var saturday = ShiftBusiness._baselineSaturday;
        var holidays = new List<DateTime>();
        var primary = ShiftBusiness.GetCompWorkOff(saturday, holidays);

        var extra = ShiftBusiness.GetExtraCompWorkOffForRestfulAke(saturday, primary, holidays);

        Assert.NotNull(extra);
        Assert.NotEqual(primary, extra);
        Assert.True(extra > saturday);
        Assert.True(ShiftBusiness.IsBusinessDay(extra!.Value, holidays));
    }

    [Fact]
    public void GetExtraCompWorkOffForRestfulAke_WeekdayDutyWithHolidayAke_GrantsExtraEvenWithoutPrimary()
    {
        // 当直日自体は祝日ではない平日(火曜)だが、明け(水曜)が祝日というケース。
        // GetCompWorkOffは当直日自身の曜日/祝日区分しか見ないためprimaryはnullになるが、
        // 明けが休みという事実は別途拾って代休を確保する必要がある。
        var duty = ShiftBusiness._baselineSaturday.AddDays(-4); // 火曜日
        var ake = duty.AddDays(1); // 水曜日
        var holidays = new List<DateTime> { ake };

        var primary = ShiftBusiness.GetCompWorkOff(duty, holidays);
        Assert.Null(primary);

        var extra = ShiftBusiness.GetExtraCompWorkOffForRestfulAke(duty, primary, holidays);

        Assert.NotNull(extra);
        Assert.True(extra > duty);
        Assert.True(ShiftBusiness.IsBusinessDay(extra!.Value, holidays));
    }
}
