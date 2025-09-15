using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo.Models
{
    public class ShiftCsvRow
    {
        public int 個人コード { get; init; }
        public string 氏名 { get; init; } = "";
        public string 処理日 { get; init; } = ""; // yyyy/MM/dd
        public string カレンダー { get; init; } = ""; // 勤務 or 休日
        public string 勤怠区分 { get; init; } = "なし";
        public string シフト区分 { get; init; } = ""; // 日勤/半日/当直 など
        public string 出勤例外 { get; init; } = "なし";
        public string 退勤例外 { get; init; } = "なし";
        public string 修正処理日 { get; init; } = ""; // yyyy/M/d
    }
}
