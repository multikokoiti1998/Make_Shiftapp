using Shiftapp_demo.Business;
using Shiftapp_demo.Models;
using Xunit;

namespace Shiftapp_demo.Tests;

// ShiftsSolver（CP-SATベースの当直生成）のユニットテスト。
// コンストラクタはDBハンドルを持たずプレーンなデータのみを受け取るため、
// ShiftBusinessTestsと同様にDBなしでテストできる。
public class ShiftsSolverTests
{
    private const int StidDuty = 1;
    private const int StidAfterDuty = 2;
    private const int StidSubOff = 3;
    private const int StidOff = 4;
    private const int StidDayWork = 5;

    // 2026年2月(28日、うるう年でない)を対象月にすることでCP-SATモデルを小さく保つ
    private static readonly DateTime Month = new(2026, 2, 1);

    private static List<Employee> BuildSymmetricEmployees(int cathCount = 8, int nonCathCount = 8)
    {
        var list = new List<Employee>();
        int id = 1;
        for (int i = 0; i < cathCount; i++)
        {
            list.Add(new Employee
            {
                EmployeeId = id++,
                EmployeeName = $"Cath{i}",
                CanDoCatheterization = true,
                SaturdayClass = "", // 空文字にして土曜班フィルタを無効化し、可解性を安定させる
                CanDoNightDuty = true,
                CanDayDuty = false,
            });
        }
        for (int i = 0; i < nonCathCount; i++)
        {
            list.Add(new Employee
            {
                EmployeeId = id++,
                EmployeeName = $"NonCath{i}",
                CanDoCatheterization = false,
                SaturdayClass = "",
                CanDoNightDuty = true,
                CanDayDuty = false,
            });
        }
        return list;
    }

    // ===== ComputeDayWorkWeight（日勤の優先度を公平性・希望より確実に上回らせる重みの計算） =====

    [Fact]
    public void ComputeDayWorkWeight_ExceedsFairnessAndPreferencePenalties()
    {
        var weights = new[] { 1_000_000, 1_000_000, -1_000_000 };

        int result = ShiftsSolver.ComputeDayWorkWeight(fairnessWeight: 100, daysCount: 31, preferenceWeights: weights);

        long maxFairness = 100L * 31;
        long maxPreference = weights.Sum(w => (long)Math.Abs(w));
        Assert.True(result > maxFairness + maxPreference);
    }

    [Fact]
    public void ComputeDayWorkWeight_NoPreferences_StillExceedsFairness()
    {
        int result = ShiftsSolver.ComputeDayWorkWeight(fairnessWeight: 100, daysCount: 31, preferenceWeights: Array.Empty<int>());

        Assert.True(result > 100 * 31);
    }

    [Fact]
    public void ComputeDayWorkWeight_ExtremeWeights_NeverThrowsOrOverflows()
    {
        var weights = Enumerable.Repeat(int.MaxValue, 100).ToArray();

        var exception = Record.Exception(() =>
            ShiftsSolver.ComputeDayWorkWeight(fairnessWeight: 100, daysCount: 31, preferenceWeights: weights));

        Assert.Null(exception);
    }

