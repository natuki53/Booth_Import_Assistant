# Mac用セットアップガイド

このガイドはmacOS向けの詳細なセットアップ手順を説明します。

---

## 📋 前提条件

- macOS 11 (Big Sur) 以降
- Unity 2021 LTS 〜 2022 LTS for Mac
- インターネット接続

---

## 🔧 ステップ1: Node.jsのインストール

### 方法1: Homebrewを使用（推奨）

1. **Homebrewがインストールされていない場合**

ターミナルを開き、以下を実行：

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

2. **Node.jsをインストール**

```bash
brew install node
```

3. **確認**

```bash
node --version
npm --version
```

両方とも正しく表示されればOKです。

### 方法2: 公式インストーラー

1. https://nodejs.org/ にアクセス
2. 「LTS版」をダウンロード
3. `.pkg` ファイルを実行してインストール
4. ターミナルで確認：

```bash
node --version
```

---

## 🔧 ステップ2: Bridgeのセットアップ

1. **ターミナルでプロジェクトフォルダに移動**

```bash
cd /Users/<ユーザー名>/Documents/Corsor/Project/Booth_Import_Assistant
```

2. **Bridgeフォルダに移動**

```bash
cd Bridge
```

3. **依存関係をインストール**

```bash
npm install
```

成功すると `node_modules` フォルダが作成されます。

4. **動作確認（オプション）**

```bash
node bridge.js --projectPath "/Users/<ユーザー名>/Documents/UnityProjects/TestProject"
```

以下が表示されればOK：

```
[BoothBridge] Bridge起動に成功しました
[BoothBridge] OS: darwin arm64
[BoothBridge] プロジェクトパス: /Users/.../TestProject
[BoothBridge] ダウンロードフォルダ監視: /Users/.../Downloads
[BoothBridge] HTTPサーバー起動完了: http://localhost:4823
```

`Ctrl+C` で終了してください。

---

## 🔧 ステップ3: Unity拡張のインポート

1. **Unityプロジェクトを開く**

Unity Hub から既存プロジェクトを開くか、新規作成します。

2. **UnityExtensionフォルダをコピー**

Finder で `UnityExtension` フォルダをUnityプロジェクトの `Assets` フォルダ内にドラッグ＆ドロップします。

推奨配置：
```
Assets/BoothImportAssistant/Editor/
```

3. **コンパイル確認**

Unity Editorが自動的にスクリプトをコンパイルします。
Consoleにエラーが表示されないことを確認してください。

4. **メニュー確認**

Unity上部メニューから `Tools > BOOTH Library` が表示されればOKです。

---

## 🔧 ステップ4: ブラウザ拡張のインストール

1. **Chrome または Edge を開く**

2. **拡張機能ページにアクセス**

```
chrome://extensions/
```

または

```
edge://extensions/
```

3. **デベロッパーモードを有効化**

右上のトグルスイッチをONにします。

4. **拡張機能を読み込む**

「パッケージ化されていない拡張機能を読み込む」ボタンをクリックし、
`BoothExtension` フォルダを選択します。

5. **インストール確認**

拡張機能一覧に「BOOTH Import Assistant」が表示されればOKです。

---

## ✅ 動作確認

### 1. Unityで同期テスト

1. Unity で `Tools > BOOTH Library` を開く
2. 「同期」ボタンをクリック
3. Consoleに以下が表示されることを確認：

```
[BoothBridge] BridgeManager初期化完了
[BoothBridge] Bridge起動に成功しました
[BoothBridge] OS: darwin arm64
```

4. ブラウザでBOOTHページが開く
5. 数秒後、Unity の BOOTH Library に購入リストが表示される

### 2. ZIPインポートテスト

1. BOOTH Library で「ダウンロード」ボタンをクリック
2. BOOTHページからZIPをダウンロード
3. ファイル名を `booth_<商品ID>.zip` に変更
4. `~/Downloads` フォルダに配置
5. 自動的に `Assets/ImportedAssets/booth_<ID>/` に展開される

---

## 🐛 トラブルシューティング（Mac固有）

### Node.jsが見つからない

**症状**: 「Node.jsがインストールされているか確認してください」

**対処**:

1. ターミナルで確認：

```bash
which node
```

パスが表示されればインストール済みです。

2. パスが表示されない場合：

```bash
# Homebrewでインストール
brew install node

# またはシンボリックリンクを作成
sudo ln -s /usr/local/bin/node /usr/bin/node
```

3. Unityを再起動

### Permission denied エラー

**症状**: Bridgeやスクリプト実行時にPermission deniedが表示される

**対処**:

```bash
chmod +x Bridge/bridge.js
```

### Homebrew（Apple Silicon）でNode.jsが見つからない

**症状**: M1/M2 MacでHomebrewインストール後も `node: command not found`

**対処**:

Apple Silicon Mac では Homebrew のパスが異なります。

```bash
# .zshrc または .bash_profile に追加
echo 'export PATH="/opt/homebrew/bin:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

その後、Unityを再起動してください。

### Unity Consoleに日本語が文字化けする

**症状**: ログが文字化けする

**対処**:

現在のバージョンでは影響なしですが、気になる場合はシステム環境設定で言語を確認してください。

---

## 🔒 セキュリティ（Mac固有）

### Gatekeeper

初回実行時に「開発元が未確認」と表示される場合：

1. システム環境設定 > セキュリティとプライバシー
2. 「このまま開く」をクリック

ただし、本ツールはNode.jsスクリプトなので通常は表示されません。

### Firefallの確認

Bridgeが起動すると、Firewall（ファイアウォール）の確認ダイアログが表示される場合があります。

- 「プライベートネットワークでのアクセスを許可」を選択
- localhost限定なので外部からのアクセスはありません

---

## 📝 Mac特有の注意事項

### ダウンロードフォルダ

Macのダウンロードフォルダは：

```
/Users/<ユーザー名>/Downloads
```

Safari、Chrome、Edgeすべて同じフォルダを使用します。

### パスの違い

- Windows: `C:\Users\...\`
- Mac: `/Users/.../`

パス指定時は `/` を使用してください（Unityが自動変換します）。

### ファイル名の大文字・小文字

Macはファイル名の大文字・小文字を区別しません（通常設定）。
`booth_1234567.zip` と `BOOTH_1234567.zip` は同じファイルとして扱われます。

---

## 🚀 完了！

Mac版のセットアップが完了しました。

次は README.md の「使い方」セクションを参照して、
実際にBOOTHから購入リストを同期してください。

---

**Enjoy your VRChat creation on Mac! 🍎✨**

