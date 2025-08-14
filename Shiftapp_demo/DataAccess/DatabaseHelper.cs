using Microsoft.Data.Sqlite;
using Shiftapp_demo.Business;
using Shiftapp_demo.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;

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
        public void UpdateEmployee(Employee employee)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT *
            FROM daily_employee_shifts
            WHERE shift_date = '2025/07/07';
            ";
            command.Parameters.AddWithValue("@Name", employee.EmployeeName);
            command.Parameters.AddWithValue("@SaturdayClass", employee.SaturdayClass);
            command.Parameters.AddWithValue("@Limit", employee.MonthlyDutyLimit);
            command.Parameters.AddWithValue("@CanDo", employee.CanDoCatheterization ? 1 : 0);
            command.Parameters.AddWithValue("@Id", employee.EmployeeId);

            command.ExecuteNonQuery();
        }

        public List<Employee> GetAllEmployees()
        {
            var employees = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT employee_id, employee_name,Role FROM employee ORDER BY employee_id";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                employees.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),
                    EmployeeName = reader.GetString(1),
                    Role=reader.GetInt32(2)
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
            SELECT s.employee_id, s.shift_date, t.symbol
            FROM daily_employee_shifts s
            LEFT JOIN shift_types t ON s.shift_type_id = t.shift_type_id
            WHERE DATE(s.shift_date) BETWEEN DATE(@start) AND DATE(@end)";
            cmd.Parameters.AddWithValue("@start", startDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@end", endDate.ToString("yyyy-MM-dd"));

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



    }
}
