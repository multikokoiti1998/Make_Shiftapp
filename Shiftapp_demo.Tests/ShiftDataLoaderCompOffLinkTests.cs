using Shiftapp_demo.Business;
using Xunit;

namespace Shiftapp_demo.Tests
{
    public class ShiftDataLoaderCompOffLinkTests
    {
        private static ShiftDataLoader CreateOctober2026(List<(DateTime DutyDate, string Symbol)>? previousMonthDuties = null)
        {
            var loader = new ShiftDataLoader { EmployeeId = 1, EmployeeName = "テスト 一郎" };
            var first = new DateTime(2026, 10, 1);
            for (var d = first; d.Month == 10; d = d.AddDays(1))
                loader[d.ToString("yyyy-MM-dd")] = "";

            var sundays = Enumerable.Range(0, 31)
                .Select(i => first.AddDays(i))
                .Where(d => d.DayOfWeek == DayOfWeek.Sunday)
                .Select(d => d.ToString("yyyy-MM-dd"));

            loader.SetAttentionContext(
                new HashSet<string>(sundays),
                new List<DateTime>(),
                new Dictionary<string, (string, DateTime?)>(),
                previousMonthDuties ?? new List<(DateTime, string)>(),
                new Dictionary<string, (string, DateTime?)>());
            loader.EnableCascade();
            return loader;
        }

        [Fact]
        public void 当直より前の日に手入力した代休を当直に紐づけられる()
        {
            var loader = CreateOctober2026();
            var duty = new DateTime(2026, 10, 17); // 土曜当直

            loader["2026-10-17"] = "当";

            // 自動で後ろに付いた代休を消し、代わりに当直より前の日（水曜）に代休を手入力する
            var autoCompOff = loader.CompOffOrigin.Single(kv => kv.Value == duty && loader[kv.Key] == "●").Key;
            loader[autoCompOff] = "";
            Assert.True(loader.NeedsAttention);

            List<(DateTime DutyDate, string Symbol)>? offered = null;
            loader.RequestCompOffLinkSelection = c => { offered = c; return c[0].DutyDate; };

            loader["2026-10-14"] = "●";

            Assert.NotNull(offered);
            Assert.Contains(offered!, c => c.DutyDate == duty);
            Assert.Equal(duty, loader.CompOffOrigin["2026-10-14"]);
            Assert.False(loader.NeedsAttention);
        }

        [Fact]
        public void 前月の当直は代休の紐づけ候補にしない()
        {
            // 前月末の土曜当直（代休未付与）があっても、今月の手入力代休の候補には出さない
            var loader = CreateOctober2026(new List<(DateTime, string)> { (new DateTime(2026, 9, 26), "当") });
            loader["2026-10-10"] = "当";
            var autoCompOff = loader.CompOffOrigin.Single(kv => loader[kv.Key] == "●").Key;
            loader[autoCompOff] = "";

            List<(DateTime DutyDate, string Symbol)>? offered = null;
            loader.RequestCompOffLinkSelection = c => { offered = c; return null; };

            loader["2026-10-07"] = "●";

            Assert.NotNull(offered);
            Assert.All(offered!, c => Assert.Equal(10, c.DutyDate.Month));
        }
    }
}
