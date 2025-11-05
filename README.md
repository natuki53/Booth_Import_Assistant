# BOOTH Import Assistant v0.1（開発版・未テスト）

VRChat改変ユーザーのための **BOOTH購入資産管理 & Unityインポート補助ツール**

> ⚠️ **注意**: このバージョンは実装完了後、まだ動作確認を行っていません。

## 📋 システム概要

BOOTHで購入したアバター・衣装・ツールなどを、Unityプロジェクトで簡単に管理・インポートできるようにするツールです。

### 特徴

- ✅ **完全ローカル**: 外部通信なし・監視なし・ユーザー操作のみで動作
- ✅ **安全設計**: Cookie・認証情報を扱わない
- ✅ **高UX**: サムネイル付き一覧表示・リアルタイム更新対応
- ✅ **自動インポート**: ダウンロードしたZIPから `.unitypackage` を自動抽出してUnityにインポート

---

## 🔧 必要な環境

- **OS**: Windows 10以降 / macOS 11 (Big Sur) 以降
- **Unity**: 2021 LTS 〜 2022 LTS
- **Node.js**: v18 以上
- **ブラウザ**: Chrome または Microsoft Edge

---

## 📦 インストール手順

> **Macユーザーの方へ**: より詳細な手順は [MAC_SETUP.md](MAC_SETUP.md) をご覧ください。

### 1. Node.jsのインストール

https://nodejs.org/ から Node.js（v18以上）をダウンロードしてインストールしてください。

インストール後、コマンドプロンプトで以下を実行して確認：

```bash
node --version
```

`v18.0.0` 以上が表示されればOKです。

### 2. Bridgeの依存関係インストール

**Windows（コマンドプロンプトまたはPowerShell）:**
```bash
cd Bridge
npm install
```

**Mac（ターミナル）:**
```bash
cd Bridge
npm install
```

### 3. Unity拡張のインポート

1. Unityプロジェクトを開く
2. `UnityExtension` フォルダ全体をプロジェクトにコピー
   - Assets フォルダ内に配置推奨：`Assets/BoothImportAssistant/`
3. Unity Editorが自動的にスクリプトをコンパイル

### 4. ブラウザ拡張のインストール

1. Chrome または Edge を開く
2. `chrome://extensions/` にアクセス
3. 右上の「デベロッパーモード」を有効化
4. 「パッケージ化されていない拡張機能を読み込む」をクリック
5. `BoothExtension` フォルダを選択

---

## 🚀 使い方

### 初回同期（購入リストの取得）

1. **Unity でプロジェクトを開く**
   - プロジェクトを必ず保存してください（無名プロジェクトは非対応）

2. **BOOTH Library ウィンドウを開く**
   - Unity メニューから `Tools > BOOTH Library` を選択

3. **同期ボタンを押す**
   - ウィンドウ上部の「同期」ボタンをクリック
   - 自動的にBridgeが起動し、BOOTHページ（https://accounts.booth.pm/library）が開きます

4. **自動同期完了**
   - BOOTHページ読み込み完了後、**全ページを自動的に巡回**してデータを取得します
   - 複数ページある場合、画面右上に進捗が表示されます（例：🔄 ページ 3/10 を取得中...）
   - 数秒〜数十秒後、Unity の BOOTH Library に購入リストが表示されます
   
   **全ページ自動巡回の特徴:**
   - ページネーションを自動検出
   - 全ページを順次取得して統合
   - 重複を自動除去
   - レート制限考慮（ページあたり0.5秒待機）

### アセットのダウンロードとインポート

#### 単一ダウンロードの場合

1. **ダウンロードボタンを押す**
   - BOOTH Library でダウンロードしたいアセットの「ダウンロード」ボタンをクリック
   - BOOTHのダウンロードページが開きます

2. **BOOTHから正式にダウンロード**
   - 開かれたページで、公式のダウンロードボタンをクリック
   - ZIPファイルが「ダウンロード」フォルダに保存されます
     - Windows: `C:\Users\<ユーザー名>\Downloads`
     - Mac: `/Users/<ユーザー名>/Downloads`
   - **ファイル名の変更は不要です**（元のファイル名のままでOK）

