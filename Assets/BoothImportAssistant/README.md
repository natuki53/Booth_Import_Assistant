# BOOTH Import Assistant

BOOTHライブラリから直接Unityプロジェクトにアセットをインポートするツール。VRChatアバター開発者向けの効率化ツールです。

## ✨ 特徴

- ✅ **Node.js組み込み済み** - 追加インストール不要！
- ✅ **ワンクリックインストール** - VCC経由で簡単導入
- ✅ **自動インポート** - ダウンロードから展開まで全自動
- ✅ **プログレスバー** - リアルタイムで進捗を表示
- ✅ **クロスプラットフォーム** - Windows/Mac/Linux対応

## 📦 インストール方法

### VCC（VRChat Creator Companion）を使用する場合（推奨）

1. VCCを開く
2. プロジェクトを選択
3. 「Manage Project」→「Add Package」
4. このリポジトリのURLを追加:
   ```
   https://github.com/natuki53/Booth_Import_Assistant.git?path=/Assets/BoothImportAssistant
   ```
5. 完了！ **Node.jsのインストールは不要です**

### 手動インストール

1. このリポジトリをダウンロード
2. `Assets/BoothImportAssistant/` フォルダを自分のUnityプロジェクトの `Assets/` フォルダにコピー
3. 完了！ **Node.jsのインストールは不要です**

## 🚀 使い方

### 初回セットアップ

1. **ブラウザ拡張機能のインストール**
   - Chrome/Edgeで拡張機能を有効化（開発者モード）
   - リポジトリの `BoothExtension/` フォルダを読み込む
   - 詳細な手順は[INSTALL.md](../../INSTALL.md)を参照

2. **Unityでの設定**
   - Unity Editorで「Tools」→「BOOTH Library」を開く
   - 「同期」ボタンをクリック
   - BOOTHライブラリページが開きます

### 日常の使用

1. Unity側で「同期」ボタンをクリック
2. BOOTHライブラリページが開く
3. ダウンロードしたいアセットを選択
4. Unity側で「DL」ボタンをクリック
5. 自動的にダウンロード→展開→インポート！

## 📁 ディレクトリ構造

```
Assets/
  └─ BoothImportAssistant/
      ├─ Editor/                     Unity Editor拡張
      │   ├─ BridgeManager.cs        Bridge（Node.js）管理
      │   ├─ BoothLibraryWindow.cs   UIウィンドウ
      │   └─ BoothImportAssistant.asmdef
      ├─ Bridge/                     Node.jsサーバー
      │   ├─ bridge.js               メインスクリプト
      │   ├─ package.json
      │   ├─ node_modules/           npm依存関係
      │   └─ node-runtime/           Node.js実行環境（組み込み）
      │       ├─ win-x64/            Windows用
      │       ├─ osx-x64/            macOS用
      │       └─ linux-x64/          Linux用
      ├─ package.json                VCC用パッケージ定義
      └─ README.md                   このファイル

BoothBridge/                         プロジェクトルート直下（自動生成）
  ├─ booth_assets.json               アセット情報
  ├─ thumbnails/                     サムネイル画像
  └─ temp/                           一時ファイル
```

## 🔧 必要なもの

- Unity 2022.3 以上
- Chrome または Edge ブラウザ
- BOOTHアカウント

**注意**: Node.jsは**パッケージに組み込まれています**。追加インストールは不要です！

## 📝 ライセンス

MIT License

## 🤝 貢献

プルリクエストは歓迎します！バグ報告や機能要望はIssuesで受け付けています。

## 🔗 リンク

- GitHub: https://github.com/natuki53/Booth_Import_Assistant
- BOOTH: https://booth.pm/

## ⚠️ 注意事項

- このツールはBOOTHの公式ツールではありません
- 個人利用の範囲で使用してください
- アセットのダウンロードは手動で行う必要があります（自動ダウンロードは非対応）

