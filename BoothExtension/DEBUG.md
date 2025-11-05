# BOOTH Extension デバッグガイド

このガイドでは、BOOTH Import Assistantの拡張機能をテスト・デバッグする方法を説明します。

---

## 🧪 ブラウザコンソールでのテスト

### 1. BOOTHライブラリページを開く

https://accounts.booth.pm/library にアクセスしてログインしてください。

### 2. 開発者ツールを開く

- **Chrome/Edge**: `F12` または `Ctrl+Shift+I` (Mac: `Cmd+Option+I`)
- Consoleタブを選択

### 3. 手動で関数を実行

拡張機能が読み込まれていれば、以下のコマンドを実行できます：

#### 商品リスト抽出テスト

#### 全ページ自動取得（推奨）

```javascript
// 全ページを自動的に巡回して商品情報を抽出（非同期）
const items = await extractBoothItems();

// 結果を確認
console.log('抽出された商品数:', items.length);
console.table(items);
```

**特徴:**
- 購入リストの全ページを自動巡回
- ページネーションから自動的に総ページ数を検出
- 各ページを順番に取得してパース
- 重複を自動除去
- 進捗を画面右上に表示

#### 現在のページのみ取得（高速・デバッグ用）

```javascript
// 現在表示中のページのみを解析（同期）
const items = extractBoothItemsCurrentPageOnly();

// 結果を確認
console.log('抽出された商品数:', items.length);
console.table(items);
```

**特徴:**
- 現在のページのみを高速解析
- ページ遷移なし
- デバッグやテスト用

#### JSON保存テスト

**全ページ取得してJSON保存:**
```javascript
// 商品情報を抽出してJSONファイルとしてダウンロード
const items = await extractBoothItems();
saveBoothLibraryJSON(items);
```

**現在のページのみJSON保存:**
```javascript
// 高速版（現在のページのみ）
const items = extractBoothItemsCurrentPageOnly();
saveBoothLibraryJSON(items);
```

#### Bridge送信テスト

```javascript
// 商品情報を抽出してBridgeに送信
const items = await extractBoothItems();
await syncToBridge(items);
```

---

## 🔍 DOM構造の確認

### 現在のDOM構造（2025年10月版）

```html
<!-- 商品ブロックの例 -->
<div>
  <!-- サムネイル -->
  <a href="https://booth.pm/ja/items/6376404">
    <img src="https://booth.pximg.net/.../thumbnail.jpg" />
  </a>
  
  <!-- 商品タイトル -->
  <a href="https://booth.pm/ja/items/6376404">
    ✦動く配信画面 / Clear stars (クリア)
  </a>
  
  <!-- 作者名 -->
  <a href="https://tearie.booth.pm/">
    solisnotte (hanamori design)
  </a>
  
  <!-- ZIPファイル名 -->
  clearstars_clear_overlay_t02.zip
  
  <!-- ダウンロードリンク -->
  <a href="https://booth.pm/downloadables/5652034">
    ダウンロード
  </a>
</div>
```

### DOM構造変更時の対応

BOOTHのDOM構造が変更された場合、`extractBoothItems()` 関数を以下の手順で調整してください：

1. **要素を手動で確認**

```javascript
// すべての商品リンクを確認
document.querySelectorAll('a[href*="/items/"]').forEach((link, index) => {
  console.log(index, link.href, link.textContent.trim());
});

// すべての作者リンクを確認
document.querySelectorAll('a[href*=".booth.pm"]').forEach((link, index) => {
  if (!link.href.includes('/items/')) {
    console.log(index, link.href, link.textContent.trim());
  }
});
```

2. **親要素の構造を確認**

```javascript
// 最初の商品リンクの親要素を確認
const firstLink = document.querySelector('a[href*="/items/"]');
console.log('親要素:', firstLink.parentElement);
console.log('closest div:', firstLink.closest('div'));
```

3. **セレクタを調整**

取得できない情報がある場合、`extractBoothItems()` 関数内のセレクタを調整してください。

---

---

## 🔄 全ページ巡回機能について

### 動作の流れ

1. 現在のページでページネーション要素を検索
2. 全ページ数を自動検出
3. 現在のページ（通常1ページ目）を解析
4. 2ページ目以降を順次fetch
5. 各ページをDOMParserで解析
6. 全ページの結果を統合

### パフォーマンス

- ページあたり約0.5秒の待機時間
- 10ページなら約5-6秒
- 50ページなら約25-30秒
- 進捗は画面右上に表示

### ページ数検出の仕組み

```javascript
// ページネーションリンクから検出
const paginationLinks = document.querySelectorAll('a[href*="page="]');

// ページ番号テキストからも検出（例: "1 / 5"）
const pageTexts = document.querySelectorAll('.pagination');
```

---

## 🐛 トラブルシューティング

### 全ページ巡回が途中で止まる

**原因**: ネットワークエラーまたはレート制限

**対処**:
1. コンソールでエラーログを確認
2. 手動で再実行
3. 待機時間を増やす（500ms → 1000ms）

```javascript
// bridge.js内の待機時間を変更
await new Promise(resolve => setTimeout(resolve, 1000)); // 500から1000に
```

### ページ数が正しく検出されない