    [Fact]
    public void Solve_SatisfiesCoreConstraints_AndCompOffMatchesShiftBusiness()
    {
        var employees = BuildSymmetricEmployees();
        var holidays = new List<DateTime>();

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();

        var duties = writes.Where(w => w.ShiftTypeId == StidDuty).ToList();
        var afters = writes.Where(w => w.ShiftTypeId == StidAfterDuty).ToList();
        var subOffs = writes.Where(w => w.ShiftTypeId == StidSubOff).ToList();

        var (first, last) = ShiftBusiness.GetMonthRange(Month);

        // 最終日を除く毎日、カテ可1名+カテ不可1名の当直が立っていること
        for (var day = first; day < last; day = day.AddDays(1))
        {
            var dutiesOnDay = duties.Where(w => w.Date == day).ToList();
            Assert.Equal(2, dutiesOnDay.Count);

            var cathCount = dutiesOnDay.Count(w => employees.First(e => e.EmployeeId == w.EmployeeId).CanDoCatheterization);
            Assert.Equal(1, cathCount);
            Assert.Equal(1, dutiesOnDay.Count - cathCount);
        }

        // 最低3日はあける（同一職員の当直日が4日未満の間隔で連続しない）
        foreach (var group in duties.GroupBy(w => w.EmployeeId))
        {
            var sortedDates = group.Select(w => w.Date).OrderBy(d => d).ToList();
            for (int i = 1; i < sortedDates.Count; i++)
            {
                Assert.True((sortedDates[i] - sortedDates[i - 1]).Days >= 4,
                    $"employee {group.Key}: {sortedDates[i - 1]:yyyy-MM-dd} -> {sortedDates[i]:yyyy-MM-dd}");
            }
        }

        // 当直の翌日は必ず明けが付与されている
        foreach (var duty in duties)
        {
            Assert.Contains(afters, w => w.EmployeeId == duty.EmployeeId && w.Date == duty.Date.AddDays(1));
        }

        // 代休は ShiftBusiness.GetCompWorkOff と同じ日付で付与されている（単一の真実源であることの回帰確認）
        foreach (var duty in duties)
        {
            var expectedCompDate = ShiftBusiness.GetCompWorkOff(duty.Date, holidays);
            if (expectedCompDate is null) continue;

            Assert.Contains(subOffs, w => w.EmployeeId == duty.EmployeeId && w.Date == expectedCompDate.Value);
        }

        // 同一職員・同一日に複数の異なるシフトが重複して書き込まれていないこと
        foreach (var group in writes.GroupBy(w => (w.EmployeeId, w.Date)))
        {
            Assert.Single(group);
        }
    }

    [Fact]
    public void Solve_DayWork_OnlyOnSundayOrHoliday_ForEligibleEmployees()
    {
        var employees = BuildSymmetricEmployees();
        // ShiftBusiness.GetActiveEmployeesWithDayDutyClass同様、日勤対象はカテ不可(CanDoCatheterization=false)の
        // 職員のみのため、非カテ班(index 8以降)からCanDayDutyを付与する
        employees[8].CanDayDuty = true;
        employees[12].CanDayDuty = true;
        var dayWorkEligibleIds = new[] { employees[8].EmployeeId, employees[12].EmployeeId };

        var holidays = new List<DateTime> { new DateTime(2026, 2, 11) }; // 水曜日を祝日にする

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();
        var dayWorks = writes.Where(w => w.ShiftTypeId == StidDayWork).ToList();

        // 適格者がいるのに日勤が一人も割り当てられない、という回帰（目的関数に日勤の動機が
        // 無かったため常に0件になっていたバグ）を検出するための核心のアサーション。
        Assert.NotEmpty(dayWorks);

        Assert.All(dayWorks, w => Assert.True(w.Date.DayOfWeek == DayOfWeek.Sunday || holidays.Contains(w.Date)));
        Assert.All(dayWorks, w => Assert.Contains(w.EmployeeId, dayWorkEligibleIds));

        // 同一職員・同一日の重複（当直/明け/代休との衝突）が無いこと
        foreach (var group in writes.GroupBy(w => (w.EmployeeId, w.Date)))
        {
            Assert.Single(group);
        }
    }

    [Fact]
    public void Solve_DayWork_SkipsEmployeeAlreadyOffOnThatDay()
    {
        var employees = BuildSymmetricEmployees();
        employees[8].CanDayDuty = true; // 日勤対応可能(カテ不可)なのはこの1名のみ
        var blockedDate = new DateTime(2026, 2, 1); // Feb 2026の最初の日曜日

        var existingMap = new Dictionary<(int, DateTime), int>
        {
            [(employees[8].EmployeeId, blockedDate)] = StidOff, // 既に公休が入っている
        };

        var solver = new ShiftsSolver(Month, employees, existingMap, new List<DateTime>(),
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();

        Assert.DoesNotContain(writes, w =>
            w.ShiftTypeId == StidDayWork && w.EmployeeId == employees[8].EmployeeId && w.Date == blockedDate);
    }

    [Fact]
    public void Solve_DayWork_ExcludesCatheterizationCapableEmployees()
    {
        // ShiftBusiness.GetActiveEmployeesWithDayDutyClass同様、カテ可の職員は
        // CanDayDuty=trueであっても日勤対象から除外されることを確認する。
        var employees = BuildSymmetricEmployees();
        employees[0].CanDayDuty = true; // カテ可(index 0)にCanDayDutyを付与しても対象外のはず

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), new List<DateTime>(),
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();

        Assert.DoesNotContain(writes, w => w.ShiftTypeId == StidDayWork && w.EmployeeId == employees[0].EmployeeId);
    }

