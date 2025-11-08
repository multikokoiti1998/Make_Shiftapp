using Microsoft.Data.Sqlite;
using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.IO;

namespace Shiftapp_demo.DataAccess
{
    public class DatabaseHelper
    {
        private string _connectionString;

        public DatabaseHelper()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dbPath = Path.Combine(baseDir, "Data", "shiftapp.db");
            _connectionString = $"Data Source={dbPath}";

            //_connectionString = $"Data Source=C:\\Users\\user\\Source\\Repos\\Shiftapp_demo\\Shiftapp_demo\\Data\\shiftapp.db";
        }


        // 技師を削除するメソッド
        public void DeleteEmployee(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM employee WHERE employee_id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }

        public List<Employee> GetAllEmployees()
        {
            var employees = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, employee_name, Role, is_active
            FROM employee
            WHERE is_active = 1
            ORDER BY employee_id";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                employees.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),
                    EmployeeName = reader.GetString(1),
                    Role = reader.GetInt32(2)
                });
            }
            return employees;
        }

        public List<ShiftRow> GetShiftRow(DateTime startDate, DateTime endDate)
        {
            var result = new List<ShiftRow>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"            
            SELECT
            b.employee_id,
            e.employee_name,
            b.shift_date,
            t.symbol,
            e.Role
            FROM daily_employee_shifts b
            JOIN employee e
              ON e.employee_id = b.employee_id
             AND e.is_active = 1
            LEFT JOIN shift_types t
              ON t.shift_type_id = b.shift_type_id
            WHERE DATE(b.shift_date) >= DATE(@start)
              AND DATE(b.shift_date) <  DATE(@next)
            ORDER BY e.Role,b.employee_id, b.shift_date;";

            var next = endDate.AddDays(1);
            cmd.Parameters.AddWithValue("@start", startDate.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@next", next.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@stidDaikyu", GetShiftTypeIdBySymbol("●"));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new ShiftRow
                {
                    EmployeeId = reader.GetInt32(0),

                    EmployeeName = reader.GetString(1),

                    Date = DateTime.Parse(reader.GetString(2)).Date,

                    ShiftSymbol = reader.GetString(3),

                    Role = reader.GetInt32(4)
                });
            }
            return result;
        }

        public List<Shift> GetShiftsOnly(DateTime startDate, DateTime endDate)
        {
            var result = new List<Shift>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT
              b.employee_id,
              b.shift_date,
              COALESCE(t.symbol, '') AS symbol,       
              b.shift_type_id         AS final_shift_type_id
            FROM daily_employee_shifts b
            JOIN employee e
              ON e.employee_id = b.employee_id AND e.is_active = 1
            LEFT JOIN shift_types t
              ON t.shift_type_id = b.shift_type_id
            WHERE DATE(b.shift_date) >= DATE(@start)
              AND DATE(b.shift_date) <  DATE(@next)
            ORDER BY b.employee_id, b.shift_date;";

            var next = endDate.AddDays(1);

            cmd.Parameters.AddWithValue("@start", startDate.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@next", next.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@stidDuty", GetShiftTypeIdBySymbol("当"));
            cmd.Parameters.AddWithValue("@stidDayDuty", GetShiftTypeIdBySymbol("日"));
            cmd.Parameters.AddWithValue("@stidSatWork", GetShiftTypeIdBySymbol("/"));
            cmd.Parameters.AddWithValue("@stidAke", GetShiftTypeIdBySymbol("明"));
            cmd.Parameters.AddWithValue("@stidDaikyu", GetShiftTypeIdBySymbol("●"));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Shift
                {
                    EmployeeId = reader.GetInt32(0),

                    ShiftDate = DateTime.Parse(reader.GetString(1)).Date,

                    Symbol = reader.IsDBNull(2) ? "" : reader.GetString(2)
                });
            }
            return result;
        }

        //各技師の土曜日の班を取得
        public List<Employee> GetActiveEmployeesWithSaturdayClass()
        {
            var result = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, saturday_class
            FROM employee 
            WHERE is_active = 1";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),

                    SaturdayClass = reader.GetString(1)
                });
            }
            return result;
        }

        //各シフトのシンボル取得
        public int GetShiftTypeIdBySymbol(string symbol)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT shift_type_id FROM shift_types WHERE symbol = @sym;";
            cmd.Parameters.AddWithValue("@sym", symbol);
            var obj = cmd.ExecuteScalar();
            if (obj == null || obj == DBNull.Value) throw new InvalidOperationException($"symbol '{symbol}' not found");
            return Convert.ToInt32(obj);
        }

        public Dictionary<(int EmployeeId, DateTime Date), int> GetShiftMap(DateTime start, DateTime end)
        {
            var map = new Dictionary<(int, DateTime), int>();

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            SELECT
              b.employee_id,
              b.shift_date,
              b.shift_type_id
            FROM daily_employee_shifts b
            JOIN employee e ON e.employee_id = b.employee_id AND e.is_active = 1
            WHERE DATE(b.shift_date) >= DATE(@start)
              AND DATE(b.shift_date) <  DATE(@next)
            ORDER BY b.employee_id, b.shift_date;";

            var next = end.Date.AddDays(1);
            cmd.Parameters.AddWithValue("@start", start.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@next", next.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@stidDuty", 1);
            cmd.Parameters.AddWithValue("@stidDayDuty", 0);
            cmd.Parameters.AddWithValue("@stidAke", 5);
            cmd.Parameters.AddWithValue("@stidDaikyu", 2);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int eid = r.GetInt32(0);
                var date = DateTime.Parse(r.GetString(1));
                int stid = r.GetInt32(2);
                map[(eid, date.Date)] = stid;
            }
            return map;
        }
        public void DeleteMonthDutyAndDayParentsWithCascade(DateTime monthFirst, int stidDuty, int stidDay)
        {
            var first = new DateTime(monthFirst.Year, monthFirst.Month, 1);
            var next = first.AddMonths(1);

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var tx = con.BeginTransaction();
            using (var pragma = con.CreateCommand())
            {
                pragma.Transaction = tx;
                pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
                pragma.ExecuteNonQuery();
            }

            using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            // 共通パラメータ
            cmd.Parameters.AddWithValue("@first", first.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@next", next.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@sidDuty", stidDuty);
            cmd.Parameters.AddWithValue("@sidDay", stidDay);
            cmd.Parameters.AddWithValue("@stidSatWork", 5);
            cmd.Parameters.AddWithValue("@stidoff", 4);

            // 1) 親のシンプル削除：origin_shifts_id IS NULL AND type IN (当/日)
            cmd.CommandText = @"
            DELETE FROM daily_employee_shifts
            WHERE origin_shifts_id IS NULL
              AND shift_type_id IN (@sidDuty, @sidDay,@stidSatWork, @stidoff)
              AND shift_date >= @first
              AND shift_date <  @next;";
            cmd.ExecuteNonQuery();

            //2) 当月内の孤児（親が存在しない子）を削除
            cmd.CommandText = @"
             DELETE FROM daily_employee_shifts AS c
              WHERE c.shift_date >= @first AND c.shift_date < @next
              AND c.origin_shifts_id IS NULL
              AND c.shift_type_id IN (@sidDuty, @sidDay);";
            cmd.ExecuteNonQuery();

            tx.Commit();
        }

        /// <summary>
        /// シフト種別マスタを全件取得（priority は無視）
        /// </summary>
        public IReadOnlyList<ShiftTypeM> GetShiftTypeMaster()
        {
            var list = new List<ShiftTypeM>();

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            SELECT shift_type_id, symbol, type_name
            FROM shift_types
            ORDER BY shift_type_id;";

            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new ShiftTypeM
                {
                    ShiftTypeId = rd.GetInt32(0),
                    Symbol = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Name = rd.IsDBNull(2) ? "" : rd.GetString(2),
                });
            }
            return list;
        }

        public void DeleteOrphanNightChildren(DateTime start, DateTime end, int stidDuty, int stidAke, int stidSubOff)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using (var pragma = con.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
                pragma.ExecuteNonQuery();
            }

            using var tx = con.BeginTransaction();
            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                DELETE FROM daily_employee_shifts AS c
                WHERE c.shift_date >= @start AND c.shift_date <= @end
                  AND c.shift_type_id IN (@stidAke, @stidSubOff)
                  AND NOT EXISTS (
                        SELECT 1
                        FROM daily_employee_shifts AS p
                        WHERE p.shifts_id     = c.origin_shifts_id
                          AND p.shift_type_id = @stidDuty
                );";
                cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@stidAke", stidAke);
                cmd.Parameters.AddWithValue("@stidSubOff", stidSubOff);
                cmd.Parameters.AddWithValue("@stidDuty", stidDuty);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        // 土日のデフォルト登録
        public void BulkUpsertShifts(IEnumerable<(int EmployeeId, DateTime Date, int ShiftTypeId)> items, DateTime month)
        {
            var monthFirst = new DateTime(month.Year, month.Month, 1);
            var raw = new Raw(
              GetShiftTypeIdBySymbol("当"),
              GetShiftTypeIdBySymbol("明"),
              GetShiftTypeIdBySymbol("●"),
              GetShiftTypeIdBySymbol("日")
          );


            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var tx = con.BeginTransaction();

            using var cmd = con.CreateCommand();

            cmd.Transaction = tx;
            cmd.CommandText = @"
            INSERT INTO daily_employee_shifts (employee_id, shift_date, shift_type_id, registered_at)
            VALUES (@eid, @date, @stid, CURRENT_TIMESTAMP)
            ON CONFLICT(employee_id, shift_date) DO UPDATE SET
              shift_type_id = excluded.shift_type_id,
              registered_at = CURRENT_TIMESTAMP
            WHERE 
                daily_employee_shifts.shift_type_id NOT IN (@stidAke, @stidDaikyu)
                AND daily_employee_shifts.shift_type_id IS NOT excluded.shift_type_id;";
            var pEid = cmd.CreateParameter(); pEid.ParameterName = "@eid"; cmd.Parameters.Add(pEid);
            var pDate = cmd.CreateParameter(); pDate.ParameterName = "@date"; cmd.Parameters.Add(pDate);
            var pSid = cmd.CreateParameter(); pSid.ParameterName = "@stid"; cmd.Parameters.Add(pSid);
            cmd.Parameters.AddWithValue("@stidAke", raw.StidAke);
            cmd.Parameters.AddWithValue("@stidDaikyu", raw.StidDai);

            foreach (var (eid, d, stid) in items)
            {
                pEid.Value = eid;
                pDate.Value = d.ToString("yyyy-MM-dd");
                pSid.Value = stid;
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        public record ShiftWrite(
         int EmployeeId,
         DateTime Date,
         int ShiftTypeId,
         long? originShiftId = null,
         DateTime? originDutyDate = null
             );


        public record Raw(
           int StidDuty,
           int StidAke,
           int StidDai,
           int StidDayDuty
           );


        // 当直日勤や代休の登録
        public void BulkUpsert_Duty_Shifts(IEnumerable<ShiftWrite> items, DateTime month)
        {
            using var con = new SqliteConnection($"{_connectionString};Foreign Keys=True;");
            con.Open();
            using var tx = con.BeginTransaction();

            var raw = new Raw(
                GetShiftTypeIdBySymbol("当"),
                GetShiftTypeIdBySymbol("明"),
                GetShiftTypeIdBySymbol("●"),
                GetShiftTypeIdBySymbol("日")
            );

            // 対象月（itemsは単月想定）
            var monthFirst = new DateTime(month.Year, month.Month, 1);

            // ★ 当月の「親：当直・日勤」だけ削除（子はCASCADEで自動削除）
            //DeleteMonthDutyAndDayParentsWithCascade(con, tx, monthFirst, raw.StidDuty, raw.StidDayDuty);

            var parentsDuty = items.Where(r => r.ShiftTypeId == raw.StidDuty).ToList();   // 親：当
            var parentsDay = items.Where(r => r.ShiftTypeId == raw.StidDayDuty).ToList(); // 親：日
            var children = items.Where(r => r.ShiftTypeId != raw.StidDuty && r.ShiftTypeId != raw.StidDayDuty).ToList();

            var dutyIdMap = new Dictionary<(int, DateTime), long>(); // 当の親ID
            var dayDutyIdMap = new Dictionary<(int, DateTime), long>(); // 日の親ID

            // 1) 親：当直をUpsertし shifts_id を取得
            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                INSERT INTO daily_employee_shifts
                  (employee_id, shift_date, shift_type_id, registered_at, origin_shifts_id)
                VALUES (@eid,@date,@sid,CURRENT_TIMESTAMP,NULL)
                ON CONFLICT(employee_id, shift_date) DO UPDATE SET
                  shift_type_id    = excluded.shift_type_id,
                  registered_at    = CURRENT_TIMESTAMP,
                  origin_shifts_id = NULL
                RETURNING shifts_id;";

                var pEid = cmd.CreateParameter(); pEid.ParameterName = "@eid"; cmd.Parameters.Add(pEid);
                var pDate = cmd.CreateParameter(); pDate.ParameterName = "@date"; cmd.Parameters.Add(pDate);
                var pSid = cmd.CreateParameter(); pSid.ParameterName = "@sid"; cmd.Parameters.Add(pSid);

                foreach (var r in parentsDuty)
                {
                    pEid.Value = r.EmployeeId;
                    pDate.Value = r.Date.ToString("yyyy-MM-dd");
                    pSid.Value = raw.StidDuty;
                    var obj = cmd.ExecuteScalar();
                    var id = (obj is long l) ? l : throw new InvalidOperationException("RETURNING failed for duty parent.");
                    dutyIdMap[(r.EmployeeId, r.Date.Date)] = id;
                }
            }

            // 1') 親（日）をUPSERTしてID取得（明けは付けないが代休の親になり得る）
            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                INSERT INTO daily_employee_shifts
                  (employee_id, shift_date, shift_type_id, registered_at, origin_shifts_id)
                VALUES (@eid,@date,@sid,CURRENT_TIMESTAMP,NULL)
                ON CONFLICT(employee_id, shift_date) DO UPDATE SET
                  shift_type_id    = excluded.shift_type_id,
                  registered_at    = CURRENT_TIMESTAMP,
                  origin_shifts_id = NULL
                RETURNING shifts_id;";

                var pEid = cmd.CreateParameter(); pEid.ParameterName = "@eid"; cmd.Parameters.Add(pEid);
                var pDate = cmd.CreateParameter(); pDate.ParameterName = "@date"; cmd.Parameters.Add(pDate);
                var pSid = cmd.CreateParameter(); pSid.ParameterName = "@sid"; cmd.Parameters.Add(pSid);

                foreach (var r in parentsDay)
                {
                    pEid.Value = r.EmployeeId;
                    pDate.Value = r.Date.ToString("yyyy-MM-dd");
                    pSid.Value = raw.StidDayDuty;
                    var obj = cmd.ExecuteScalar();
                    var id = (obj is long l) ? l : throw new InvalidOperationException("RETURNING failed for day parent.");
                    dayDutyIdMap[(r.EmployeeId, r.Date.Date)] = id;
                }
            }

            // 2) 子（明/代）をUPSERT。明は当親のみ、代は当 or 日 のどちらか
            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                INSERT INTO daily_employee_shifts
                  (employee_id, shift_date, shift_type_id, registered_at, origin_shifts_id)
                VALUES (@eid,@date,@sid,CURRENT_TIMESTAMP,@origin)
                ON CONFLICT(employee_id, shift_date) DO UPDATE SET
                  shift_type_id    = excluded.shift_type_id,
                  registered_at    = CURRENT_TIMESTAMP,
                  origin_shifts_id = excluded.origin_shifts_id;";

                var pEid = cmd.CreateParameter(); pEid.ParameterName = "@eid"; cmd.Parameters.Add(pEid);
                var pDate = cmd.CreateParameter(); pDate.ParameterName = "@date"; cmd.Parameters.Add(pDate);
                var pSid = cmd.CreateParameter(); pSid.ParameterName = "@sid"; cmd.Parameters.Add(pSid);
                var pOrigin = cmd.CreateParameter(); pOrigin.ParameterName = "@origin"; cmd.Parameters.Add(pOrigin);

                foreach (var r in children)
                {
                    long? originId = r.originShiftId;

                    // 明：当直の親しか持てない
                    if (r.ShiftTypeId == raw.StidAke)
                    {
                        // originDutyDate が来ていれば dutyIdMap を優先
                        if (originId is null)
                        {
                            var od = (r.originDutyDate ?? r.Date.AddDays(-1)).Date;  // 既定は前日
                            long found = 0;
                            if (!dutyIdMap.TryGetValue((r.EmployeeId, od.Date), out found))
                            {
                                // DBから当直親だけ検索
                                using var look = con.CreateCommand();
                                look.Transaction = tx;
                                look.CommandText = @"
                                SELECT shifts_id
                                FROM daily_employee_shifts
                                WHERE employee_id=@eid AND shift_date=@date AND shift_type_id=@sid
                                LIMIT 1;";
                                look.Parameters.AddWithValue("@eid", r.EmployeeId);
                                look.Parameters.AddWithValue("@date", od.ToString("yyyy-MM-dd"));
                                look.Parameters.AddWithValue("@sid", raw.StidDuty);
                                var obj = look.ExecuteScalar();
                                if (obj != null && obj != DBNull.Value) found = Convert.ToInt64(obj);
                            }
                            originId = (originId ?? (long?)found);
                        }
                        if (originId is null) continue; // 当親がなければ明は作らない
                    }
                    else if (r.ShiftTypeId == raw.StidDai)
                    {
                        // 代休：当 or 日 のどちらかを親にできる
                        if (originId is null)
                        {
                            if (r.originDutyDate is not DateTime odRaw) { continue; }

                            var od = odRaw.Date;

                            long found = 0;
                            // まず当直親を探す
                            if (!dutyIdMap.TryGetValue((r.EmployeeId, od.Date), out found))
                            {
                                using var look = con.CreateCommand();
                                look.Transaction = tx;
                                look.CommandText = @"
                                SELECT shifts_id
                                FROM daily_employee_shifts
                                WHERE employee_id=@eid AND shift_date=@date AND shift_type_id IN (@sidDuty, @sidDay)";
                                look.Parameters.AddWithValue("@eid", r.EmployeeId);
                                look.Parameters.AddWithValue("@date", od.ToString("yyyy-MM-dd"));
                                look.Parameters.AddWithValue("@sidDuty", raw.StidDuty);
                                look.Parameters.AddWithValue("@sidDay", raw.StidDayDuty);
                                var obj = look.ExecuteScalar();
                                if (obj != null && obj != DBNull.Value) found = Convert.ToInt64(obj);
                            }

                            // 当が無ければ日親も試す（メモリ内→DBの順）
                            if (found == 0 && !dayDutyIdMap.TryGetValue((r.EmployeeId, od.Date), out found))
                            {
                                using var look2 = con.CreateCommand();
                                look2.Transaction = tx;
                                look2.CommandText = @"
                                SELECT shifts_id
                                FROM daily_employee_shifts
                                WHERE employee_id=@eid AND shift_date=@date AND shift_type_id=@sidDay
                                LIMIT 1;";
                                look2.Parameters.AddWithValue("@eid", r.EmployeeId);
                                look2.Parameters.AddWithValue("@date", od.ToString("yyyy-MM-dd"));
                                look2.Parameters.AddWithValue("@sidDay", raw.StidDayDuty);
                                var obj2 = look2.ExecuteScalar();
                                if (obj2 != null && obj2 != DBNull.Value) found = Convert.ToInt64(obj2);
                            }
                            // dayDutyIdMap のメモリキャッシュでもう一押し
                            if (found == 0 && dayDutyIdMap.TryGetValue((r.EmployeeId, od), out var foundDay))
                                found = foundDay;

                            originId = (found != 0) ? found : (long?)null;
                        }
                        if (originId is null) continue; // 親が判定できなければ代休も作らない
                    }

                    // 子のUPSERT
                    pEid.Value = r.EmployeeId;
                    pDate.Value = r.Date.ToString("yyyy-MM-dd");
                    pSid.Value = r.ShiftTypeId;
                    pOrigin.Value = originId.Value;
                    cmd.ExecuteNonQuery();
                }
            }
            tx.Commit();
        }


        public List<DateTime> GetHolidays(DateTime start, DateTime end)
        {
            var result = new List<DateTime>();
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
             SELECT date
             FROM holiday
             WHERE DATE(date) BETWEEN DATE(@start) AND DATE(@end)";
            cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(DateTime.Parse(reader.GetString(0)));
            }

            return result;
        }


        public List<Employee> GetActiveEmployeesWithNightDutyClass()
        {
            var result = new List<Employee>();

            using var connection = new SqliteConnection(_connectionString);

            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, CanDoNightDuty,CanDoCatheterization
            FROM employee 
            WHERE is_active = 1 and CanDoNightDuty=1";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),

                    CanDoNightDuty = reader.GetInt32(1)==1,

                    CanDoCatheterization = reader.GetInt32(2) == 1
                });
            }
            return result;
        }

        public List<Employee> GetActiveEmployeesWithDayDutyClass()
        {
            var result = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, CanDoDayduty,CanDoCatheterization
            FROM employee 
            WHERE is_active = 1 and CanDoCatheterization==0 and CanDoDayduty==1 ";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),

                    CanDayDuty = reader.GetInt32(1)==1,
                });
            }
            return result;
        }
    }
}
