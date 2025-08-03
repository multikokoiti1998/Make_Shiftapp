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

        public DatabaseHelper(string databaseName = "shiftapp.db")
        {
            // アプリケーションの実行ディレクトリにデータベースファイルを作成
            string dbPath = Path.Combine(Directory.GetCurrentDirectory(), databaseName);
            _connectionString = $"Data Source={dbPath}";
        }

        // 技師を追加するメソッド (新しいプロパティに合わせて修正)
        public void AddTechnician(Employee technician)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO employee (employee_name, saturday_class, CanDoCatheterization) VALUES (@Name, @SaturdayClass, @CanDo)";
                command.Parameters.AddWithValue("@Name", technician.Name);
                command.Parameters.AddWithValue("@SaturdayClass", technician.SaturdayClass);
                command.Parameters.AddWithValue("@CanDo", technician.CanDoCatheterization ? 1 : 0);
                command.ExecuteNonQuery();
            }
        }


        // 全ての技師を取得するメソッド (新しいプロパティに合わせて修正)
        public async Task<ObservableCollection<Employee>> GetAllTechnicians()
        {
            var technicians = new ObservableCollection<Employee>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT employee_id, employee_name, saturday_class, CanDoCatheterization FROM employee";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        technicians.Add(new Employee
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            SaturdayClass = reader.IsDBNull(2) ? null : reader.GetString(2),
                            CanDoCatheterization = !reader.IsDBNull(3) && reader.GetInt32(3) == 1
                        });
                    }
                }
            }
            return technicians;
        }




        // 技師を検索するメソッド (このメソッドはAdminWindow側で実装しているため、ここでは不要かもしれません)
        public ObservableCollection<Employee> SearchTechnicians(string keyword)
        {
            var technicians = new ObservableCollection<Employee>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT employee_id, employee_name, saturday_class, CanDoCatheterization 
                FROM employee 
                WHERE employee_name LIKE @Keyword 
                OR saturday_class LIKE @Keyword 
                OR CanDoCatheterization LIKE @Keyword";

                command.Parameters.AddWithValue("@Keyword", $"%{keyword}%");

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        technicians.Add(new Employee
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            SaturdayClass = reader.IsDBNull(2) ? null : reader.GetString(2),
                            CanDoCatheterization = !reader.IsDBNull(3) && reader.GetInt32(3) == 1
                        });
                    }
                }
            }
            return technicians;
        }


        // 技師を削除するメソッド
        public void DeleteTechnician(int id)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM employee WHERE employee_id = @Id";
                command.Parameters.AddWithValue("@Id", id);
                command.ExecuteNonQuery();
            }
        }


        // 技師を更新するメソッド (新しいプロパティに合わせて修正)
        public void UpdateTechnician(Employee technician)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                UPDATE employee 
                SET employee_name = @Name, 
                saturday_class = @SaturdayClass, 
                CanDoCatheterization = @CanDo 
                WHERE employee_id = @Id";

                command.Parameters.AddWithValue("@Name", technician.Name);
                command.Parameters.AddWithValue("@SaturdayClass", technician.SaturdayClass);
                command.Parameters.AddWithValue("@CanDo", technician.CanDoCatheterization ? 1 : 0);
                command.Parameters.AddWithValue("@Id", technician.Id);

                command.ExecuteNonQuery();
            }
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
        WHERE s.shift_date BETWEEN @start AND @end
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
