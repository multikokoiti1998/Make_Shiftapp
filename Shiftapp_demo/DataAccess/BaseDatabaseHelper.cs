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
        protected string _connectionString;

        public BaseDatabaseHelper()
        {
            var dbPath = GetDbPath();
            _connectionString = $"Data Source={dbPath}";
            EnsureSchema();
        }

        /// <summary>
        /// 複数の操作を1つのトランザクションにまとめたい呼び出し元向けに、開いた状態の接続を返す。
        /// 呼び出し元が using で破棄すること。
        /// </summary>
        public SqliteConnection OpenConnection()
        {
            var con = new SqliteConnection(_connectionString);
            con.Open();

            using var pragma = con.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
            pragma.ExecuteNonQuery();

            return con;
        }

        // 配布されたDBファイルにマイグレーション機構がないため、未作成のテーブルを起動のたびに
        // 冪等に用意する（CREATE TABLE IF NOT EXISTS のみ・既存テーブルには触れない）。
        private void EnsureSchema()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS employee_preference (
                preference_id INTEGER PRIMARY KEY AUTOINCREMENT,
                employee_id   INTEGER NOT NULL,
                day_of_week   INTEGER NULL,
                is_weekend    INTEGER NOT NULL DEFAULT 0,
                polarity      INTEGER NOT NULL,
                weight        INTEGER NOT NULL DEFAULT 1,
                is_active     INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (employee_id) REFERENCES employee(employee_id)
            );";
            cmd.ExecuteNonQuery();

            // 配布済みDBには無い列を冪等に追加する（既に列があれば何もしない）
            EnsureColumn(connection, "employee", "IsShortTime", "INTEGER NOT NULL DEFAULT 0");
        }

        // "ALTER TABLE ... ADD COLUMN" は列が既に存在するとエラーになるため、
        // PRAGMA table_info で存在確認してから冪等に追加する。
        private static void EnsureColumn(SqliteConnection connection, string table, string column, string columnDefinition)
        {
            using (var check = connection.CreateCommand())
            {
                check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = @col;";
                check.Parameters.AddWithValue("@col", column);
                var count = Convert.ToInt32(check.ExecuteScalar());
                if (count > 0) return;
            }

            using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {columnDefinition};";
            alter.ExecuteNonQuery();
        }

        /// <summary>
        /// 実際に使用する DB ファイルのフルパスを返す。
        /// ・基本は %LocalAppData%\ShiftApp\Data\shiftapp.db
        /// ・存在しなければ Program Files 側の Data\shiftapp.db からコピーする
        /// </summary>
        public static string GetDbPath()
        {
            // 1) ユーザーデータ領域のパスを決定
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(local, "ShiftApp", "Data");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var dbPath = Path.Combine(dir, "shiftapp.db");

            // 2) すでにあるならそれを使う（2回目以降の起動）
            if (File.Exists(dbPath))
            {
                return dbPath;
            }

            // 3) 初回起動などでまだ DB がない場合、
            //    Program Files 側（= exe と同じ場所）の Data\shiftapp.db をコピーする
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var initialDb = Path.Combine(appDir, "Data", "shiftapp.db");

            if (File.Exists(initialDb))
            {
                File.Copy(initialDb, dbPath, overwrite: false);
            }
            else
            {
                // もし初期 DB を配布していない場合は、ここで空 DB を作る／スキーマを作る処理を入れても良い
                throw new FileNotFoundException("初期 DB が見つかりません。", initialDb);
            }

            return dbPath;
        }


        //テスト用DB
        //public static string GetDbPath()
        //{
        //    string dbPath = "C:\\Users\\multi\\source\\repos\\Shiftapp_demo\\Shiftapp_demo\\Data\\test.db";
        //    return dbPath;
        //}

        public List<Employee> GetAllEmployees()
        {
            var employees = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, Shift_id,employee_name,CanDoCatheterization,saturday_class,
            MonthlyDutyLimit,CanDoNightDuty,Role, CanDoDayduty, IsShortTime
            FROM employee
            ORDER BY Role";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                employees.Add(new Employee
                {
                    EmployeeId = reader.GetInt32(0),
                    ShiftId=reader.GetInt32(1),
                    EmployeeName = reader.GetString(2),
                    CanDoCatheterization = reader.GetInt32(3) == 1,
                    SaturdayClass = reader.GetString(4),
                    MonthlyDutyLimit = reader.GetInt32(5),
                    CanDoNightDuty = reader.GetInt32(6) == 1,
                    Role = reader.GetInt32(7),
                    CanDayDuty = reader.GetInt32(8) == 1,
                    IsShortTime = reader.GetInt32(9) == 1,
                });
            }
            return employees;
        }
    }
}
