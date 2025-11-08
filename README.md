# BOOTH Import Assistant

**BOOTHライブラリから直接Unityプロジェクトにアセットをインポートするツール**  
VRChatアバター開発者向けの効率化ツールです。

<div align="center">

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-blue.svg)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg)]()

</div>

---

## ✨ 特徴

- ✅ **Node.js組み込み済み** - 追加インストール不要！
- ✅ **VCC対応** - ワンクリックでインストール
- ✅ **自動インポート** - ダウンロード→展開→インポートを全自動化
- ✅ **プログレスバー** - リアルタイムで進捗を表示
- ✅ **複数ダウンロード対応** - アバター別・マテリアル別に自動分類
- ✅ **完全ローカル** - 外部通信なし・セキュア設計
- ✅ **クロスプラットフォーム** - Windows/Mac/Linux対応

---

## 📦 インストール方法

### VCC/ALCOM（VPMリポジトリ経由）を使用する場合（推奨）

1. **VCCまたはALCOMを開く**
2. **Settings（設定）を開く**
3. **「Packages」→「Add Repository」**
4. **以下のURLを追加:**
   ```
   https://natuki53.github.io/Booth_Import_Assistant/index.json
   ```
5. **プロジェクトを選択**
6. **「Manage Project」→「BOOTH Import Assistant」を追加**
7. **完了！** Node.jsのインストールは不要です 🎉

### VCC（Git URL経由）を使用する場合

1. VCCを開く
2. プロジェクトを選択
3. 「Manage Project」→「Add Package」
4. このリポジトリのURLを追加:
   ```
   https://github.com/natuki53/Booth_Import_Assistant.git?path=/Assets/BoothImportAssistant
   ```
5. 完了！ **Node.jsのインストールは不要です** 🎉

### 手動インストール

