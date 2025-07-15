using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;

namespace Shiftapp_demo
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
        public void AddTechnician(Technician technician)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO Technicians (Name, saturday_class, catheterization) VALUES (@Name, @saturday_class, @catheterization)";
                command.Parameters.AddWithValue("@Name", technician.Name);
                command.Parameters.AddWithValue("@saturday_class", technician.saturday_class ?? (object)DBNull.Value); // null許容
                command.Parameters.AddWithValue("@catheterization", technician.catheterization ?? (object)DBNull.Value); // null許容
                command.ExecuteNonQuery();
            }
        }

        // 全ての技師を取得するメソッド (新しいプロパティに合わせて修正)
        public async Task< ObservableCollection<Technician>> GetAllTechnicians()
        {
            var technicians = new ObservableCollection<Technician>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Id, Name, saturday_class, catheterization FROM Technicians";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        technicians.Add(new Technician
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            saturday_class = reader.IsDBNull(2) ? null : reader.GetString(2),
                            catheterization = reader.IsDBNull(3) ? null : reader.GetString(3)
                        });
                    }
                }
            }
            return technicians;
        }

        public async Task<ObservableCollection<Technician>> GetTechniciansshift()
        {
            return await Task.Run(() => GetAllTechnicians()).ConfigureAwait(false);
        }


        // 技師を検索するメソッド (このメソッドはAdminWindow側で実装しているため、ここでは不要かもしれません)
        public ObservableCollection<Technician> SearchTechnicians(string keyword)
        {
            var technicians = new ObservableCollection<Technician>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                // 新しいプロパティに合わせて検索対象を修正
                command.CommandText = "SELECT employee_id, employee_name, saturday_class, CanDoCatherization " +
                    "FROM employee WHERE employee_name LIKE @Keyword OR saturday_class LIKE @Keyword OR CanDoCatherization LIKE @Keyword";
                command.Parameters.AddWithValue("@Keyword", $"%{keyword}%");
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        technicians.Add(new Technician
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            saturday_class = reader.IsDBNull(2) ? null : reader.GetString(2),
                            catheterization = reader.IsDBNull(3) ? null : reader.GetString(3)
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
                command.CommandText = "DELETE FROM Technicians WHERE Id = @Id";
                command.Parameters.AddWithValue("@Id", id);
                command.ExecuteNonQuery();
            }
        }

        // 技師を更新するメソッド (新しいプロパティに合わせて修正)
        public void UpdateTechnician(Technician technician)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE Technicians SET Name = @Name, saturday_class = @saturday_class, catheterization = @catheterization WHERE Id = @Id";
                command.Parameters.AddWithValue("@Name", technician.Name);
                command.Parameters.AddWithValue("@saturday_class", technician.saturday_class ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@catheterization", technician.catheterization ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Id", technician.Id);
                command.ExecuteNonQuery();
            }
        }
    }
}
