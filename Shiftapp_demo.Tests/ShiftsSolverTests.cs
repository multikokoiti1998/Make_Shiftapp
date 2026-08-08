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
        employees[0].CanDayDuty = true;
        employees[4].CanDayDuty = true;
        var dayWorkEligibleIds = new[] { employees[0].EmployeeId, employees[4].EmployeeId };

        var holidays = new List<DateTime> { new DateTime(2026, 2, 11) }; // 水曜日を祝日にする

        var solver = new ShiftsSolver(Month, employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();
        var dayWorks = writes.Where(w => w.ShiftTypeId == StidDayWork).ToList();

        Assert.All(dayWorks, w => Assert.True(w.Date.DayOfWeek == DayOfWeek.Sunday || holidays.Contains(w.Date)));
        Assert.All(dayWorks, w => Assert.Contains(w.EmployeeId, dayWorkEligibleIds));

        // 同一職員・同一日の重複（当直/明け/代休との衝突）が無いこと
        foreach (var group in writes.GroupBy(w => (w.EmployeeId, w.Date)))
        {
            Assert.Single(group);
        }
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
}
