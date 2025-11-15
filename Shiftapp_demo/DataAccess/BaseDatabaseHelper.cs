using Microsoft.Data.Sqlite;
using Shiftapp_demo.Models;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo.DataAccess
{
    public class BaseDatabaseHelper
    {
        private string _connectionString;

        public BaseDatabaseHelper()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dbPath = Path.Combine(baseDir, "Data", "shiftapp.db");
            _connectionString = $"Data Source={dbPath}";
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
