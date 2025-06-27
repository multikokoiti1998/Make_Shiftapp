using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo
{
    public class DatabaseHelper
    {
        private string _connectionString;

        public DatabaseHelper(string databaseName = "RadiographerShifts.db")
        {
            // アプリケーションの実行ディレクトリにデータベースファイルを作成
            string dbPath = Path.Combine(Directory.GetCurrentDirectory(), databaseName);
            _connectionString = $"Data Source={dbPath}";

            InitializeDatabase(); // データベースとテーブルが存在しない場合は作成
        }

        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();

                // 技師テーブルの作成
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Technicians (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Contact TEXT
                    );";
                command.ExecuteNonQuery();

                // 他のテーブル（休日、シフトなど）もここで作成
                // 例:
                // command.CommandText = @"
                //    CREATE TABLE IF NOT EXISTS Holidays (
                //        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                //        Date TEXT NOT NULL,
                //        Description TEXT
                //    );";
                // command.ExecuteNonQuery();
            }
        }

        // 技師を追加するメソッド
        public void AddTechnician(Technician technician)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO Technicians (Name, Contact) VALUES (@Name, @Contact)";
                command.Parameters.AddWithValue("@Name", technician.Name);
                command.Parameters.AddWithValue("@Contact", technician.Contact);
                command.ExecuteNonQuery();
            }
        }

        // 全ての技師を取得するメソッド
        public ObservableCollection<Technician> GetAllTechnicians()
        {
            var technicians = new ObservableCollection<Technician>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Id, Name, Contact FROM Technicians";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        technicians.Add(new Technician
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Contact = reader.IsDBNull(2) ? null : reader.GetString(2)
                        });
                    }
                }
            }
            return technicians;
        }

        // 技師を検索するメソッド
        public ObservableCollection<Technician> SearchTechnicians(string keyword)
        {
            var technicians = new ObservableCollection<Technician>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                // 名前に部分一致するものを検索
                command.CommandText = "SELECT Id, Name, Contact FROM Technicians WHERE Name LIKE @Keyword OR Contact LIKE @Keyword";
                command.Parameters.AddWithValue("@Keyword", $"%{keyword}%"); // 部分一致検索
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        technicians.Add(new Technician
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Contact = reader.IsDBNull(2) ? null : reader.GetString(2)
                        });
                    }
                }
            }
            return technicians;
        }

        // 技師を削除するメソッド (例)
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

        // 技師を更新するメソッド (例)
        public void UpdateTechnician(Technician technician)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE Technicians SET Name = @Name, Contact = @Contact WHERE Id = @Id";
                command.Parameters.AddWithValue("@Name", technician.Name);
                command.Parameters.AddWithValue("@Contact", technician.Contact);
                command.Parameters.AddWithValue("@Id", technician.Id);
                command.ExecuteNonQuery();
            }
        }
    }
}
