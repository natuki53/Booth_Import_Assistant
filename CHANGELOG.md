# Changelog

All notable changes to BOOTH Import Assistant will be documented in this file.

## [0.1.0] - 2025-11-05（開発版・未テスト）

### Added
- 初期実装完了（動作未確認）
- **BOOTHブラウザ拡張（Chrome/Edge対応）**
  - DOM解析による購入リスト取得
  - **全ページ自動巡回機能**（ページネーション対応）
  - 自動同期機能
  - 複数ダウンロードリンク対応
  - **Background Service Worker** (`background.js`)
    - Chrome `downloads` API を使用したダウンロード監視
    - ダウンロードURLと商品IDの自動紐付け
    - ファイル名変更不要（元のファイル名のままで自動展開）
- **Bridge（Node.jsローカルサーバー）**
  - HTTPサーバー（ポート4823）
  - JSON保存・サムネイルダウンロード
  - ダウンロードフォルダ監視（Windows/Mac対応）
  - **ZIP自動展開 + .unitypackage自動抽出**
  - 一時フォルダでの展開・不要ファイル自動削除
  - `downloadTrackingMap` による追跡機能
  - `/download-notify` エンドポイント
- **Unity Editor拡張**
  - BOOTH Library ウィンドウ
  - Bridge自動起動・終了管理（Windows/Mac対応）
  - FileSystemWatcherによるリアルタイム更新
  - **`.unitypackage` 自動インポート機能**
  - サムネイル付き一覧表示
  - 複数ダウンロード対応UI（ドロップダウン、個別・一括ボタン）
- 完全ローカル動作（外部通信なし）
- **Windows 10以降 / macOS 11以降対応**
- Unity 2021-2022 LTS対応

### Features Highlights
- ✨ ファイル名変更不要（自動ダウンロード特定）
- ✨ `.unitypackage` のみ自動抽出・インポート
- ✨ 複数アバター対応（バリエーション別ダウンロード）
- ✨ 全ページ自動巡回（大量購入データに対応）

### Security
- localhost限定のBridge通信
- Cookie・認証情報を扱わない設計
- BOOTH公式リンクのみ使用

## [Unreleased]

### Planned for v0.2
- 初回起動時の説明表示
- キャッシュ削除ボタン
- タグ・カテゴリ分類機能
- 検索・フィルタ機能

### Planned for v1.0（正式版）
- ダウンロード進捗表示
- 安定性向上
- 詳細なエラーメッセージ
- ユーザードキュメント充実

### Planned for v1.x（将来）
- Linux対応
- 自動ポート選択機能
- Bridge exe化（Node.js不要化）
- カスタムダウンロードフォルダ設定

