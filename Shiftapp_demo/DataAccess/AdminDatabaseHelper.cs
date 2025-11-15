using Microsoft.Data.Sqlite;
using Shiftapp_demo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.IO;

namespace Shiftapp_demo.DataAccess
{
    public class AdminDatabaseHelper
    {
        private string _connectionString;
        public AdminDatabaseHelper()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dbPath = Path.Combine(baseDir, "Data", "shiftapp.db");
            _connectionString = $"Data Source={dbPath}";
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

        public List<Employee> GetAllEmployees()
        {
            var employees = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, employee_name,CanDoCatheterization,saturday_class, 
            MonthlyDutyLimit,CanDoNightDuty,Role, CanDoDayduty,is_active
            FROM employee
            WHERE is_active = 1
            ORDER BY Role";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                employees.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),
                    EmployeeName = reader.GetString(1),
                    CanDoCatheterization = reader.GetInt32(2) == 1,
                    SaturdayClass = reader.GetString(3),
                    MonthlyDutyLimit = reader.GetInt32(4),
                    CanDoNightDuty = reader.GetInt32(5) == 1,
                    Role = reader.GetInt32(6),
                    CanDayDuty = reader.GetInt32(7) == 1,
                });
            }
            return employees;
        }
    }
}
