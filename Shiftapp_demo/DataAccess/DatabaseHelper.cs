using MaterialDesignThemes.Wpf;
using Microsoft.Data.Sqlite;
using Shiftapp_demo.Business;
using Shiftapp_demo.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
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
            SELECT s.employee_id, s.shift_date, t.symbol
            FROM daily_employee_shifts s
            JOIN employee e ON e.employee_id = s.employee_id   
            LEFT JOIN shift_types t ON s.shift_type_id = t.shift_type_id 
            WHERE e.is_active = 1
            AND DATE(s.shift_date) BETWEEN DATE(@start) AND DATE(@end)";
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

        // daily_employee_shifts に UNIQUE(employee_id, shift_date) 制約がある前提
        public void BulkUpsertShifts(IEnumerable<(int EmployeeId, DateTime Date, int ShiftTypeId)> items)
        {
            using var con = new SqliteConnection(_connectionString);
            con.Open();
            using var tx = con.BeginTransaction();

            using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
            @"
            INSERT INTO daily_employee_shifts
            (employee_id, shift_date, shift_type_id, registered_at)
            VALUES
            (@eid, @date, @stid, CURRENT_TIMESTAMP);";

            var pEid = cmd.CreateParameter(); pEid.ParameterName = "@eid"; cmd.Parameters.Add(pEid);
            var pDate = cmd.CreateParameter(); pDate.ParameterName = "@date"; cmd.Parameters.Add(pDate);
            var pStid = cmd.CreateParameter(); pStid.ParameterName = "@stid"; cmd.Parameters.Add(pStid);

            foreach (var (eid, d, stid) in items)
            {
                pEid.Value = eid;
                pDate.Value = d.ToString("yyyy-MM-dd");   // TEXT(ISO8601)で保存
                pStid.Value = stid;
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
        public Dictionary<(int EmployeeId, DateTime Date), int> GetShiftMap(DateTime start, DateTime end)
        {
            var map = new Dictionary<(int, DateTime), int>();

            using var con = new SqliteConnection(_connectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
            SELECT e.employee_id,
            DATE(s.shift_date) AS d,
            s.shift_type_id
            FROM daily_employee_shifts s
            JOIN employee e ON e.employee_id = s.employee_id
            WHERE /* e.is_active = 1 AND */       
            /* s.is_active = 1 AND */       
            DATE(s.shift_date) BETWEEN DATE(@start) AND DATE(@end);";
            cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd"));

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int eid = r.GetInt32(0);
                // SQLiteのDATEはTEXTで返ることが多いので安全にParseExact
                string ds = r.GetString(1);
                var date = DateTime.ParseExact(ds, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                int? stid = r.IsDBNull(2) ? (int?)null : r.GetInt32(2);
                if (stid.HasValue)
                {
                    map[(eid, date)] = stid.Value;
                }
            }

            return map;
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
    }
}