**原因**: BOOTHのDOM構造変更

**対処**:

```javascript
// 手動でページ数を確認
const totalPages = getTotalPages(document);
console.log('検出されたページ数:', totalPages);

// ページネーション要素を確認
const paginationLinks = document.querySelectorAll('a[href*="page="]');
console.log('ページネーションリンク:', paginationLinks.length);
paginationLinks.forEach((link, i) => {
  console.log(i, link.href);
});
```

### 商品が検出されない

**確認項目**:

1. ページが完全に読み込まれているか
2. ログインしているか
3. 購入済み商品があるか

**デバッグ**:

```javascript
// 商品リンクの数を確認
const links = document.querySelectorAll('a[href*="/items/"]');
console.log('商品リンク数:', links.length);

// 最初の5件を表示
links.forEach((link, i) => {
  if (i < 5) {
    console.log(i, link.href, link.textContent.trim());
  }
});
```

### 作者名が取得できない

**デバッグ**:

```javascript
// 作者リンクを確認
const authorLinks = document.querySelectorAll('a[href*=".booth.pm"]');
console.log('作者リンク数:', authorLinks.length);

authorLinks.forEach((link, i) => {
  if (i < 5 && !link.href.includes('/items/')) {
    console.log(i, link.href, link.textContent.trim());
  }
});
```

### サムネイルが取得できない

**デバッグ**:

```javascript
// すべての画像を確認
const images = document.querySelectorAll('img');
console.log('画像数:', images.length);

images.forEach((img, i) => {
  if (i < 5) {
    console.log(i, img.src);
  }
});
```

### Bridge送信エラー

**確認項目**:

1. Bridgeが起動しているか
2. `http://localhost:4823` にアクセスできるか
3. CORSエラーが出ていないか

**デバッグ**:

```javascript
// Bridge接続テスト
fetch('http://localhost:4823/sync', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify([{
    id: 'booth_test',
    title: 'テスト商品',
    author: 'テスト作者',
    productUrl: 'https://booth.pm/ja/items/123456',
    thumbnailUrl: '',
    downloadUrl: '',
    purchaseDate: '2025-10-27',
    localThumbnail: '',
    installed: false,
    importPath: '',
    notes: ''
  }])
})
.then(res => res.json())
.then(data => console.log('Bridge応答:', data))
.catch(err => console.error('Bridge接続エラー:', err));
```

---

## 📊 ログの見方

### 正常動作時のログ

```
[BOOTH Import] Content Script 読み込み完了
[BOOTH Import] 自動同期開始
[BOOTH Import] 商品リンク検出: 25 件
[BOOTH Import] 商品解析成功: { id: 'booth_6376404', title: '✦動く配信画面 / Clear st...', author: 'solisnotte (hanamori design)' }
[BOOTH Import] 商品解析成功: ...
[BOOTH Import] 解析完了: 20 件（重複除去後）
[BOOTH Import] Bridge送信開始: 20 件
[BOOTH Import] 同期完了: { success: true, count: 20 }
```

### エラー発生時のログ

```
[BOOTH Import] 商品が見つかりませんでした
⚠️ 商品が見つかりませんでした。ページを更新してください。
```

または

```
[BOOTH Import] Bridge送信エラー: TypeError: Failed to fetch
❌ Unityが起動していません。Bridgeを起動してから再試行してください。
```

---

## 🧑‍💻 開発時の便利なコマンド

### 拡張機能の再読み込み

拡張機能を更新した後：

1. `chrome://extensions/` を開く
2. 「BOOTH Import Assistant」の更新ボタン（🔄）をクリック
3. BOOTHページをリロード（F5）

### リアルタイムデバッグ

```javascript
// 監視モード（10秒ごとに実行）
setInterval(() => {
  const items = extractBoothItems();
  console.log('商品数:', items.length);
}, 10000);
```

### データ構造の確認

```javascript
// 最初の1件を詳細表示
const items = extractBoothItems();
if (items.length > 0) {
  console.log('最初の商品:', JSON.stringify(items[0], null, 2));
}
```

### 複数ダウンロードリンクの確認

```javascript
// 複数ダウンロードリンクを持つ商品を探す
const items = extractBoothItems();
const multiDownloads = items.filter(item => item.downloadUrls && item.downloadUrls.length > 1);

console.log('複数ダウンロードリンクを持つ商品:', multiDownloads.length, '件');
multiDownloads.forEach(item => {
  console.log(item.title, ':', item.downloadUrls.length, 'リンク');
  item.downloadUrls.forEach((dl, i) => {
    console.log('  ', i + 1, ':', dl.label, '→', dl.url);
  });
});
```

---

## 📝 テストチェックリスト

- [ ] 商品リンクが正しく検出される
- [ ] 商品IDが正しく抽出される
- [ ] 商品タイトルが正しく取得される
- [ ] 作者名が正しく取得される
- [ ] サムネイルURLが正しく取得される
- [ ] ダウンロードURLが正しく取得される
- [ ] 重複が除去される
- [ ] JSON形式が正しい
- [ ] Bridge送信が成功する
- [ ] エラーハンドリングが機能する

---

以上です。問題が発生した場合は、このガイドを参考にデバッグしてください。

