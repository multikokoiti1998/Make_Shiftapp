using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Shiftapp_demo.Models
{
    public class Technician
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // --- 属性（固定情報） ---
        public string? SaturdayClass { get; set; }         // A班 or B班
        public bool CanDoCatheterization { get; set; }     // カテ可否

        // --- シフト状況（動的） ---
        public Dictionary<DateTime, string> ShiftMap { get; set; } = new();

        public string this[DateTime date]
        {
            get => ShiftMap.TryGetValue(date, out var v) ? v : "";
            set => ShiftMap[date] = value;
        }

        // --- 月間の当直回数を計算するためのプロパティ ---
        public int GetDutyCount(DateTime month)
        {
            return ShiftMap.Count(kv =>
                kv.Key.Year == month.Year &&
                kv.Key.Month == month.Month &&
                (kv.Value == "A" || kv.Value == "B")); // 当直とするシフト名に応じて調整
        }
    }
}