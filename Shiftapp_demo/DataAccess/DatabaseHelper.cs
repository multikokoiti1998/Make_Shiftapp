using MaterialDesignThemes.Wpf;
using Microsoft.Data.Sqlite;
using Shiftapp_demo.Business;
using Shiftapp_demo.Models;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Shiftapp_demo.DataAccess
{
    public class DatabaseHelper
    {
        private string _connectionString;

        public DatabaseHelper()
        {
            _connectionString = "Data Source=C:\\sqlite_db\\shiftapp.db";
        }

        // 技師を追加するメソッド (新しいプロパティに合わせて修正)
        //Todo　不完全
        public void AddEmployee(Employee employee)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO employee 
                (employee_name, saturday_class, MonthlyDutyLimit, CanDoCatheterization) 
                VALUES (@Name, @SaturdayClass, @Limit, @CanDo)";

            command.Parameters.AddWithValue("@Name", employee.EmployeeName);
            command.Parameters.AddWithValue("@SaturdayClass", employee.SaturdayClass);
            command.Parameters.AddWithValue("@Limit", employee.MonthlyDutyLimit);
            command.Parameters.AddWithValue("@CanDo", employee.CanDoCatheterization ? 1 : 0);

            command.ExecuteNonQuery();
        }


        // 全ての技師を取得するメソッド (新しいプロパティに合わせて修正)
        //Todo　不完全
        public async Task<ObservableCollection<Employee>> GetAllEmployeesAsync()
        {
            var employees = new ObservableCollection<Employee>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT employee_id, employee_name, saturday_class, MonthlyDutyLimit, CanDoCatheterization 
                FROM employee";

            using var reader = command.ExecuteReader();
            while (await reader.ReadAsync())
            {
                var emp = new Employee
                {
                    EmployeeId = reader.GetInt32(0),
                    EmployeeName = reader.GetString(1),
                    SaturdayClass = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    MonthlyDutyLimit = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    CanDoCatheterization = !reader.IsDBNull(4) && reader.GetInt32(4) == 1
                };

                employees.Add(emp);
            }

            return employees;
        }



        // 技師を検索するメソッド (このメソッドはAdminWindow側で実装しているため、ここでは不要かもしれません)
        public ObservableCollection<Employee> SearchEmployees(string keyword)
        {
            var employees = new ObservableCollection<Employee>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT employee_id, employee_name, saturday_class, MonthlyDutyLimit, CanDoCatheterization 
                FROM employee 
                WHERE employee_name LIKE @Keyword 
                OR saturday_class LIKE @Keyword";

            command.Parameters.AddWithValue("@Keyword", $"%{keyword}%");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                employees.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),
                    EmployeeName = reader.GetString(1),
                    SaturdayClass = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    MonthlyDutyLimit = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    CanDoCatheterization = !reader.IsDBNull(4) && reader.GetInt32(4) == 1
                });
            }

            return employees;
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

        // --- 職員の更新 ---


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

        public List<Shift> GetShiftsOnly(DateTime startDate, DateTime endDate)
        {
            var result = new List<Shift>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            WITH latest AS (
              SELECT s.*,
                     ROW_NUMBER() OVER (
                       PARTITION BY s.employee_id, s.shift_date
                       ORDER BY s.registered_at DESC, s.shifts_id DESC
                     ) AS rn
              FROM daily_employee_shifts s
              WHERE s.shift_date >= @start AND s.shift_date < @end
            ),
            base AS (SELECT * FROM latest WHERE rn = 1),
            orig_duty AS (  -- 明け判定用：当直のみ
              SELECT b.shifts_id, b.employee_id, b.shift_date
              FROM base b
              WHERE b.shift_type_id = @stidDuty
            ),
            resolved AS (  -- 不整合を潰した最終シフト種別ID
              SELECT
                b.employee_id,
                b.shift_date,
                CASE
                  -- 明け：前日の当直（origin）が無ければ無効
                  WHEN b.shift_type_id = @stidAke
                    AND NOT EXISTS (
                      SELECT 1 FROM orig_duty o
                      WHERE o.shifts_id   = b.origin_shifts_id
                        AND o.employee_id = b.employee_id
                        AND o.shift_date  = DATE(b.shift_date, '-1 day')
                    )
                  THEN NULL

                  -- 代休：元が 当 or 日 or 土曜出勤(/) に該当しなければ無効
                  WHEN b.shift_type_id = @stidDaikyu
                    AND NOT EXISTS (
                      SELECT 1 FROM base o
                      WHERE o.shifts_id    = b.origin_shifts_id
                        AND o.employee_id  = b.employee_id
                        AND o.shift_type_id IN (@stidDuty, @stidDayDuty, @stidSatWork)
                    )
                  THEN NULL

                  ELSE b.shift_type_id
                END AS final_shift_type_id
              FROM base b
            )
            SELECT
              r.employee_id,
              r.shift_date,
              COALESCE(t.symbol, '') AS symbol,       -- これをUI表示に使う
              r.final_shift_type_id                   -- 必要なら内部用途で
            FROM resolved r
            LEFT JOIN shift_types t ON t.shift_type_id = r.final_shift_type_id
            JOIN employee e ON e.employee_id = r.employee_id AND e.is_active = 1
            ORDER BY r.employee_id, r.shift_date;";

            cmd.Parameters.AddWithValue("@start", startDate.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@end", endDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@stidDuty", 1); // 当
            cmd.Parameters.AddWithValue("@stidDayDuty", 0); // 日
            cmd.Parameters.AddWithValue("@stidSatWork", 5); // /
            cmd.Parameters.AddWithValue("@stidAke", 2); // 明
            cmd.Parameters.AddWithValue("@stidDaikyu", 3); // 代
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
            WITH latest AS (
              SELECT s.*,
                     ROW_NUMBER() OVER (
                       PARTITION BY s.employee_id, s.shift_date
                       ORDER BY s.registered_at DESC, s.shifts_id DESC
                     ) AS rn
              FROM daily_employee_shifts s
              WHERE s.shift_date >= @start AND s.shift_date < @next
            ),
            base AS (SELECT * FROM latest WHERE rn = 1)
            SELECT
              b.employee_id,
              b.shift_date,
              CASE
                -- 明け：前日の当直(origin)が無ければ空文字
                WHEN b.shift_type_id = @stidAke
                  AND NOT EXISTS (
                    SELECT 1
                    FROM daily_employee_shifts o
                    WHERE o.shifts_id    = b.origin_shifts_id
                      AND o.employee_id  = b.employee_id
                      AND o.shift_type_id = @stidDuty
                      AND o.shift_date   = DATE(b.shift_date, '-1 day')
                  )
                THEN ''

                -- 代休：元が 当 or 日 のいずれでも無ければ空文字（※土曜出勤は対象外）
                WHEN b.shift_type_id = @stidDaikyu
                  AND NOT EXISTS (
                    SELECT 1
                    FROM daily_employee_shifts o
                    WHERE o.shifts_id    = b.origin_shifts_id
                      AND o.employee_id  = b.employee_id
                      AND o.shift_type_id IN (@stidDuty, @stidDayDuty)
                  )
                THEN ''

                ELSE COALESCE(t.symbol,'')
              END AS symbol
            FROM base b
            LEFT JOIN shift_types t ON t.shift_type_id = b.shift_type_id
            JOIN employee e ON e.employee_id = b.employee_id AND e.is_active = 1
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

        //シフト削除
        private static void DeleteMonthDutyAndDayParentsWithCascade(
            SqliteConnection con, SqliteTransaction tx, DateTime monthFirst,
            int stidDuty, int stidDay)
        {
            var first = new DateTime(monthFirst.Year, monthFirst.Month, 1);
            var next = first.AddMonths(1);

            // 接続ごとに有効化（保険）
            using (var fk = con.CreateCommand())
            {
                fk.Transaction = tx;
                fk.CommandText = "PRAGMA foreign_keys=ON;";
                fk.ExecuteNonQuery();
            }

            using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
            DELETE FROM daily_employee_shifts
            WHERE origin_shifts_id IS NULL                -- 親だけ
              AND shift_date >= @first AND shift_date < @next
              AND shift_type_id IN (@sidDuty, @sidDay);";
            cmd.Parameters.AddWithValue("@first", first.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@next", next.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@sidDuty", stidDuty);
            cmd.Parameters.AddWithValue("@sidDay", stidDay);
            cmd.ExecuteNonQuery();
        }


        // 土日のデフォルト登録
        public void BulkUpsertShifts(IEnumerable<(int EmployeeId, DateTime Date, int ShiftTypeId)> items)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();
            using (var pragma = con.CreateCommand())
            {   // 連打でのロック緩和（任意）
                pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
                pragma.ExecuteNonQuery();
            }

            using var tx = con.BeginTransaction();

            using var cmd = con.CreateCommand();

            cmd.Transaction = tx;
            cmd.CommandText = @"
            INSERT INTO daily_employee_shifts (employee_id, shift_date, shift_type_id, registered_at)
            VALUES (@eid, @date, @stid, CURRENT_TIMESTAMP)
            ON CONFLICT(employee_id, shift_date) DO UPDATE SET
              shift_type_id = excluded.shift_type_id,
              registered_at = CURRENT_TIMESTAMP;";
            var pEid = cmd.CreateParameter(); pEid.ParameterName = "@eid"; cmd.Parameters.Add(pEid);
            var pDate = cmd.CreateParameter(); pDate.ParameterName = "@date"; cmd.Parameters.Add(pDate);
            var pSid = cmd.CreateParameter(); pSid.ParameterName = "@stid"; cmd.Parameters.Add(pSid);

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
        public void BulkUpsert_Duty_Shifts(IEnumerable<ShiftWrite> items)
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
            var monthFirst = new DateTime(items.Min(x => x.Date).Year, items.Min(x => x.Date).Month, 1);

            // ★ 当月の「親：当直・日勤」だけ削除（子はCASCADEで自動削除）
            DeleteMonthDutyAndDayParentsWithCascade(con, tx, monthFirst, raw.StidDuty, raw.StidDayDuty);

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
                    pDate.Value = r.Date.ToString("yyyy-MM-dd");                 // DateTime でOK
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
                                look.Parameters.AddWithValue("@date", od);
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
                                look2.Parameters.AddWithValue("@date", od);
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
                    pDate.Value = r.Date;
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
            WHERE is_active = 1";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),

                    CanDoNightDuty = reader.GetInt32(1),

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

                    CanDoNightDuty = reader.GetInt32(1),
                });
            }
            return result;
        }
    }
}
