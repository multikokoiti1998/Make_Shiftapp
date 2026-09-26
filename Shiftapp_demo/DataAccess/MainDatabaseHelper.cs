using Microsoft.Data.Sqlite;
using Shiftapp_demo.Business;
using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.IO;
using Serilog;
using System.Windows;

namespace Shiftapp_demo.DataAccess
{
    public class MainDatabaseHelper : BaseDatabaseHelper
    {

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


        public MainDatabaseHelper() : base()
        {

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

        // 在職中の職員を役職→職員コード順（管理者画面・Excel出力と同じ並び）で全員返す。
        // その月のシフト実績が1件も無い新人・非正規等も含めるためのもの
        // （GetShiftRowはdaily_employee_shiftsとのINNER JOINのため、実績が無い職員は出てこない）。
        public List<Employee> GetActiveEmployeesOrdered()
        {
            var result = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, employee_name, Role
            FROM employee
            WHERE is_active = 1
            ORDER BY Role, employee_id;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),
                    EmployeeName = reader.GetString(1),
                    Role = reader.GetInt32(2),
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
              b.shift_type_id         AS final_shift_type_id,
              p.shift_date            AS origin_date,
              COALESCE(pt.symbol, '') AS origin_symbol
            FROM daily_employee_shifts b
            JOIN employee e
              ON e.employee_id = b.employee_id AND e.is_active = 1
            LEFT JOIN shift_types t
              ON t.shift_type_id = b.shift_type_id
            LEFT JOIN daily_employee_shifts p
              ON p.shifts_id = b.origin_shifts_id
            LEFT JOIN shift_types pt
              ON pt.shift_type_id = p.shift_type_id
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

                    Symbol = reader.IsDBNull(2) ? "" : reader.GetString(2),

                    OriginDate = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)).Date,

                    OriginSymbol = reader.IsDBNull(5) ? "" : reader.GetString(5)
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

