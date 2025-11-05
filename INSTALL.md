# インストールガイド

## 前提条件

- Windows 10 以降 / macOS 11 (Big Sur) 以降
- Unity 2021 LTS 〜 2022 LTS
- インターネット接続（初回セットアップのみ）

---

## ステップ1: Node.jsのインストール

### 1-1. ダウンロード

https://nodejs.org/ にアクセスし、「LTS版」をダウンロードしてください。

### 1-2. インストール

ダウンロードしたインストーラーを実行し、デフォルト設定でインストールしてください。

### 1-3. 確認

**Windows**: コマンドプロンプトまたはPowerShellを開き、以下を実行：
```bash
node --version
```

**Mac**: ターミナルを開き、以下を実行：
```bash
node --version
```

`v18.0.0` 以上が表示されればOKです。

**Macでnode: command not foundが表示される場合**:
- Homebrewを使用している場合: `brew install node`
- または公式サイトから再インストール

---

## ステップ2: Bridgeのセットアップ

### 2-1. パッケージインストール

**Windows**: コマンドプロンプトまたはPowerShellで `Bridge` フォルダに移動し、以下を実行：
```bash
cd Bridge
npm install
```

**Mac**: ターミナルで `Bridge` フォルダに移動し、以下を実行：
```bash
cd Bridge
npm install
```

成功すると `node_modules` フォルダが生成されます。

### 2-2. 動作確認（オプション）

手動起動テスト：

```bash
node bridge.js --projectPath "C:/test"
```

以下が表示されればOK：

```
[BoothBridge] Bridge起動に成功しました
[BoothBridge] HTTPサーバー起動完了: http://localhost:4823
```

`Ctrl+C` で終了してください。

---

## ステップ3: Unity拡張のインポート

### 3-1. Unityプロジェクトを開く

既存のVRChatプロジェクトまたは新規プロジェクトを開きます。

### 3-2. 拡張機能をコピー

`UnityExtension` フォルダ全体を、Unityプロジェクトの `Assets` フォルダ内にコピーします。

推奨パス：
```
Assets/BoothImportAssistant/Editor/
```

### 3-3. コンパイル確認

Unity Editorが自動的にスクリプトをコンパイルします。
エラーが表示されないことを確認してください。

### 3-4. メニュー確認

Unity上部メニューから `Tools > BOOTH Library` が表示されればOKです。

---

## ステップ4: ブラウザ拡張のインストール

### 4-1. Chrome拡張機能ページを開く

Chrome または Edge で以下にアクセス：

```
chrome://extensions/
```

### 4-2. デベロッパーモードを有効化

右上のトグルスイッチをONにします。

### 4-3. 拡張機能を読み込む

「パッケージ化されていない拡張機能を読み込む」ボタンをクリックし、
`BoothExtension` フォルダを選択します。

### 4-4. インストール確認

拡張機能一覧に「BOOTH Import Assistant」が表示されればOKです。

---

## ステップ5: 初回動作確認

### 5-1. Unity でウィンドウを開く

Unity メニューから `Tools > BOOTH Library` を選択。

### 5-2. 同期テスト

1. 「同期」ボタンをクリック
2. Bridgeが起動（コンソールにログ表示）
3. ブラウザでBOOTHページが開く
4. 数秒後、Unityに購入リストが表示される

成功すれば、インストール完了です！

---

## トラブルシューティング

### Node.jsが見つからない

**症状**: 「Node.jsがインストールされているか確認してください」エラー

**対処（Windows）**:
1. コマンドプロンプトで `node --version` を実行
2. エラーが出る場合は、Node.jsを再インストール
3. 環境変数PATHにNode.jsのパスが含まれているか確認
4. Unity を再起動

**対処（Mac）**:
1. ターミナルで `node --version` を実行
2. エラーが出る場合:
   - Homebrew: `brew install node`
   - 公式: https://nodejs.org/ からインストール
3. ターミナルを再起動してPATHを更新
4. Unity を再起動

### ポート4823が使用中

**症状**: 「ポート4823は既に使用されています」エラー

**対処**:
1. タスクマネージャーで `node.exe` を終了
2. 他のBridgeプロセスが起動していないか確認
3. Unity を再起動

### 拡張機能がインストールできない

**症状**: 「マニフェストファイルが無効です」エラー

**対処**:
1. `BoothExtension/manifest.json` が存在するか確認
2. ファイルが破損していないか確認
3. Chrome/Edge を再起動

### 同期しても何も表示されない

**症状**: 同期ボタンを押しても一覧が空のまま

**対処**:
1. BOOTHにログインしているか確認
2. 購入済み商品があるか確認
3. ブラウザの開発者ツール（F12）でエラーを確認
4. プロジェクトフォルダ内の `BoothBridge/booth_assets.json` が生成されているか確認
   - Windows: `C:\<プロジェクトパス>\BoothBridge\booth_assets.json`
   - Mac: `/Users/<ユーザー名>/<プロジェクトパス>/BoothBridge/booth_assets.json`

---

## アンインストール方法

### Unity拡張

`Assets/BoothImportAssistant/` フォルダを削除

### ブラウザ拡張

`chrome://extensions/` から「削除」

### Bridge

`Bridge` フォルダを削除（またはそのまま保持）

### Node.js

コントロールパネルから「Node.js」をアンインストール

---

完了です。お疲れさまでした！ 🎉

