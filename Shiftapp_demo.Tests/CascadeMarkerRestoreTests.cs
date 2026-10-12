using Microsoft.Data.Sqlite;
using Shiftapp_demo.Business;
using Shiftapp_demo.DataAccess;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Shiftapp_demo.Tests
{
    public class CascadeMarkerRestoreTests
    {
        [Theory]
        [InlineData("2026-11-01", false, "A", false, "●")]  // 日曜
        [InlineData("2026-11-03", true, "A", false, "●")]   // 祝日（平日）
        [InlineData("2026-11-02", false, "A", false, null)] // 平日
        [InlineData("2026-11-02", false, "A", true, "短")]  // 平日・時短勤務
        [InlineData("2026-11-07", true, "A", false, "●")]   // 土曜の祝日
        [InlineData("2026-11-07", false, "", false, null)]  // 土曜・班未設定
        public void GetDefaultMarkerSymbol_月作成時と同じ既定記号(string date, bool isHoliday, string satClass, bool isShortTime, string? expected)
        {
            Assert.Equal(expected, ShiftBusiness.GetDefaultMarkerSymbol(DateTime.Parse(date), isHoliday, satClass, isShortTime, baselineIsA: false));
        }

        [Fact]
        public void GetDefaultMarkerSymbol_土曜は出勤班なら出勤_休み班なら丸()
        {
            var sat = new DateTime(2026, 11, 7);
            var working = ShiftBusiness.GetWorkingClass(sat, baselineIsA: false);
            var resting = working == "A" ? "B" : "A";

            Assert.Equal("/", ShiftBusiness.GetDefaultMarkerSymbol(sat, false, working, false, baselineIsA: false));
            Assert.Equal("○", ShiftBusiness.GetDefaultMarkerSymbol(sat, false, resting, false, baselineIsA: false));
        }

        [Fact]
        public void 月の作り直しで連鎖削除された翌月の日曜に丸黒を入れ直す()
        {
            var path = Path.Combine(Path.GetTempPath(), $"shift_regen_{Guid.NewGuid():N}.db");
            var cs = $"Data Source={path};Pooling=False";
            try
            {
                using (var con = new SqliteConnection(cs))
                {
                    con.Open();
                    Exec(con, @"
                    PRAGMA foreign_keys=ON;
                    CREATE TABLE employee (employee_id INTEGER PRIMARY KEY, saturday_class TEXT, IsShortTime INTEGER NOT NULL DEFAULT 0);
                    CREATE TABLE shift_types (shift_type_id INTEGER PRIMARY KEY, symbol TEXT);
                    CREATE TABLE holiday (id INTEGER PRIMARY KEY AUTOINCREMENT, date TEXT NOT NULL UNIQUE, name TEXT);
                    CREATE TABLE daily_employee_shifts (
                      shifts_id INTEGER PRIMARY KEY, employee_id INTEGER NOT NULL, shift_date TEXT NOT NULL,
                      shift_type_id INTEGER NOT NULL, registered_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                      origin_shifts_id INTEGER,
                      FOREIGN KEY (origin_shifts_id) REFERENCES daily_employee_shifts(shifts_id) ON DELETE CASCADE);
                    CREATE UNIQUE INDEX ux_emp_date ON daily_employee_shifts(employee_id, shift_date);
                    INSERT INTO shift_types VALUES (0,'日'),(1,'当'),(2,'明'),(3,'●'),(4,'○'),(5,'/'),(7,'短');
                    INSERT INTO holiday (date, name) VALUES ('2026-11-03','文化の日');
                    INSERT INTO employee VALUES (1,'A',0);
                    -- 10/31(土)当直 → 11/1(日)明け・11/2(月)代休（いずれも翌月）
                    INSERT INTO daily_employee_shifts (shifts_id, employee_id, shift_date, shift_type_id, origin_shifts_id) VALUES
                      (10, 1, '2026-10-31', 1, NULL),
                      (11, 1, '2026-11-01', 2, 10),
                      (12, 1, '2026-11-02', 3, 10),
                      (13, 1, '2026-11-03', 3, NULL);");
                }

                var db = (MainDatabaseHelper)RuntimeHelpers.GetUninitializedObject(typeof(MainDatabaseHelper));
                typeof(BaseDatabaseHelper).GetField("_connectionString", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(db, cs);

                using (var con = db.OpenConnection())
                using (var tx = con.BeginTransaction())
                {
                    db.DeleteMonthDutyAndDayParentsWithCascade(con, tx, new DateTime(2026, 10, 1));
                    tx.Commit();
                }

                using (var con = new SqliteConnection(cs))
                {
                    con.Open();
                    Assert.Null(SymbolAt(con, "2026-10-31"));  // 当月の当直は作り直しのため削除
                    Assert.Equal("●", SymbolAt(con, "2026-11-01")); // 日曜の明け → 日曜の●に戻る
                    Assert.Null(SymbolAt(con, "2026-11-02"));  // 平日の代休 → 空（出勤）
                    Assert.Equal("●", SymbolAt(con, "2026-11-03")); // 元からある祝日の●はそのまま
                }
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                File.Delete(path);
            }
        }

        private static void Exec(SqliteConnection con, string sql)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static string? SymbolAt(SqliteConnection con, string date)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT t.symbol FROM daily_employee_shifts d JOIN shift_types t USING(shift_type_id)
                                WHERE d.employee_id = 1 AND d.shift_date = @d;";
            cmd.Parameters.AddWithValue("@d", date);
            return cmd.ExecuteScalar() as string;
        }
    }
}
