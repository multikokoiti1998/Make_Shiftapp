using Shiftapp_demo.Business;
using Shiftapp_demo.Models;
using Xunit;

namespace Shiftapp_demo.Tests;

// 実際の本番DBから取得した実データ構成(2026年9月、シルバーウィークで祝日が月火水と3連続)で
// 発生した「シフト生成不可: 条件が厳しすぎます」の回帰テスト。
// 原因: UpdateSundayShifts が GetHolidaysInMonth(当月+翌月の2ヶ月分)をそのまま使っていたため、
// 8月分を生成しただけで9月の祝日(9/21,22,23)にも全職員へ○が書き込まれてしまい、
// その後9月分を生成しようとすると全員が既存○でブロックされ、当直必須(=1)制約と矛盾してINFEASIBLEになっていた。
public class ShiftsSolverRealDataRegressionTests
{
    private const int StidDuty = 1;
    private const int StidAfterDuty = 2;
    private const int StidSubOff = 3;
    private const int StidDayWork = 0;
    private const int StidOff = 4;

    private static List<Employee> RealRoster() => new()
    {
        new Employee{EmployeeId=29056, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="B"},
        new Employee{EmployeeId=33473, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="B"},
        new Employee{EmployeeId=33485, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="A"},
        new Employee{EmployeeId=35665, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="A"},
        new Employee{EmployeeId=37584, CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=37601, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=39805, CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=42503, CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=46963, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="B"},
        new Employee{EmployeeId=51774, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="B"},
        new Employee{EmployeeId=52687, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="A"},
        new Employee{EmployeeId=56712, CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=62993, CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=79269, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="B"},
        new Employee{EmployeeId=88014, CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=88777, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="B"},
        new Employee{EmployeeId=94908, CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=97962, CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=97974, CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=103814,CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=109272,CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="B"},
        new Employee{EmployeeId=109997,CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=112499,CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=114863,CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=118857,CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=118869,CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=122339,CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=125642,CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=130415,CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=130439,CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=130441,CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=130831,CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=131603,CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=138041,CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=138053,CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="B"},
        new Employee{EmployeeId=142042,CanDoNightDuty=true,  CanDoCatheterization=true,  CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=145410,CanDoNightDuty=true,  CanDoCatheterization=false, CanDayDuty=true,  SaturdayClass="A"},
        new Employee{EmployeeId=147802,CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="B"},
        new Employee{EmployeeId=174614,CanDoNightDuty=false, CanDoCatheterization=false, CanDayDuty=false, SaturdayClass="A"},
    };

    [Fact]
    public void Sept2026_WithPollutedOffOnAllEmployees_ThrowsInfeasible_ThisWasTheBug()
    {
        var employees = RealRoster();
        var holidays = new List<DateTime> { new(2026, 9, 21), new(2026, 9, 22), new(2026, 9, 23) };

        // UpdateSundayShifts の2ヶ月漏れバグにより、8月分生成の副作用として
        // 9/21,22,23 に「全職員」○が書き込まれてしまっていた状態を再現する。
        var existingMap = new Dictionary<(int, DateTime), int>();
        foreach (var h in holidays)
            foreach (var e in employees)
                existingMap[(e.EmployeeId, h)] = StidOff;

        var solver = new ShiftsSolver(new DateTime(2026, 9, 1), employees, existingMap, holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var ex = Assert.Throws<InvalidOperationException>(() => solver.Solve());
        Assert.Contains("条件が厳しすぎます", ex.Message);
    }

    [Fact]
    public void Sept2026_WithoutPollution_SucceedsAndAssignsDayWork()
    {
        // DeleteMonthDutyAndDayParentsWithCascade を拡張し、○/出勤"/" も当月分は
        // 再生成前にクリアするようにした修正後の状態（=汚染された○が存在しない）を再現する。
        var employees = RealRoster();
        var holidays = new List<DateTime> { new(2026, 9, 21), new(2026, 9, 22), new(2026, 9, 23) };

        var solver = new ShiftsSolver(new DateTime(2026, 9, 1), employees, new Dictionary<(int, DateTime), int>(), holidays,
            StidDuty, StidAfterDuty, StidSubOff, StidOff, StidDayWork);

        var writes = solver.Solve();

        var dayWorks = writes.Where(w => w.ShiftTypeId == StidDayWork).ToList();
        Assert.NotEmpty(dayWorks);

        var (first, last) = ShiftBusiness.GetMonthRange(new DateTime(2026, 9, 1));
        var duties = writes.Where(w => w.ShiftTypeId == StidDuty).ToList();
        for (var day = first; day < last; day = day.AddDays(1))
        {
            Assert.Equal(2, duties.Count(w => w.Date == day));
        }
    }
}