    [Fact]
    public void Solve_DayWork_NeverAssignsSameEmployeeOnConsecutiveDays()
    {
        // 2026年10月: 10/11(日)の翌日10/12(月)が祝日(体育の日)で、日勤対象日が連続するケース。
        // 日勤対応可能な職員を1名だけにして、同一人物しか候補がいない状況でも
        // 2日連続では割り当てられず、2日目は日勤なしになることを確認する。
        var october = new DateTime(2026, 10, 1);
        var employees = BuildSymmetricEmployees();
        employees[8].CanDayDuty = true; // 日勤対応可能なのはこの1名のみ（非カテ班）
        var dayWorkEligibleId = employees[8].EmployeeId;
        var holidays = new List<DateTime> { new DateTime(2026, 10, 12) };

        var solver = new ShiftsSolver(october, employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();
        var dayWorkDates = writes.Where(w => w.ShiftTypeId == StidDayWork && w.EmployeeId == dayWorkEligibleId)
            .Select(w => w.Date)
            .OrderBy(d => d)
            .ToList();

        foreach (var date in dayWorkDates)
        {
            Assert.DoesNotContain(date.AddDays(1), dayWorkDates);
        }
    }

    [Fact]
    public void Solve_DayWork_LimitedToOncePerEmployeePerMonth()
    {
        // 2026年10月は日曜が4回(4,11,18,25)＋祝日1回(12日)の計5回、日勤対象日がある。
        // 日勤対応可能な職員を1名だけにし、月内で複数回ではなく1回までしか
        // 割り当てられないことを確認する（運用要望：日勤は一人月1回まで）。
        var october = new DateTime(2026, 10, 1);
        var employees = BuildSymmetricEmployees();
        employees[8].CanDayDuty = true; // 日勤対応可能なのはこの1名のみ（非カテ班）
        var dayWorkEligibleId = employees[8].EmployeeId;
        var holidays = new List<DateTime> { new DateTime(2026, 10, 12) };

        var solver = new ShiftsSolver(october, employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();
        var dayWorkCount = writes.Count(w => w.ShiftTypeId == StidDayWork && w.EmployeeId == dayWorkEligibleId);

        Assert.True(dayWorkCount <= 1, $"expected at most 1 day-work assignment, got {dayWorkCount}");
    }

    [Fact]
    public void Solve_AvoidPreference_KeepsEmployeeOffThatWeekdayInOptimalSolution()
    {
        var employees = BuildSymmetricEmployees();
        var avoidEmployeeId = employees.First(e => e.CanDoCatheterization).EmployeeId;

        var preferences = new Dictionary<int, List<EmployeePreference>>
        {
            [avoidEmployeeId] = new List<EmployeePreference>
            {
                new() { EmployeeId = avoidEmployeeId, DayOfWeek = DayOfWeek.Monday, Polarity = PreferencePolarity.Avoid, Weight = 1 },
            },
        };

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), new List<DateTime>(),
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork,
            baselineIsA: false, preferencesByEmployee: preferences);

        var writes = solver.Solve();

        var mondayDutiesForAvoidingEmployee = writes.Where(w =>
            w.ShiftTypeId == StidDuty && w.EmployeeId == avoidEmployeeId && w.Date.DayOfWeek == DayOfWeek.Monday);

        Assert.Empty(mondayDutiesForAvoidingEmployee);
    }