        // ソルバー向け：対象月を除く全期間の週末(土日)・祝日当直回数を職員ごとに集計する。
        // 目的関数の「週末・祝日比率の平準化」項の実績オフセットとして使うため、比率や氏名は持たない。
        public Dictionary<int, int> GetHistoricalWeekendHolidayDutyCounts(DateTime excludeMonth)
        {
            var result = new Dictionary<int, int>();
            var monthStart = new DateTime(excludeMonth.Year, excludeMonth.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            using var con = new SqliteConnection(_connectionString);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            SELECT
              d.employee_id,
              SUM(
                CASE WHEN strftime('%w', d.shift_date) IN ('0','6')
                       OR EXISTS (SELECT 1 FROM holiday h WHERE DATE(h.date) = DATE(d.shift_date))
                     THEN 1 ELSE 0 END
              ) AS weekend_holiday_duty
            FROM daily_employee_shifts d
            WHERE d.shift_type_id = @stidDuty
              AND (DATE(d.shift_date) < DATE(@monthStart) OR DATE(d.shift_date) >= DATE(@monthEnd))
            GROUP BY d.employee_id;";
            cmd.Parameters.AddWithValue("@stidDuty", GetShiftTypeIdBySymbol("当"));
            cmd.Parameters.AddWithValue("@monthStart", monthStart.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@monthEnd", monthEnd.ToString("yyyy-MM-dd"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result[reader.GetInt32(0)] = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);

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

        // シンボルに対応するシフト種別が無ければ登録する（Excel取込で未知の記号が出てきた場合用）
        public int EnsureShiftType(string symbol, string typeName, int priority = 0)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using (var check = con.CreateCommand())
            {
                check.CommandText = "SELECT shift_type_id FROM shift_types WHERE symbol = @sym;";
                check.Parameters.AddWithValue("@sym", symbol);
                var existing = check.ExecuteScalar();
                if (existing != null && existing != DBNull.Value)
                    return Convert.ToInt32(existing);
            }

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO shift_types (symbol, type_name, priority)
            VALUES (@sym, @name, @prio);
            SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@sym", symbol);
            cmd.Parameters.AddWithValue("@name", typeName);
            cmd.Parameters.AddWithValue("@prio", priority);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // シフト種別IDマップ取得
        public Dictionary<(int EmployeeId, DateTime Date), int> GetShiftMap(DateTime start, DateTime end)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();
            return GetShiftMap(con, null, start, end);
        }

        /// <summary>
        /// 呼び出し元が開いた接続（任意でトランザクション）上で読み取る。
        /// シフト作成のように、削除〜ソルバー実行〜書き込みを1トランザクションにまとめたい場合に使う。
        /// </summary>
        internal Dictionary<(int EmployeeId, DateTime Date), int> GetShiftMap(SqliteConnection con, SqliteTransaction? tx, DateTime start, DateTime end)
        {
            var map = new Dictionary<(int, DateTime), int>();

            using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
            SELECT
              b.employee_id,
              b.shift_date,
              b.shift_type_id
            FROM daily_employee_shifts b
            JOIN employee e ON e.employee_id = b.employee_id
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
        public void DeleteMonthDutyAndDayParentsWithCascade(DateTime monthFirst)
        {
            using var con = OpenConnection();
            using var tx = con.BeginTransaction();

            DeleteMonthDutyAndDayParentsWithCascade(con, tx, monthFirst);

            tx.Commit();
        }

        /// <summary>
        /// 呼び出し元が開いた接続/トランザクション上で削除だけを行う（コミットは呼び出し元の責務）。
        /// シフト作成のように、削除〜ソルバー実行〜書き込みを1トランザクションにまとめたい場合に使う。
        /// </summary>
        internal void DeleteMonthDutyAndDayParentsWithCascade(SqliteConnection con, SqliteTransaction tx, DateTime monthFirst)
        {
            var first = new DateTime(monthFirst.Year, monthFirst.Month, 1);
            var next = first.AddMonths(1);

            using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            // 共通パラメータ
            cmd.Parameters.AddWithValue("@first", first.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@next", next.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@sidDay", 0);
            cmd.Parameters.AddWithValue("@sidDuty", 1);
            cmd.Parameters.AddWithValue("@stidake", 2);
            cmd.Parameters.AddWithValue("@stidoff", 3);
            cmd.Parameters.AddWithValue("@stidSun", GetShiftTypeIdBySymbol("○"));
            cmd.Parameters.AddWithValue("@stidSatWork", GetShiftTypeIdBySymbol("/"));
            cmd.Parameters.AddWithValue("@stidSubOff", GetShiftTypeIdBySymbol("●"));

            // 1) 親のシンプル削除：origin_shifts_id IS NULL AND type IN (当/日)
            cmd.CommandText = @"
            DELETE FROM daily_employee_shifts
            WHERE origin_shifts_id IS NULL
              AND shift_type_id IN (@sidDuty, @sidDay)
              AND shift_date >= @first
              AND shift_date <  @next;";
            int deleted_count=cmd.ExecuteNonQuery();

            //2) 当月内の孤児（親が存在しない子）を削除
            cmd.CommandText = @"
             DELETE FROM daily_employee_shifts AS c
              WHERE c.shift_date >= @first AND c.shift_date < @next
              AND c.origin_shifts_id IS NULL
              AND c.shift_type_id IN (@stidake, @sidDay);";
            cmd.ExecuteNonQuery();

            // 3) 土日祝の自動割当（○/出勤"/"、および日・祝日休みの●）も当直・日勤と合わせて作り直す。
            // これらは常にGenerateOffShift側で全職員分を無条件に再計算する値のため、
            // ここで消さずに残すと、以前の生成時に付いた○・●が当直・日勤の候補者を
            // ブロックしたまま次回の再生成に持ち越されてしまう（INFEASIBLEや日勤0件の原因）。
            // ●は当直に紐づく代休(origin_shifts_idあり)と記号を共有しているため、
            // 単独マーカー(origin_shifts_id IS NULL)だけを対象にし、本物の代休は消さない。
            cmd.CommandText = @"
            DELETE FROM daily_employee_shifts
            WHERE origin_shifts_id IS NULL
              AND shift_type_id IN (@stidSun, @stidSatWork, @stidSubOff)
              AND shift_date >= @first
              AND shift_date <  @next;";
            cmd.ExecuteNonQuery();

            Log.Information($"{deleted_count} 件削除されました",deleted_count);
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

        public void DeleteOrphanNightChildren(DateTime start, DateTime end, int stidDuty, int stidAke, int stidSubOff, int stidDayWork)
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
                // 明(●)は当直の子にしかなり得ないが、代休(●)は当直・日勤どちらの子にもなり得るため、
                // 親の判定基準をシンボルごとに分ける（●を一律「当直の子」でしか判定しないと、
                // 日勤者の代休が「親が見つからない孤児」と誤判定されて消えてしまう）。
                cmd.CommandText = @"
                DELETE FROM daily_employee_shifts AS c
                WHERE c.shift_date >= @start AND c.shift_date <= @end
                  AND c.origin_shifts_id IS NOT NULL
                  AND (
                        (c.shift_type_id = @stidAke AND NOT EXISTS (
                            SELECT 1 FROM daily_employee_shifts AS p
                            WHERE p.shifts_id = c.origin_shifts_id AND p.shift_type_id = @stidDuty
                        ))
                        OR
                        (c.shift_type_id = @stidSubOff AND NOT EXISTS (
                            SELECT 1 FROM daily_employee_shifts AS p
                            WHERE p.shifts_id = c.origin_shifts_id AND p.shift_type_id IN (@stidDuty, @stidDayWork)
                        ))
                );";
                cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@stidAke", stidAke);
                cmd.Parameters.AddWithValue("@stidSubOff", stidSubOff);
                cmd.Parameters.AddWithValue("@stidDuty", stidDuty);
                cmd.Parameters.AddWithValue("@stidDayWork", stidDayWork);
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


        // ====== シフト作成用 ======
        public void BulkUpsert_Duty_Shifts(IEnumerable<ShiftWrite> items, DateTime month)
        {
            using var con = new SqliteConnection($"{_connectionString};Foreign Keys=True;");
            con.Open();
            using var tx = con.BeginTransaction();

            BulkUpsert_Duty_Shifts(con, tx, items, month);

            tx.Commit();
        }

        /// <summary>
        /// 呼び出し元が開いた接続/トランザクション上で書き込みだけを行う（コミットは呼び出し元の責務）。
        /// シフト作成のように、削除〜ソルバー実行〜書き込みを1トランザクションにまとめたい場合に使う。
        /// </summary>
        internal void BulkUpsert_Duty_Shifts(SqliteConnection con, SqliteTransaction tx, IEnumerable<ShiftWrite> items, DateTime month)
        {
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
        }


        public List<Employee> GetActiveEmployeesWithNightDutyClass()
        {
            var result = new List<Employee>();

            using var connection = new SqliteConnection(_connectionString);

            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, CanDoNightDuty, CanDoCatheterization, saturday_class
            FROM employee
            WHERE CanDoNightDuty=1 AND is_active=1";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),

                    CanDoNightDuty = reader.GetInt32(1) == 1,

                    CanDoCatheterization = reader.GetInt32(2) == 1,

                    SaturdayClass = reader.IsDBNull(3) ? "" : reader.GetString(3)
                });
            }
            return result;
        }

        // 時短勤務者一覧を取得
        public List<Employee> GetActiveEmployeesWithShortTime()
        {
            var result = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id
            FROM employee
            WHERE IsShortTime = 1 AND is_active = 1";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),
                });
            }
            return result;
        }

        // Excel取込で見つかった未登録の職員をDBへ追加する（既に存在する場合は何もしない）
        // カテーテル対応や当直・日勤対応は不明のため、いずれも不可（false）として登録し、
        // 詳細は管理者画面で後から設定してもらう想定。
        public void InsertEmployeeIfMissing(int employeeId, string employeeName)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using (var check = con.CreateCommand())
            {
                check.CommandText = "SELECT 1 FROM employee WHERE employee_id = @id;";
                check.Parameters.AddWithValue("@id", employeeId);
                if (check.ExecuteScalar() != null) return;
            }

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO employee (
              employee_id, Shift_id, employee_name, CanDoCatheterization,
              saturday_class, MonthlyDutyLimit, CanDoNightDuty, Role, CanDoDayduty, IsShortTime, is_active
            ) VALUES (
              @id, 0, @name, 0, 'A', 0, 0, 0, 0, 0, 1
            );";
            cmd.Parameters.AddWithValue("@id", employeeId);
            cmd.Parameters.AddWithValue("@name", employeeName);
            cmd.ExecuteNonQuery();
        }

        // Excel取込データから逆算した土曜日班をまとめて反映する
        public void UpdateSaturdayClass(int employeeId, string saturdayClass)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE employee SET saturday_class = @cls WHERE employee_id = @id;";
            cmd.Parameters.AddWithValue("@cls", saturdayClass);
            cmd.Parameters.AddWithValue("@id", employeeId);
            cmd.ExecuteNonQuery();
        }

        public List<Employee> GetActiveEmployeesWithDayDutyClass()
        {
            var result = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, CanDoDayduty, CanDoCatheterization, saturday_class
            FROM employee
            WHERE CanDoCatheterization==0 and CanDoDayduty==1 AND is_active=1";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),

                    CanDayDuty = reader.GetInt32(1) == 1,

                    SaturdayClass = reader.IsDBNull(3) ? "" : reader.GetString(3)
                });
            }
            return result;
        }

        // Solverへ渡す職員一覧: 当直対応可 or 日勤対応可のいずれかを満たす有効職員をまとめて取得
        // （ヒューリスティックのcanCath/cannotCath/canDayduty相当を1つのリストに統合し、
        //  Solver側はEmployee.CanDoNightDuty/CanDayDuty/CanDoCatheterizationで個別に判定する）
        public List<Employee> GetActiveEmployeesForScheduling()
        {
            var result = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, CanDoNightDuty, CanDoCatheterization, CanDoDayduty, saturday_class, MonthlyDutyLimit
            FROM employee
            WHERE is_active=1 AND (CanDoNightDuty=1 OR CanDoDayduty=1)";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),
                    CanDoNightDuty = reader.GetInt32(1) == 1,
                    CanDoCatheterization = reader.GetInt32(2) == 1,
                    CanDayDuty = reader.GetInt32(3) == 1,
                    SaturdayClass = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    MonthlyDutyLimit = reader.GetInt32(5),
                });
            }
            return result;
        }

        // シフト生成(Solver)側から使う: 全職員分の有効な勤務希望を employee_id ごとにまとめて取得
        public Dictionary<int, List<EmployeePreference>> GetAllActivePreferencesByEmployee()
        {
            var result = new Dictionary<int, List<EmployeePreference>>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT preference_id, employee_id, day_of_week, is_weekend, polarity, weight
            FROM employee_preference
            WHERE is_active = 1";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var pref = new EmployeePreference
                {
                    PreferenceId = reader.GetInt32(0),
                    EmployeeId = reader.GetInt32(1),
                    DayOfWeek = reader.IsDBNull(2) ? null : (DayOfWeek)reader.GetInt32(2),
                    IsWeekend = reader.GetInt32(3) == 1,
                    Polarity = (PreferencePolarity)reader.GetInt32(4),
                    Weight = reader.GetInt32(5),
                    IsActive = true,
                };

                if (!result.TryGetValue(pref.EmployeeId, out var list))
                {
                    list = new List<EmployeePreference>();
                    result[pref.EmployeeId] = list;
                }
                list.Add(pref);
            }

            return result;
        }
        // ====== シフト作成用 ======


        // ====== 休日取得用 ======
        public List<Holiday> GetHolidays(DateTime start, DateTime end)
        {
            var result = new List<Holiday>();
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
                var date = DateTime.Parse(reader.GetString(0));
                result.Add(new Holiday
                {
                    date = date
                });
            }

            return result;
        }

        // ====== シフトInsert用 ======
        public void SaveDailyShifts(
        DateTime startDate,
        DateTime endDate,
        IEnumerable<ShiftDataLoader> dirtyRows,
        IReadOnlyDictionary<string, int> symbolToId)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();
            using var tx = con.BeginTransaction();

            // 元になった当直/日勤（CompOffOrigin）を持つセルは、その親のshifts_idが確定してから
            // origin_shifts_id付きでUpsertする必要があるため、後回しにする（2パス）。
            var pendingLinked = new List<(ShiftDataLoader row, string dateKey, string symbol, DateTime date, DateTime originDate)>();

            foreach (var row in dirtyRows)
            {
                foreach (var kv in row.Shifts)
                {
                    var dateKey = kv.Key;
                    var symbol = kv.Value ?? "";
                    var date = DateTime.Parse(dateKey);

                    if (date < startDate || date > endDate)
                        continue;

                    if (!string.IsNullOrEmpty(symbol) && row.CompOffOrigin.TryGetValue(dateKey, out var originDate))
                    {
                        pendingLinked.Add((row, dateKey, symbol, date, originDate));
                        continue;
                    }

                    if (string.IsNullOrEmpty(symbol))
                    {
                        // シフト空文字 → DELETE
                        using var cmd = con.CreateCommand();
                        cmd.CommandText = @"
                        DELETE FROM daily_employee_shifts
                        WHERE employee_id = @eid AND shift_date = @date;";
                        cmd.Parameters.AddWithValue("@eid", row.EmployeeId);
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        if (!symbolToId.TryGetValue(symbol, out var shiftTypeId))
                            throw new Exception($"未知のシフト記号です: '{symbol}'");

                        using var cmd = con.CreateCommand();
                        cmd.CommandText = @"
                        INSERT INTO daily_employee_shifts
                            (employee_id, shift_date, shift_type_id)
                        VALUES (@eid, @date, @sid)
                        ON CONFLICT(employee_id, shift_date)
                        DO UPDATE SET shift_type_id = excluded.shift_type_id;";

                        cmd.Parameters.AddWithValue("@eid", row.EmployeeId);
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@sid", shiftTypeId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            // 2パス目：親（元になった当直/日勤）は上のループで既にUpsert済みのはずなので、
            // そのshifts_idを引いてorigin_shifts_id付きでUpsertする。親が見つからない場合
            // （保存対象外の月にずれていた等）はorigin_shifts_idなしで通常通り保存する。
            foreach (var (row, dateKey, symbol, date, originDate) in pendingLinked)
            {
                if (!symbolToId.TryGetValue(symbol, out var shiftTypeId))
                    throw new Exception($"未知のシフト記号です: '{symbol}'");

                long? originShiftsId = null;
                using (var lookup = con.CreateCommand())
                {
                    lookup.Transaction = tx;
                    lookup.CommandText = @"
                    SELECT shifts_id FROM daily_employee_shifts
                    WHERE employee_id = @eid AND shift_date = @odate;";
                    lookup.Parameters.AddWithValue("@eid", row.EmployeeId);
                    lookup.Parameters.AddWithValue("@odate", originDate.ToString("yyyy-MM-dd"));
                    var result = lookup.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        originShiftsId = Convert.ToInt64(result);
                }

                using var cmd = con.CreateCommand();
                if (originShiftsId.HasValue)
                {
                    cmd.CommandText = @"
                    INSERT INTO daily_employee_shifts
                        (employee_id, shift_date, shift_type_id, origin_shifts_id)
                    VALUES (@eid, @date, @sid, @oid)
                    ON CONFLICT(employee_id, shift_date)
                    DO UPDATE SET shift_type_id = excluded.shift_type_id, origin_shifts_id = excluded.origin_shifts_id;";
                    cmd.Parameters.AddWithValue("@oid", originShiftsId.Value);
                }
                else
                {
                    cmd.CommandText = @"
                    INSERT INTO daily_employee_shifts
                        (employee_id, shift_date, shift_type_id)
                    VALUES (@eid, @date, @sid)
                    ON CONFLICT(employee_id, shift_date)
                    DO UPDATE SET shift_type_id = excluded.shift_type_id;";
                }

                cmd.Parameters.AddWithValue("@eid", row.EmployeeId);
                cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@sid", shiftTypeId);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

    }
}
