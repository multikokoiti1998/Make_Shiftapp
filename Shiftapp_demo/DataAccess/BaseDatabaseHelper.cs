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
        public List<Employee> GetAllEmployees()
        {
            var employees = new List<Employee>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT employee_id, Shift_id,employee_name,CanDoCatheterization,saturday_class, 
            MonthlyDutyLimit,CanDoNightDuty,Role, CanDoDayduty
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
                });
            }
            return employees;
        }
    }
}
