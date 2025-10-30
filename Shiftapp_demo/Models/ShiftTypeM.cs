namespace Shiftapp_demo.Models
{
    public sealed class ShiftTypeM
    {
        public int ShiftTypeId { get; set; }
        public string Symbol { get; set; } = "";  // "当","明","●","日","/","○" など
        public string Name { get; set; } = "";  // 画面表示名（"当直","明け","代休","日勤"...）
        //public bool IsParent { get; set; }
    }
}