    [Fact]
    public void Solve_ExtremePreferences_NeverThrowsInfeasible()
    {
        var employees = BuildSymmetricEmployees();
        var targetId = employees[0].EmployeeId;

        // 全曜日をAvoidに設定しても、希望はソフト制約(目的関数のペナルティ)のみなので
        // INFEASIBLEにはならず結果を返せることを確認する。
        var prefs = Enum.GetValues<DayOfWeek>()
            .Select(dow => new EmployeePreference { EmployeeId = targetId, DayOfWeek = dow, Polarity = PreferencePolarity.Avoid, Weight = 1000 })
            .ToList();

        var preferences = new Dictionary<int, List<EmployeePreference>> { [targetId] = prefs };

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), new List<DateTime>(),
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork,
            baselineIsA: false, preferencesByEmployee: preferences);

        var exception = Record.Exception(() => solver.Solve());

        Assert.Null(exception);
    }

    [Fact]
    public void Solve_DayWork_StillAssignedAlongsideHeavyPreferences()
    {
        var employees = BuildSymmetricEmployees();
        employees[8].CanDayDuty = true; // 日勤対応可能なのはこの1名のみ（非カテ班）
        var dayWorkEligibleId = employees[8].EmployeeId;
        var holidays = new List<DateTime> { new DateTime(2026, 2, 11) }; // 水曜日を祝日にする

        // 日勤の優先度が公平性/希望のペナルティに埋もれないことを確認するため、
        // 複数の職員にそれぞれ重い希望を設定する。
        var preferences = new Dictionary<int, List<EmployeePreference>>();
        for (int i = 0; i < 6; i++)
        {
            preferences[employees[i].EmployeeId] = Enum.GetValues<DayOfWeek>()
                .Select(dow => new EmployeePreference { EmployeeId = employees[i].EmployeeId, DayOfWeek = dow, Polarity = PreferencePolarity.Avoid, Weight = 5000 })
                .ToList();
        }

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork,
            baselineIsA: false, preferencesByEmployee: preferences);

        var writes = solver.Solve();
        var dayWorks = writes.Where(w => w.ShiftTypeId == StidDayWork).ToList();

        Assert.NotEmpty(dayWorks);
        Assert.All(dayWorks, w => Assert.Equal(dayWorkEligibleId, w.EmployeeId));
    }

    // カテ可/不可それぞれをA班・B班に均等に分けた職員セット（土曜出勤班/翌土曜休み班の
    // 制約テスト用。土曜/金曜はどちらかの班に絞られるため、各カテゴリを両班に用意しておく）。
    private static List<Employee> BuildEmployeesWithSaturdayBands()
    {
        var list = new List<Employee>();
        int id = 1;
        for (int i = 0; i < 4; i++)
            list.Add(new Employee { EmployeeId = id++, EmployeeName = $"CathA{i}", CanDoCatheterization = true, SaturdayClass = "A", CanDoNightDuty = true, CanDayDuty = false });
        for (int i = 0; i < 4; i++)
            list.Add(new Employee { EmployeeId = id++, EmployeeName = $"CathB{i}", CanDoCatheterization = true, SaturdayClass = "B", CanDoNightDuty = true, CanDayDuty = false });
        for (int i = 0; i < 4; i++)
            list.Add(new Employee { EmployeeId = id++, EmployeeName = $"NonCathA{i}", CanDoCatheterization = false, SaturdayClass = "A", CanDoNightDuty = true, CanDayDuty = false });
        for (int i = 0; i < 4; i++)
            list.Add(new Employee { EmployeeId = id++, EmployeeName = $"NonCathB{i}", CanDoCatheterization = false, SaturdayClass = "B", CanDoNightDuty = true, CanDayDuty = false });
        return list;
    }

    [Fact]
    public void Solve_SaturdayDuty_OnlyFromWorkingBand_AndFridayDuty_OnlyFromOffBand()
    {
        var employees = BuildEmployeesWithSaturdayBands();
        var holidays = new List<DateTime>();

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();
        var duties = writes.Where(w => w.ShiftTypeId == StidDuty).ToList();
        var empById = employees.ToDictionary(e => e.EmployeeId);

        var saturdayDuties = duties.Where(d => d.Date.DayOfWeek == DayOfWeek.Saturday).ToList();
        var fridayDuties = duties.Where(d => d.Date.DayOfWeek == DayOfWeek.Friday).ToList();
        Assert.NotEmpty(saturdayDuties);
        Assert.NotEmpty(fridayDuties);

        foreach (var duty in saturdayDuties)
        {
            var workingClass = ShiftBusiness.GetWorkingClass(duty.Date, baselineIsA: false);
            Assert.Equal(workingClass, empById[duty.EmployeeId].SaturdayClass);
        }

        foreach (var duty in fridayDuties)
        {
            var workingClassNextSat = ShiftBusiness.GetWorkingClass(duty.Date.AddDays(1), baselineIsA: false);
            Assert.NotEqual(workingClassNextSat, empById[duty.EmployeeId].SaturdayClass);
        }
    }

    [Fact]
    public void Solve_FridayDuty_NeverGetsDoubledCompOff()
    {
        var employees = BuildEmployeesWithSaturdayBands();
        var holidays = new List<DateTime>();

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();
        var fridayDuties = writes.Where(w => w.ShiftTypeId == StidDuty && w.Date.DayOfWeek == DayOfWeek.Friday).ToList();
        Assert.NotEmpty(fridayDuties);

        // 金曜当直は「翌土曜が休みの班」から選ぶよう既に固定しているため、明けが休みであることを
        // 理由にした追加代休(二重付与)は起きないはず（1件以下、通常の代休が同月内に取れない
        // 月末近くの金曜だけは0件になり得るが、2件になることは無い）。
        foreach (var duty in fridayDuties)
        {
            var compOffCount = writes.Count(w =>
                w.ShiftTypeId == StidSubOff && w.EmployeeId == duty.EmployeeId && w.originDutyDate == duty.Date);
            Assert.True(compOffCount <= 1, $"eid={duty.EmployeeId} date={duty.Date:yyyy-MM-dd} compOffCount={compOffCount}");
        }

        // 月末に近すぎない金曜（同月内に必ず通常の代休が取れるはず）については、
        // 通常の代休(次の水曜)がきちんと1件付与されていることも確認する。
        var earlyFridayDuty = fridayDuties.OrderBy(d => d.Date).First();
        var earlyCompOffCount = writes.Count(w =>
            w.ShiftTypeId == StidSubOff && w.EmployeeId == earlyFridayDuty.EmployeeId && w.originDutyDate == earlyFridayDuty.Date);
        Assert.Equal(1, earlyCompOffCount);
    }

    [Fact]
    public void Solve_SaturdayDuty_NeverGetsExtraCompOff()
    {
        // 土曜当直（明けは常に日曜）は「当直日が日曜/祝日」という条件を満たさないため、
        // 二重代休の対象外（通常の代休1件のみ）であることを確認する。
        var employees = BuildSymmetricEmployees();
        var holidays = new List<DateTime>();

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();

        var (first, _) = ShiftBusiness.GetMonthRange(Month);
        var firstSaturday = Enumerable.Range(0, 7)
            .Select(i => first.AddDays(i))
            .First(d => d.DayOfWeek == DayOfWeek.Saturday);

        var dutiesOnSaturday = writes.Where(w => w.ShiftTypeId == StidDuty && w.Date == firstSaturday).ToList();
        Assert.Equal(2, dutiesOnSaturday.Count);

        var primary = ShiftBusiness.GetCompWorkOff(firstSaturday, holidays);
        Assert.NotNull(primary);

        foreach (var duty in dutiesOnSaturday)
        {
            var compOffs = writes.Where(w =>
                w.ShiftTypeId == StidSubOff && w.EmployeeId == duty.EmployeeId && w.originDutyDate == duty.Date).ToList();

            Assert.Single(compOffs);
            Assert.Equal(primary!.Value, compOffs[0].Date);
        }
    }

    [Fact]
    public void Solve_SundayDuty_WithHolidayAke_GrantsExtraCompOff()
    {
        // シルバーウィークのように「日曜当直の明け(月)が祝日」となるケースでは、
        // 通常の代休とは別にもう1日代休が確保されることを確認する。
        var employees = BuildSymmetricEmployees();
        var (first, _) = ShiftBusiness.GetMonthRange(Month);
        var firstSunday = Enumerable.Range(0, 7)
            .Select(i => first.AddDays(i))
            .First(d => d.DayOfWeek == DayOfWeek.Sunday);
        var monday = firstSunday.AddDays(1);
        var holidays = new List<DateTime> { monday };

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();

        var dutiesOnSunday = writes.Where(w => w.ShiftTypeId == StidDuty && w.Date == firstSunday).ToList();
        Assert.Equal(2, dutiesOnSunday.Count);

        var primary = ShiftBusiness.GetCompWorkOff(firstSunday, holidays);
        var extra = ShiftBusiness.GetExtraCompWorkOffForRestfulAke(firstSunday, primary, holidays);
        Assert.NotNull(extra);

        foreach (var duty in dutiesOnSunday)
        {
            var compOffs = writes.Where(w =>
                w.ShiftTypeId == StidSubOff && w.EmployeeId == duty.EmployeeId && w.originDutyDate == duty.Date).ToList();

            Assert.Equal(2, compOffs.Count);
            Assert.Contains(compOffs, c => c.Date == extra!.Value);
        }
    }
}
