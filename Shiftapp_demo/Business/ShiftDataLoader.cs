using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Shiftapp_demo.Business
{
    public class ShiftDataLoader : INotifyPropertyChanged
    {
        public ShiftDataLoader()
        {
            Tooltip = new CellTooltipLookup(_tooltips);
        }

        private int _employeeId { get; set; }

        public int EmployeeId { get => _employeeId; set { if (_shiftId != value) { _employeeId = value; Raise(nameof(EmployeeId)); IsDirty = true; } } }

        private int _shiftId { get; set; }
        public int ShiftId { get => _shiftId; set { if (_shiftId != value) { _shiftId = value; Raise(nameof(ShiftId)); IsDirty = true; } } }

        private string? _employeeName { get; set; }
        public string? EmployeeName { get => _employeeName; set { if (_employeeName != value) { _employeeName = value; Raise(nameof(EmployeeId)); IsDirty = true; } } }

        private int _role;
        public int Role { get => _role; set { if (_role!= value) {_role= value; Raise(nameof(Role)); IsDirty = true; } } }

        private Dictionary<string, string> _shifts = new();
        public IReadOnlyDictionary<string, string> Shifts => _shifts;

        // UI 編集はすべてこのインデクサを通す
        public string this[string key]
        {
            get => _shifts.TryGetValue(key, out var v) ? v : "";
            set
            {
                if (_shifts.TryGetValue(key, out var old) && old == value)
                    return;

                // 手動編集時のみ：当直の翌日に日勤を設定することは禁止（明けとして休みが必要なため）
                if (_cascadeEnabled && value == DayWorkSymbol && IsPrecededByDuty(key))
                {
                    MessageBox.Show("当直の翌日に日勤を設定することはできません（明けとして休みが必要です）。",
                        "設定できません", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Raise("Item[]"); // 選択を元の値に戻す（データは変更していないため再評価させるだけ）
                    return;
                }

                _shifts[key] = value;

                // DataGrid のセル更新通知
                // 「Item[具体的なキー]」形式だとWPFのバインディングエンジンが拾わないことがあるため、
                // インデクサ変更の標準的な合図である空括弧の「Item[]」で通知する（対象行の全セルを再評価させる）。
                Raise("Item[]");

                // モデルの更新フラグ
                IsDirty = true;

                // 手動編集時のみ：セルに直接「●」を選択した場合はCascadeDutyChangeを経由しないため
                // 紐付け（CompOffOrigin）が行われない。代休が不足している当直/日勤の代休日と一致するなら
                // その場で紐付け、アテーション判定（NeedsAttention）に正しく反映されるようにする。
                if (_cascadeEnabled && value == CompOffSymbol && old != CompOffSymbol
                    && DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var compOffDate))
                {
                    TryAutoLinkManualCompOff(key, compOffDate);
                }

                // 当直/日勤/代休の記号が変わった場合のみ、代休アテンションを再計算する
                if (value == DutySymbol || old == DutySymbol || value == DayWorkSymbol || old == DayWorkSymbol || value == CompOffSymbol || old == CompOffSymbol)
                    RecomputeAttention();

                // 手動編集時のみ：当直/日勤のセット・解除に連動して、明け・対応する代休をリアルタイムで反映する
                if (_cascadeEnabled && (value == DutySymbol || old == DutySymbol || value == DayWorkSymbol || old == DayWorkSymbol))
                    CascadeDutyChange(key, old, value);
            }
        }

        private const string DutySymbol = "当";
        private const string DayWorkSymbol = "日";
        private const string AkeSymbol = "明";
        private const string CompOffSymbol = "●";

        // 月データの初期読み込み中は連動処理（CascadeDutyChange）を止めておくためのフラグ。
        // 読み込みループも同じインデクサを通るため、これが無いとDB由来の既存データにまで
        // 連動処理がかかり、意図的に設定された値を読み込むたびに上書きしてしまう。
        private bool _cascadeEnabled;

        // 月データの読み込みが完了した後に呼び、以降の手動編集から連動を有効にする
        public void EnableCascade() => _cascadeEnabled = true;

        // 当直/日勤のセット・解除に連動して、翌日の「明け」（当直のみ）と、対応する代休を反映する。
        // 代休はShiftBusiness.GetCompWorkOff等・ソルバーと同じルールで計算した日に自動セットし、
        // その代休がどの当直/日勤に対応するか（CompOffOrigin）を記録する。これにより
        // ・アテンション判定を厳密な1対1で行える
        // ・当直/日勤を解除したとき、対応する代休だけを正確にクリアできる
        // ようになる。既存の内容は常に上書きする。
        private void CascadeDutyChange(string key, string? oldValue, string newValue)
        {
            if (!DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return;

            // 明け（当直のみ、翌日）
            if (newValue == DutySymbol || oldValue == DutySymbol)
            {
                var nextKey = day.AddDays(1).ToString("yyyy-MM-dd");
                if (_shifts.ContainsKey(nextKey))
                {
                    if (newValue == DutySymbol)
                        this[nextKey] = AkeSymbol;
                    else if (oldValue == DutySymbol)
                        this[nextKey] = string.Empty;
                }
            }

            bool wasDuty = oldValue == DutySymbol || oldValue == DayWorkSymbol;
            bool isDutyNow = newValue == DutySymbol || newValue == DayWorkSymbol;

            if (isDutyNow && !wasDuty)
                CreateCompOffFor(day, newValue == DutySymbol);
            else if (wasDuty && !isDutyNow)
                ClearOrphanCompOffFor(day);
        }

        // dutyDateの当直/日勤に対応する代休を、ソルバーと同じ計算ルールで自動セットする
        // （明けが別の祝日と重なるシルバーウィーク型は当直のみ2つ目もセット）。
        private void CreateCompOffFor(DateTime dutyDate, bool isNightDuty)
        {
            var primary = ShiftBusiness.GetCompWorkOff(dutyDate, _holidays);
            if (primary.HasValue) SetCompOffCascaded(primary.Value, dutyDate);

            if (isNightDuty && ShiftBusiness.AkeAlsoLandsOnHoliday(dutyDate, _holidays))
            {
                var extra = ShiftBusiness.GetExtraCompWorkOffForRestfulAke(dutyDate, primary, _holidays);
                if (extra.HasValue) SetCompOffCascaded(extra.Value, dutyDate);
            }
        }

        private void SetCompOffCascaded(DateTime compOffDate, DateTime originDutyDate)
        {
            var key = compOffDate.ToString("yyyy-MM-dd");
            if (!_shifts.ContainsKey(key)) return; // 表示月の範囲外（翌月にずれ込む等）は対象外

            // RecomputeAttentionが正しく参照できるよう、セットする前に紐付けを記録しておく
            _compOffOrigin[key] = originDutyDate;
            this[key] = CompOffSymbol;
        }

        // dutyDate（解除された当直/日勤日）に紐付いている代休（CompOffOrigin経由）が
        // まだ「●」のままなら、親を失った代休としてクリアする。
        // ※翌月にずれ込んだ代休はこの月のデータからは編集できないため対象外。
        private void ClearOrphanCompOffFor(DateTime dutyDate)
        {
            var linkedKeys = _compOffOrigin
                .Where(kv => kv.Value.Date == dutyDate.Date)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in linkedKeys)
            {
                if (_shifts.TryGetValue(key, out var v) && v == CompOffSymbol)
                    this[key] = string.Empty;
                _compOffOrigin.Remove(key);
            }
        }

        // 前日が当直（＝この日は明けで休みが必要）かどうか
        private bool IsPrecededByDuty(string key)
        {
            if (!DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return false;

            var prevKey = day.AddDays(-1).ToString("yyyy-MM-dd");
            return _shifts.TryGetValue(prevKey, out var prevValue) && prevValue == DutySymbol;
        }

        // 代休セル(key) → 元になった当直/日勤日。DBロード時の実データ（Shift.OriginDate）と
        // 手動編集時のCascadeDutyChangeによる自動セットの両方がここに記録される。
        // 保存時にorigin_shifts_idとしてDBへ書き戻すため、MainDatabaseHelperからも参照できるよう公開する。
        private readonly Dictionary<string, DateTime> _compOffOrigin = new();
        public IReadOnlyDictionary<string, DateTime> CompOffOrigin => _compOffOrigin;

        // DBからロードした代休/明けセルの起点（元になった当直/日勤日）をセットする
        internal void SetOrigin(string key, DateTime originDate) => _compOffOrigin[key] = originDate;

        private HashSet<string> _compOffCheckDates = new();
        private List<DateTime> _holidays = new();
        private HashSet<DateTime> _holidaySet = new();
        private Dictionary<string, (string Symbol, DateTime? OriginDate)> _nextMonthShifts = new();

        // 代休アテンション判定に必要な情報をセットし、再計算する。
        // ・compOffCheckDates: 表示月内で「本来休みのはずの日」（祝日＋日曜）の一覧
        // ・holidays: 代休日計算（ShiftBusiness.GetCompWorkOff等）に使う祝日一覧。月またぎ計算のため表示月＋翌月分が必要
        // ・nextMonthShifts: 代休が翌月にずれ込むケース（金土日当直→翌月初め等）を検出するための、
        //   この職員の翌月分シフト（記号とOriginDate）
        public void SetAttentionContext(
            HashSet<string> compOffCheckDates,
            List<DateTime> holidays,
            Dictionary<string, (string Symbol, DateTime? OriginDate)> nextMonthShifts)
        {
            _compOffCheckDates = compOffCheckDates ?? new HashSet<string>();
            _holidays = holidays ?? new List<DateTime>();
            _holidaySet = new HashSet<DateTime>(_holidays.Select(d => d.Date));
            _nextMonthShifts = nextMonthShifts ?? new Dictionary<string, (string, DateTime?)>();
        }

        // DBロード完了後・手動編集開始前に1回呼び、確定した状態でアテンションを計算する
        // （読み込みループの途中では記号とOriginDateのセット順序の都合で正しく計算できないため）。
        public void RefreshAttention() => RecomputeAttention();

        private bool _needsAttention;
        // 代休が必要な当直/日勤に対して代休が付与されていない可能性がある行に立てるフラグ（行ハイライト用）
        public bool NeedsAttention
        {
            get => _needsAttention;
            private set { if (_needsAttention != value) { _needsAttention = value; Raise(nameof(NeedsAttention)); } }
        }

        private string? _attentionMessage;
        // NeedsAttention時のツールチップ用メッセージ（代休が不足している当直/日勤日を列挙）
        public string? AttentionMessage
        {
            get => _attentionMessage;
            private set { if (_attentionMessage != value) { _attentionMessage = value; Raise(nameof(AttentionMessage)); } }
        }

        // CompOffOrigin（当直/日勤日 → 代休セルの1対1の紐付け）を使い、各当直/日勤について
        // 必要な代休数（通常1、明けが祝日と重なるシルバーウィーク型は2）が実際に紐付いた
        // 「●」のセルで満たされているかを確認する。件数の帳尻合わせではなく、当直/日勤ごとに
        // ピンポイントで判定するため、他の当直/日勤の代休を誤って「足りている」とみなさない。
        private void RecomputeAttention()
        {
            var missingDutyDates = new List<DateTime>();
            foreach (var kv in _shifts)
            {
                bool isDuty = kv.Value == DutySymbol || kv.Value == DayWorkSymbol;
                if (!isDuty) continue;
                if (!DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dutyDate)) continue;

                var primaryCompOff = ShiftBusiness.GetCompWorkOff(dutyDate, _holidays);
                if (!primaryCompOff.HasValue) continue; // この曜日・祝日区分の当直/日勤は代休不要

                int requiredSlots = 1;
                if (kv.Value == DutySymbol && ShiftBusiness.AkeAlsoLandsOnHoliday(dutyDate, _holidays))
                    requiredSlots = 2;

                if (CountLinkedCompOffFor(dutyDate) < requiredSlots)
                    missingDutyDates.Add(dutyDate);
            }

            missingDutyDates.Sort();
            NeedsAttention = missingDutyDates.Count > 0;
            AttentionMessage = NeedsAttention
                ? string.Join("、", missingDutyDates.Select(FormatDateJp)) + " の当直/日勤に対する代休が未設定です"
                : null;
        }

        // 手動で「●」を選択したセル(key/compOffDate)を、代休が不足している当直/日勤に紐付ける。
        // 現場の都合で算出ルール通りの日に付与できないことがあるため、日付の一致は問わず、
        // まだ必要数を満たしていない当直/日勤（かつ当直/日勤日以降）の中から最も日付が近いものを選ぶ。
        // 該当する当直/日勤が無い場合（通常の日曜/祝日休みとして●を使った場合など）は何もしない。
        private void TryAutoLinkManualCompOff(string key, DateTime compOffDate)
        {
            if (_compOffOrigin.ContainsKey(key)) return; // 既に紐付け済み（Cascade由来）

            DateTime? bestDutyDate = null;
            double bestDiff = double.MaxValue;

            foreach (var kv in _shifts)
            {
                bool isDuty = kv.Value == DutySymbol || kv.Value == DayWorkSymbol;
                if (!isDuty) continue;
                if (!DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dutyDate)) continue;
                if (compOffDate.Date < dutyDate.Date) continue; // 代休は当直/日勤日より前には置けない

                var primary = ShiftBusiness.GetCompWorkOff(dutyDate, _holidays);
                if (!primary.HasValue) continue; // この曜日・祝日区分の当直/日勤は代休不要

                int required = 1;
                if (kv.Value == DutySymbol && ShiftBusiness.AkeAlsoLandsOnHoliday(dutyDate, _holidays))
                    required = 2;

                if (CountLinkedCompOffFor(dutyDate) >= required) continue;

                var diff = Math.Abs((dutyDate - compOffDate).TotalDays);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestDutyDate = dutyDate;
                }
            }

            if (bestDutyDate.HasValue)
                _compOffOrigin[key] = bestDutyDate.Value;
        }

        // dutyDateに紐付いている（CompOffOriginで記録された）「●」の数を数える。
        // 表示月内は_shifts＋_compOffOrigin、翌月にずれ込んだ分はDBロード時の_nextMonthShiftsのOriginDateで判定。
        private int CountLinkedCompOffFor(DateTime dutyDate)
        {
            int count = _compOffOrigin.Count(kv =>
                kv.Value.Date == dutyDate.Date &&
                _shifts.TryGetValue(kv.Key, out var v) && v == CompOffSymbol);

            count += _nextMonthShifts.Values.Count(s =>
                s.Symbol == CompOffSymbol && s.OriginDate?.Date == dutyDate.Date);

            return count;
        }

        private static string FormatDateJp(DateTime d)
            => $"{d.Month}/{d.Day}({d.ToString("ddd", CultureInfo.GetCultureInfo("ja-JP"))})";

        // セルのツールチップ用テキスト（代休/明けの元になった当直/日勤の日付を表示する）
        private readonly Dictionary<string, string?> _tooltips = new();
        public CellTooltipLookup Tooltip { get; }

        internal void SetTooltip(string key, string? text) => _tooltips[key] = text;

        //UI更新フラグ
        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); } }
        }
        public void AcceptChanges()
        {
            IsDirty = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    // DataGridセルのToolTipバインディング用（"Tooltip[yyyy-MM-dd]"の形でXAMLから参照する）。
    // Dictionaryを直接バインドするとキー無し時にKeyNotFoundExceptionでバインディングエラーになるため、
    // null安全な専用のルックアップを用意している。
    public sealed class CellTooltipLookup
    {
        private readonly Dictionary<string, string?> _map;
        internal CellTooltipLookup(Dictionary<string, string?> map) => _map = map;
        public string? this[string key] => _map.TryGetValue(key, out var v) ? v : null;
    }
}