3. **自動展開・インポート**
   - Bridgeが自動的にZIPを検知・特定
   - ZIPから `.unitypackage` ファイルのみを抽出
   - `Assets/ImportedAssets/booth_<ID>/` に配置
   - **Unityが自動的にインポートダイアログを表示**
   - ユーザーがインポート内容を確認して実行

#### 複数ダウンロード対応（VRChatアバター改変向け）

1つの商品に複数のダウンロードリンクがある場合、**バリエーション数に応じて最適なUIで表示**されます：

**2〜3個のバリエーション:**
- 各バリエーション用のボタンが表示されます
- 例：「萌ちゃん用」「桔梗ちゃん用」「マテリアル共通」

**4個以上のバリエーション:**
- **ドロップダウンメニュー**で選択
- 「選択中をDL」ボタンで個別ダウンロード
- 「全てDL」ボタンで一括ダウンロード（全ページを0.5秒間隔で開く）

**ダウンロード方法:**
- 各ページでBOOTHのダウンロードボタンをクリックするだけ
- **ファイル名の変更は不要です**（元のファイル名のままでOK）
- Chrome拡張が自動的にファイルと商品IDを紐付けます

**自動処理の流れ:**
1. ZIPを一時フォルダに展開
2. `.unitypackage` ファイルを検索
3. 以下のディレクトリに配置：
   - `Assets/ImportedAssets/booth_<ID>/package.unitypackage`（単一）
   - `Assets/ImportedAssets/booth_<ID>/variant_1/package.unitypackage`（複数1番目）
   - `Assets/ImportedAssets/booth_<ID>/variant_2/package.unitypackage`（複数2番目）
4. Unityが自動でインポートダイアログを表示
5. ユーザーが内容を確認してインポート

詳細は以下を参照：
- [MULTIPLE_DOWNLOADS.md](MULTIPLE_DOWNLOADS.md) - 複数ダウンロード完全ガイド
- [UI_GUIDE.md](UI_GUIDE.md) - Unity UI詳細ガイド

---

## 📂 プロジェクト構成

```
Booth_Import_Assistant/
├── .gitignore                  # Git除外設定
│
├── Bridge/                     # Node.jsローカルサーバー
│   ├── bridge.js               # メインサーバースクリプト
│   ├── package.json            # Node.js依存関係
│   ├── .npmrc                  # npm設定
│   ├── README.md               # Bridge詳細ガイド
│   └── node_modules/           # npm install で生成（.gitignore）
│
├── BoothExtension/             # ブラウザ拡張機能（Chrome/Edge）
│   ├── manifest.json           # 拡張機能設定（Manifest V3）
│   ├── content.js              # DOM解析・全ページ自動巡回
│   ├── background.js           # ダウンロード監視（Service Worker）
│   ├── README.md               # インストール・使用方法
│   ├── DEBUG.md                # デバッグ方法
│   └── ICONS_README.txt        # アイコン配置方法
│
├── UnityExtension/             # Unity Editor拡張
│   └── Editor/
│       ├── BridgeManager.cs            # Bridge起動・終了管理
│       ├── BoothLibraryWindow.cs       # メインUIウィンドウ
│       └── BoothImportAssistant.asmdef # Assembly Definition
│
├── BoothBridge/                # 実行時生成ディレクトリ（.gitignore）
│   ├── .gitkeep                # ディレクトリ保持用
│   ├── booth_assets.json       # 購入リストデータ（実行時生成）
│   ├── booth_assets.backup.json # バックアップ（実行時生成）
│   └── thumbnails/             # サムネイル画像（実行時生成）
│
├── Assets/ImportedAssets/      # ダウンロードアセット展開先（.gitignore）
│   └── .gitkeep                # ディレクトリ保持用
│       └── booth_<ID>/         # 商品IDごとのディレクトリ（実行時生成）
│           ├── *.unitypackage  # 抽出された.unitypackage
│           └── variant_*/      # 複数バリエーション用サブフォルダ
│
├── README.md                   # このファイル
├── CHANGELOG.md                # 変更履歴
├── INSTALL.md                  # インストール詳細ガイド
├── MAC_SETUP.md                # Mac専用セットアップガイド
├── PAGINATION.md               # 全ページ自動巡回機能ガイド
├── MULTIPLE_DOWNLOADS.md       # 複数ダウンロードリンク対応ガイド
├── UI_GUIDE.md                 # Unity UI詳細ガイド
├── AUTO_DOWNLOAD.md            # 自動ダウンロード特定機能ガイド
├── REQUIREMENTS_CHECK.md       # 要件定義チェックリスト
├── REQUIREMENTS_COMPARISON.md  # 要件定義との比較表
└── LICENSE                     # ライセンス情報
```