1. [Releases](https://github.com/natuki53/Booth_Import_Assistant/releases) から最新版をダウンロード
2. `Assets/BoothImportAssistant/` フォルダを自分のUnityプロジェクトの `Assets/` フォルダにコピー
3. 完了！ **Node.jsのインストールは不要です** 🎉

### ブラウザ拡張機能のインストール（必須）

1. Chrome または Edge を開く
2. `chrome://extensions/` にアクセス
3. 右上の「デベロッパーモード」を有効化
4. 「パッケージ化されていない拡張機能を読み込む」をクリック
5. リポジトリの `BoothExtension/` フォルダを選択

---

## 🚀 使い方

### 初回セットアップ

1. **Unity Editorで「BOOTH Library」を開く**
   - メニューから `Tools > BOOTH Library` を選択

2. **「同期」ボタンをクリック**
   - BOOTHライブラリページが開きます
   - 全ページを自動的に巡回してデータを取得

3. **完了！**
   - Unity側に購入したアセット一覧が表示されます
   - サムネイル付きで見やすく表示

### アセットのダウンロードとインポート

1. **Unity側で「DL」ボタンをクリック**
   - BOOTHのダウンロードページが開きます

2. **BOOTHでダウンロードボタンをクリック**
   - ZIPファイルがダウンロードフォルダに保存されます
   - ファイル名の変更は不要です

3. **自動的にインポート開始！**
   - プログレスバーで進捗を確認
   - ZIP展開 → .unitypackage検出 → Unityにインポート
   - インポート後、.unitypackageファイルは自動削除

#### 複数ダウンロード対応

**アバター別ダウンロード:**
- ドロップダウンメニューで選択
- 例：「萌ちゃん用」「桔梗ちゃん用」

**マテリアル:**
- 別ボタンで表示
- 「マテリアル」「マテリアル 2」など

---

## 📁 ディレクトリ構造

```
Booth_Import_Assistant/
  ├─ Assets/
  │   └─ BoothImportAssistant/      ← VCCパッケージ本体
  │       ├─ Editor/                Unity Editor拡張
  │       │   ├─ BridgeManager.cs
  │       │   ├─ BoothLibraryWindow.cs
  │       │   └─ BoothImportAssistant.asmdef
  │       ├─ Bridge/                Node.jsサーバー
  │       │   ├─ bridge.js
  │       │   ├─ package.json
  │       │   ├─ node_modules/      npm依存関係
  │       │   └─ node-runtime/      Node.js実行環境（組み込み）
  │       │       ├─ win-x64/       Windows用
  │       │       ├─ osx-x64/       macOS用
  │       │       └─ linux-x64/     Linux用
  │       ├─ package.json           VCC用パッケージ定義
  │       └─ README.md
  │
  ├─ BoothExtension/                ブラウザ拡張機能（別途インストール）
  │   ├─ manifest.json
  │   ├─ content.js
  │   └─ background.js
  │
  ├─ BoothBridge/                   実行時生成（プロジェクトルート直下）
  │   ├─ booth_assets.json          アセット情報
  │   ├─ thumbnails/                サムネイル画像
  │   └─ temp/                      一時ファイル
  │
  ├─ README.md
  └─ LICENSE
```

---

## 🔄 動作フロー

```
1. Unity「同期」ボタン
   ↓
2. Bridge自動起動（Node.js）
   ↓
3. BOOTHライブラリページが開く
   ↓
4. 拡張機能が全ページを自動巡回
   ↓
5. Bridgeへデータ送信
   ↓
6. JSON保存・サムネイルDL
   ↓
7. Unity UIが自動更新
   ↓
8. ダウンロード検知
   ↓
9. 自動展開・インポート
   ↓
10. 完了！
```

---

## 🔧 必要なもの

- Unity 2022.3 以上
- Chrome または Edge ブラウザ
- BOOTHアカウント

**注意**: Node.jsは**パッケージに組み込まれています**。追加インストールは不要です！

---

## 🐛 トラブルシューティング

### Bridgeが起動しない

**Windows:**
1. Unity コンソールでエラーメッセージを確認
2. `Assets/BoothImportAssistant/Bridge/node-runtime/win-x64/node.exe` が存在するか確認

**Mac/Linux:**
システムのNode.jsにフォールバックします。Node.jsをインストールしてください:
```bash
# Mac (Homebrew)
brew install node

# Linux
sudo apt install nodejs
```

### ブラウザ拡張が動作しない

1. `chrome://extensions/` で拡張機能が有効か確認
2. デベロッパーモードが有効か確認
3. ブラウザを再起動

### 同期ボタンを押してもデータが表示されない

1. ブラウザの開発者ツール（F12）を開く
2. Console タブで `[BOOTH Import]` のログを確認
3. BOOTHにログインしているか確認

### 自動インポートが動作しない

1. Unity コンソールでエラーを確認
2. `BoothBridge/temp/` フォルダが存在するか確認
3. .unitypackageファイルが正しく配置されているか確認

---

## 🔒 セキュリティについて

本ツールは以下のセキュリティ方針で設計されています：

- ✅ **外部通信なし**: すべての処理がローカルで完結
- ✅ **認証情報不使用**: Cookie・トークン・パスワードを扱わない
- ✅ **BOOTH公式リンクのみ**: 商品ページは公式URLのみ使用
- ✅ **localhost限定**: Bridgeはローカルホストのみでリッスン
- ✅ **ユーザー操作必須**: 自動ログイン・自動ダウンロードなし

**本ツールはBOOTH公式ツールではありません。個人の責任でご利用ください。**

---

## 📝 技術スタック

- **Unity Editor拡張**: C#
- **Bridge サーバー**: Node.js v20
- **ブラウザ拡張**: JavaScript (Manifest V3)
- **通信**: HTTP (localhost:4823)
- **データ形式**: JSON

---

## 🛠️ 開発者向け

### ビルド・テスト

```bash
# Bridge の依存関係インストール
cd Assets/BoothImportAssistant/Bridge
npm install

# Unityでテスト
# Tools > BOOTH Library を開く
```

### Node.jsバイナリの更新

詳細は `Assets/BoothImportAssistant/Bridge/node-runtime/README.md` を参照。

---

## 📝 ライセンス

MIT License - 詳細は [LICENSE](LICENSE) を参照

**Node.js**: MIT License (組み込みバイナリ)

---

## 🤝 貢献

プルリクエストは歓迎します！バグ報告や機能要望はIssuesで受け付けています。

---

## ⚠️ 免責事項

本ツールは非公式ツールです。BOOTHの仕様変更により動作しなくなる可能性があります。
本ツールの使用により生じたいかなる損害についても、作者は一切の責任を負いません。

---

## 🔗 リンク

- **GitHub**: https://github.com/natuki53/Booth_Import_Assistant
- **BOOTH**: https://booth.pm/
- **VCC**: https://vcc.docs.vrchat.com/

---

**Enjoy your VRChat creation! 🎨✨**
