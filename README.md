# ShiftApp — 医療現場向けシフト管理アプリケーション

![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![Language](https://img.shields.io/badge/Language-C%23-239120?logo=csharp)
![Framework](https://img.shields.io/badge/Framework-WPF%20%2F%20MVVM-blueviolet)
![DB](https://img.shields.io/badge/Database-SQLite-003B57?logo=sqlite)
![Status](https://img.shields.io/badge/Status-In%20Development-yellow)

---

## Overview

医療現場における勤務シフト作成の自動化・効率化を目的としたデスクトップアプリケーション。

当直回数・制約条件・公平性を考慮したスケジューリングロジックを実装し、管理者による柔軟な手動編集も可能。WPF + MVVM アーキテクチャにより UI とビジネスロジックを完全分離した設計。

---

## Tech Stack

| 項目 | 技術 |
|---|---|
| 言語 | C# |
| UIフレームワーク | WPF（MVVM パターン） |
| データベース | SQLite |
| ロギング | Serilog |
| 配布 | Windows インストーラー |

---

## Features

### 自動シフト生成
- 当直・日勤などの勤務種別を制約条件に従い自動割り当て
- 連続当直防止・勤務間隔制約の適用
- 当直回数の公平性を考慮したスケジューリング

### 手動編集
- セルクリックによる勤務シンボルの直感的な変更
- 編集内容をリアルタイムで SQLite へ保存

### 管理者機能
- 技師の登録・編集・削除
- 祝日・当直回数上限の設定
- 制約条件のカスタマイズ

### ログ・トラブルシュート
- Serilog によるシフト生成処理のログ出力
- 不具合解析・実運用を想定したエラートレース

---

## Architecture

```
ShiftApp/
├── Business/        # シフト生成ロジック・制約処理
├── DataAccess/      # SQLite アクセス層
├── Models/          # データモデル
├── ViewModels/      # MVVM: ViewModel層
├── Views/           # MVVM: View層（XAML）
├── FrameWork/       # 共通基盤クラス
├── Helper/          # 補助処理
├── Utils/           # 汎用ユーティリティ
├── Csv/             # CSV 入出力
├── Output/          # 出力結果
├── Resources/       # 静的リソース
└── Data/            # 初期データ・DB ファイル
```

---

## Scheduling Logic

```
制約条件
├── 当直可能 / 不可フラグ
├── 勤務間隔制約（連続当直防止）
└── 当直回数上限

最適化方針
├── 制約を満たす候補の列挙
├── 公平性スコアによる優先順位付け
└── ランダム性の付加（同スコア時）
```

> 現状はヒューリスティックベース。今後 **OR-Tools（CP-SAT）** による厳密最適化へ移行予定。

---

## Screenshots

### シフト画面

| 生成前 | 生成後 |
|---|---|
| ![シフト画面1](imgs/shift1.png) | ![シフト画面2](imgs/shift2.png) |

### 管理者画面
![管理画面](imgs/admin_.png)

### インストーラー
![インストーラー](imgs/instraller.png)

---

## Known Issues / Roadmap

| 状態 | 項目 |
|---|---|
| 🔧 対応予定 | OR-Tools による完全最適化 |
| 🔧 対応予定 | ドラッグ操作によるシフト編集 |

---

## Use Case

- 医療機関（病院・クリニック）における診療放射線技師・看護師等のシフト管理
- 当直スケジュールの自動作成と勤務負担の均等化

---

