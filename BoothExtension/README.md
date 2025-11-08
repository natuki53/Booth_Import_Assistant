# BOOTH Import Assistant - Browser Extension

## インストール方法

1. Chrome または Edge を開く
2. `chrome://extensions/` にアクセス
3. 右上の「デベロッパーモード」を有効化
4. 「パッケージ化されていない拡張機能を読み込む」をクリック
5. このフォルダ（BoothExtension）を選択

## 使い方

1. Unity で BOOTH Import Assistant を起動し、「同期」ボタンを押す
2. ブラウザでBOOTH購入ライブラリが開かれる
   - `https://accounts.booth.pm/library` または
   - `https://manage.booth.pm/library`
3. ページ読み込み完了後、自動的にデータが Unity に送信される
4. 成功すると右上に「✅ Unityへの同期が完了しました！」が表示される

## 対応ページ

- `https://accounts.booth.pm/library`（推奨）
- `https://manage.booth.pm/library`

## 主な機能

### 1. 購入リスト自動取得
#### extractBoothItems()
**全ページを自動巡回**して購入済み商品を抽出します。

- **全ページ自動取得**: ページネーションを検出して全ページを巡回
- 商品ID、タイトル、作者名を自動抽出
- サムネイルURLとダウンロードURLを取得（複数対応）
- 重複を自動除去
- 進捗表示（画面右上）

**使用例:**
```javascript
// 全ページ取得（非同期）
const items = await extractBoothItems();
console.log('取得件数:', items.length);
```

### 2. 自動ダウンロード特定機能（v0.1.0 機能）

#### background.js (Service Worker)
Chrome の `downloads` API を使用して、ダウンロードを自動的に監視・特定します。

**機能:**
- **ダウンロードURLマップ**: 同期時に商品IDとダウンロードURLの対応を記録
- **ダウンロード監視**: ダウンロード開始・完了を自動検知
- **商品ID特定**: ダウンロードURLから商品IDを自動特定
- **Bridge通知**: ファイル名と商品IDをBridgeに自動送信

**ユーザーのメリット:**
- ファイル名の変更が不要
- ダウンロードするだけで自動的にUnityに展開
- 複数バリエーションも自動的にサブフォルダに分類

**技術詳細:**
```javascript
// ダウンロード開始時
chrome.downloads.onCreated.addListener((downloadItem) => {
  // downloadUrlMapから商品IDを特定
  const boothId = findBoothIdFromUrl(downloadItem.url);
  // 追跡開始
  downloadTracking.set(downloadItem.id, { boothId, filename: ... });
});

// ダウンロード完了時
chrome.downloads.onChanged.addListener((delta) => {
  if (delta.state.current === 'complete') {
    // Bridgeに通知
    notifyBridgeDownload(tracking);
  }
});
```

### extractBoothItemsCurrentPageOnly()
現在のページのみを高速で抽出します（デバッグ用）。

**使用例:**
```javascript
// 現在のページのみ取得（同期）
const items = extractBoothItemsCurrentPageOnly();
console.log('現在のページ:', items.length, '件');
```

### saveBoothLibraryJSON()
デバッグ用の関数です。抽出した商品データをJSONファイルとしてダウンロードします。

使い方（開発者ツールのコンソールで実行）:
```javascript
const items = extractBoothItems();
saveBoothLibraryJSON(items);
```

## デバッグ

詳細なデバッグ方法は [DEBUG.md](DEBUG.md) を参照してください。

## アイコンについて

`icon16.png`, `icon48.png`, `icon128.png` はプレースホルダーです。
実際の運用では適切なアイコン画像を配置してください。

詳細は [ICONS_README.txt](ICONS_README.txt) を参照してください。