### 📝 ディレクトリの説明

| ディレクトリ | 説明 | Git管理 |
|------------|------|---------|
| `Bridge/` | Node.jsローカルサーバー | ✅ 管理対象 |
| `BoothExtension/` | Chrome/Edge拡張機能 | ✅ 管理対象 |
| `UnityExtension/` | Unity Editor拡張 | ✅ 管理対象 |
| `BoothBridge/` | 実行時データ保存先 | ❌ .gitignore |
| `Assets/ImportedAssets/` | アセット展開先 | ❌ .gitignore |
| `node_modules/` | Node.js依存関係 | ❌ .gitignore |

---

## 🔄 動作フロー

```
1. Unity「同期」ボタン
   ↓
2. Bridgeが起動（Node.js）
   ↓
3. BOOTHページが開く
   ↓
4. 拡張機能がDOM解析
   ↓
5. Bridgeへ自動POST
   ↓
6. JSON保存・サムネDL
   ↓
7. Unity UIが自動更新
```

---

## ⚙️ 保存先

### Unity プロジェクト内

- `<ProjectRoot>/BoothBridge/booth_assets.json` - 購入リストデータ
- `<ProjectRoot>/BoothBridge/thumbnails/` - サムネイル画像
- `<ProjectRoot>/BoothBridge/booth_assets.backup.json` - バックアップ（1世代）
- `<ProjectRoot>/Assets/ImportedAssets/booth_<ID>/` - 展開されたアセット

---

## 🐛 トラブルシューティング

### Bridgeが起動しない

**原因**: Node.jsがインストールされていない、またはPATHが通っていない

**対処（Windows）**:
1. コマンドプロンプトで `node --version` を実行
2. エラーが出る場合は Node.js を再インストール
3. Unity を再起動

**対処（Mac）**:
1. ターミナルで `node --version` を実行
2. エラーが出る場合は以下を確認：
   - Homebrew経由: `brew install node`
   - 公式サイトからインストール: https://nodejs.org/
3. Unity を再起動

### ブラウザ拡張が動作しない

**原因**: 拡張機能が正しくインストールされていない

**対処**:
1. `chrome://extensions/` でインストール状態を確認
2. エラーが表示されている場合は、拡張機能を削除して再インストール
3. デベロッパーモードが有効になっているか確認

### 同期ボタンを押してもデータが表示されない

**原因**: BOOTHページの構造が変更された可能性

**対処**:
1. ブラウザの開発者ツール（F12）を開く
2. Console タブで `[BOOTH Import]` のログを確認
3. エラーが表示されている場合は、Issue を報告

### ZIP自動展開が動作しない

**原因**: ファイル名が規定の形式になっていない

**対処**:
1. ダウンロードしたZIPファイル名を確認
2. `booth_<商品ID>.zip` の形式に手動でリネーム
3. 「ダウンロード」フォルダに配置
   - Windows: `C:\Users\<ユーザー名>\Downloads`
   - Mac: `/Users/<ユーザー名>/Downloads`

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

## 📝 ライセンス

MIT License

---

## 🛠️ 今後の開発予定

- v1.0: 基本機能 + Mac対応 ✅
- v1.1: FileSystemWatcherによるリアルタイム更新 ✅
- v1.2: キャッシュ削除ボタン / タグ分類
- v2.0: Linux対応
- v2.1: 自動ポート選択
- v2.2: カテゴリフィルタ・検索機能

---

## 💬 サポート

Issue・Pull Request をお待ちしています。

---

## ⚠️ 免責事項

本ツールは非公式ツールです。BOOTHの仕様変更により動作しなくなる可能性があります。
本ツールの使用により生じたいかなる損害についても、作者は一切の責任を負いません。

---

**Enjoy your VRChat creation! 🎨**

