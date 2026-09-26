using Microsoft.Data.Sqlite;
using Shiftapp_demo.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Shiftapp_demo.DataAccess
{
    public class AdminDatabaseHelper : BaseDatabaseHelper
    {
        public AdminDatabaseHelper() : base()
        {

        }
        public int InsertBlankEmployee()
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO employee (
              Shift_id,
              employee_name,
              CanDoCatheterization,
              saturday_class,
              MonthlyDutyLimit,
              CanDoNightDuty,
              Role,
              CanDoDayduty,
              IsShortTime,
              is_active
            )
                VALUES (
                   0,          -- 初期シフトID
                  '',          -- 初期は空文字
                  0,           -- false
                  'A',         -- 初期クラス
                  0,
                  0,
                  0,
                  0,
                  0,           -- 時短勤務ではない
                  1
                );
            ";

            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT last_insert_rowid();";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }


        public void DeleteEmployee(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM employee WHERE employee_id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }

        public void UpdateEmployee(Employee e)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            UPDATE employee
            SET
            Shift_id            = @shiftId,
            employee_name       = @name,
            CanDoCatheterization = @canCath,
            saturday_class      = @satClass,
            MonthlyDutyLimit    = @monthlyLimit,
            CanDoNightDuty      = @canNight,
            Role                = @role,
            CanDoDayduty        = @canDay,
            IsShortTime         = @isShortTime
            WHERE employee_id = @id;
            ";

            cmd.Parameters.AddWithValue("@shiftId", e.ShiftId);
            cmd.Parameters.AddWithValue("@name", e.EmployeeName);
            cmd.Parameters.AddWithValue("@canCath", e.CanDoCatheterization ? 1 : 0);
            cmd.Parameters.AddWithValue("@satClass", e.SaturdayClass ?? "");
            cmd.Parameters.AddWithValue("@monthlyLimit", e.MonthlyDutyLimit);
            cmd.Parameters.AddWithValue("@canNight", e.CanDoNightDuty ? 1 : 0);
            cmd.Parameters.AddWithValue("@role", e.Role);
            cmd.Parameters.AddWithValue("@canDay", e.CanDayDuty ? 1 : 0);
            cmd.Parameters.AddWithValue("@isShortTime", e.IsShortTime ? 1 : 0);

            cmd.Parameters.AddWithValue("@id", e.EmployeeId);

            cmd.ExecuteNonQuery();
        }



        public List<Holiday> GetAllHolidays(DateTime baseDate)
        {
            var result = new List<Holiday>();

            // DisplayDate から年だけ取り出す
            int year = baseDate.Year;

            // その年の 1/1 ～ 12/31 を範囲にする
            var start = new DateTime(year, 1, 1);
            var end = new DateTime(year, 12, 31);

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            SELECT date, name
            FROM holiday
            WHERE DATE(date) BETWEEN DATE(@start) AND DATE(@end);";

            cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var date = DateTime.Parse(reader.GetString(0));
                var name = reader.GetString(1);

                result.Add(new Holiday
                {
                    date = date,
                    name = name
                });
            }

            return result;
        }

        public void InsertHoliday(Holiday h)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO holiday(date, name)
            VALUES(@date, @name);";

            cmd.Parameters.AddWithValue("@date", h.date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@name", h.name);

            cmd.ExecuteNonQuery();
        }

        public void DeleteHoliday(DateTime date)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"DELETE FROM holiday WHERE date = @date;";
            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
            cmd.ExecuteNonQuery();
        }

        // ====== 技師の勤務希望 ======

        public List<EmployeePreference> GetPreferencesForEmployee(int employeeId)
        {
            var result = new List<EmployeePreference>();

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            SELECT preference_id, employee_id, day_of_week, is_weekend, polarity, weight
            FROM employee_preference
            WHERE employee_id = @id AND is_active = 1
            ORDER BY preference_id;";
            cmd.Parameters.AddWithValue("@id", employeeId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new EmployeePreference
                {
                    PreferenceId = reader.GetInt32(0),
                    EmployeeId = reader.GetInt32(1),
                    DayOfWeek = reader.IsDBNull(2) ? null : (DayOfWeek)reader.GetInt32(2),
                    IsWeekend = reader.GetInt32(3) == 1,
                    Polarity = (PreferencePolarity)reader.GetInt32(4),
                    Weight = reader.GetInt32(5),
                    IsActive = true,
                });
            }

            return result;
        }

        public int InsertPreference(EmployeePreference p)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO employee_preference (employee_id, day_of_week, is_weekend, polarity, weight, is_active)
            VALUES (@employeeId, @dayOfWeek, @isWeekend, @polarity, @weight, 1);";
            cmd.Parameters.AddWithValue("@employeeId", p.EmployeeId);
            cmd.Parameters.AddWithValue("@dayOfWeek", p.IsWeekend || p.DayOfWeek is null ? (object)DBNull.Value : (int)p.DayOfWeek.Value);
            cmd.Parameters.AddWithValue("@isWeekend", p.IsWeekend ? 1 : 0);
            cmd.Parameters.AddWithValue("@polarity", (int)p.Polarity);
            cmd.Parameters.AddWithValue("@weight", p.Weight);
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT last_insert_rowid();";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void DeletePreference(int preferenceId)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM employee_preference WHERE preference_id = @id;";
            cmd.Parameters.AddWithValue("@id", preferenceId);
            cmd.ExecuteNonQuery();
        }

        // ====== 週末・祝日当直比率（管理者画面） ======

        // シンボルに対応するシフト種別IDを取得（MainDatabaseHelper.GetShiftTypeIdBySymbolと同じ実装）
        private int GetShiftTypeIdBySymbol(string symbol)
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

        // 在職（is_active=1）の職員ごとに、全期間の当直回数と、うち週末(土日)・祝日当直の回数を集計する
        public List<EmployeeDutyRatio> GetWeekendHolidayDutyRatios()
        {
            var result = new List<EmployeeDutyRatio>();

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            SELECT
              e.employee_id,
              e.employee_name,
              COUNT(*) AS total_duty,
              SUM(
                CASE WHEN strftime('%w', d.shift_date) IN ('0','6')
                       OR EXISTS (SELECT 1 FROM holiday h WHERE DATE(h.date) = DATE(d.shift_date))
                     THEN 1 ELSE 0 END
              ) AS weekend_holiday_duty
            FROM daily_employee_shifts d
            JOIN employee e ON e.employee_id = d.employee_id AND e.is_active = 1
            WHERE d.shift_type_id = @stidDuty
            GROUP BY e.employee_id, e.employee_name
            ORDER BY e.employee_id;";
            cmd.Parameters.AddWithValue("@stidDuty", GetShiftTypeIdBySymbol("当"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new EmployeeDutyRatio
                {
                    EmployeeId = reader.GetInt32(0),
                    EmployeeName = reader.GetString(1),
                    TotalDutyCount = reader.GetInt32(2),
                    WeekendHolidayDutyCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                });
            }

            return result;
        }

    }
}
