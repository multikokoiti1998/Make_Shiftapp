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
                UPDATE employee SET
                    employee_name = @Name,
                    saturday_class = @SaturdayClass,
                    MonthlyDutyLimit = @Limit,
                    CanDoCatheterization = @CanDo
                WHERE employee_id = @Id";

            command.Parameters.AddWithValue("@Name", employee.EmployeeName);
            command.Parameters.AddWithValue("@SaturdayClass", employee.SaturdayClass);
            command.Parameters.AddWithValue("@Limit", employee.MonthlyDutyLimit);
            command.Parameters.AddWithValue("@CanDo", employee.CanDoCatheterization ? 1 : 0);
            command.Parameters.AddWithValue("@Id", employee.EmployeeId);

            command.ExecuteNonQuery();
        }
    

        public List<Shift> GetShifts(DateTime startDate, DateTime endDate)
        {
            var result = new List<Shift>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT e.employee_id, e.employee_name, s.shift_date, t.symbol
        FROM daily_employee_shifts s
        JOIN employee e ON s.employee_id = e.employee_id
        JOIN shift_types t ON s.shift_type_id = t.shift_type_id
        WHERE DATE(s.shift_date) BETWEEN DATE(@start) AND DATE(@end)
        ORDER BY e.employee_id, s.shift_date
    ";

            command.Parameters.AddWithValue("@start", startDate.Date);
            command.Parameters.AddWithValue("@end", endDate.Date);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Shift
                {
                    EmployeeId = reader.GetInt32(0),
                    EmployeeName = reader.GetString(1),
                    ShiftDate = DateTime.Parse(reader.GetString(2)),
                    Symbol = reader.GetString(3)
                });
            }

            return result;
        }



    }
}
