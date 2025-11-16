using Microsoft.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Shiftapp_demo.Models;
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
        private string _connectionString;
        public AdminDatabaseHelper()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dbPath = Path.Combine(baseDir, "Data", "shiftapp.db");
            _connectionString = $"Data Source={dbPath}";
        }

        public int InsertEmployee(Employee e)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
        INSERT INTO employee
          (employee_name,
           CanDoCatheterization,
           saturday_class,
           MonthlyDutyLimit,
           CanDoNightDuty,
           Role,
           CanDoDayduty,
           is_active)
        VALUES
          (@name,
           @canCath,
           @satClass,
           @monthlyLimit,
           @canNight,
           @role,
           @canDay,
           1);
        SELECT last_insert_rowid();";

            cmd.Parameters.AddWithValue("@name", e.EmployeeName);
            cmd.Parameters.AddWithValue("@canCath", e.CanDoCatheterization ? 1 : 0);
            cmd.Parameters.AddWithValue("@satClass", e.SaturdayClass ?? "");
            cmd.Parameters.AddWithValue("@monthlyLimit", e.MonthlyDutyLimit);
            cmd.Parameters.AddWithValue("@canNight", e.CanDoNightDuty ? 1 : 0);
            cmd.Parameters.AddWithValue("@role", e.Role);
            cmd.Parameters.AddWithValue("@canDay", e.CanDayDuty ? 1 : 0);

            var obj = cmd.ExecuteScalar();
            return Convert.ToInt32(obj);
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
          employee_name        = @name,
          CanDoCatheterization = @canCath,
          saturday_class       = @satClass,
          MonthlyDutyLimit     = @monthlyLimit,
          CanDoNightDuty       = @canNight,
          Role                 = @role,
          CanDoDayduty         = @canDay
        WHERE employee_id = @id;";

            cmd.Parameters.AddWithValue("@name", e.EmployeeName);
            cmd.Parameters.AddWithValue("@canCath", e.CanDoCatheterization ? 1 : 0);
            cmd.Parameters.AddWithValue("@satClass", e.SaturdayClass ?? "");
            cmd.Parameters.AddWithValue("@monthlyLimit", e.MonthlyDutyLimit);
            cmd.Parameters.AddWithValue("@canNight", e.CanDoNightDuty ? 1 : 0);
            cmd.Parameters.AddWithValue("@role", e.Role);
            cmd.Parameters.AddWithValue("@canDay", e.CanDayDuty ? 1 : 0);
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

    }
}
