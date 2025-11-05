# 全ページ自動巡回機能ガイド

BOOTH Import Assistant v1.0では、購入ライブラリが複数ページに分かれている場合でも、**全ページを自動的に巡回して取得**します。

---

## 📋 機能概要

### 従来の問題点

BOOTHの購入ライブラリは、商品数が多い場合、複数ページに分割されます：
- 1ページ目: 商品1〜20
- 2ページ目: 商品21〜40
- 3ページ目: 商品41〜60
- ...

従来の方法では、各ページを手動で開いて取得する必要がありました。

### 自動巡回機能

本ツールは、**自動的に全ページを巡回**して、すべての商品情報を取得します。

---

## 🔄 動作フロー

### 1. ページ数の自動検出

```javascript
// ページネーション要素を解析
const paginationLinks = document.querySelectorAll('a[href*="page="]');

// 最大ページ番号を取得
let maxPage = 1;
paginationLinks.forEach((link) => {
  const match = link.href.match(/[?&]page=(\d+)/);
  if (match) {
    const pageNum = parseInt(match[1], 10);
    if (pageNum > maxPage) maxPage = pageNum;
  }
});
```

### 2. 各ページの順次取得

```javascript
// 現在のページ（1ページ目）を解析
const page1Items = extractBoothItemsFromDOM(document);

// 2ページ目以降を fetch
for (let page = 2; page <= totalPages; page++) {
  const pageDoc = await fetchPageDOM(page);
  const pageItems = extractBoothItemsFromDOM(pageDoc);
  allItems.push(...pageItems);
}
```

### 3. 重複除去と統合

```javascript
// 全ページ通しての重複除去
const processedIds = new Set();

itemLinks.forEach((link) => {
  const boothId = `booth_${productId}`;
  
  // 重複チェック
  if (processedIds.has(boothId)) return;
  processedIds.add(boothId);
  
  // 商品情報を追加
  allItems.push(item);
});
```

---

## 🎮 使用例

### Unity からの自動同期

1. Unity で「同期」ボタンをクリック
2. BOOTHページが開く
3. 画面右上に進捗表示：
   ```
   🔄 ページ 1/10 を取得中...
   🔄 ページ 2/10 を取得中...
   🔄 ページ 3/10 を取得中...
   ...
   ✅ 全10ページ取得完了 - 187件
   ```
4. Unity に全商品が表示される

### ブラウザコンソールでの手動実行

```javascript
// 全ページ取得
const items = await extractBoothItems();
console.log('取得件数:', items.length);

// JSONファイルとして保存
saveBoothLibraryJSON(items);
```

---

## ⚡ パフォーマンス

### 取得時間の目安

| ページ数 | 取得時間 |
|---------|---------|
| 1ページ | 即座 |
| 5ページ | 約3秒 |
| 10ページ | 約6秒 |
| 20ページ | 約11秒 |
| 50ページ | 約26秒 |
| 100ページ | 約51秒 |

**計算式:** `(ページ数 - 1) × 0.5秒 + 処理時間`

### レート制限対策

各ページ取得の間に **0.5秒の待機時間** を設けています：

```javascript
await new Promise(resolve => setTimeout(resolve, 500));
```

これにより、BOOTHサーバーへの負荷を軽減し、安定した取得を実現しています。

---

## 🔍 ページ数検出の仕組み

### パターン1: ページネーションリンク

```html
<!-- BOOTHのページネーション例 -->
<a href="?page=1">1</a>
<a href="?page=2">2</a>
<a href="?page=3">3</a>
```

→ `page=` パラメータから最大ページ番号を取得

### パターン2: ページ番号テキスト

```html
<!-- ページ表示例 -->
<div class="pagination">
  1 / 10
</div>
```

→ `X / Y` パターンから総ページ数を取得

### 複数パターンの併用

両方のパターンを確認し、より大きい値を採用：

```javascript
let maxPage = 1;

// パターン1: リンクから
paginationLinks.forEach(...);

// パターン2: テキストから
const match = text.match(/(\d+)\s*\/\s*(\d+)/);

// 最大値を採用
return Math.max(maxPageFromLinks, maxPageFromText);
```

---

## 🎯 メリット

### ✅ 手動操作不要

各ページを開いて手動でコピーする必要がありません。

### ✅ 取りこぼし防止

全ページを確実に取得するため、商品の取りこぼしがありません。

### ✅ 重複除去

複数ページに同じ商品が表示されていても、自動的に重複を除去します。

### ✅ 進捗表示

画面右上に進捗が表示されるため、待ち時間がわかりやすい。

### ✅ エラー耐性

特定のページの取得に失敗しても、他のページの取得を継続します。

---

## ⚠️ 注意事項

### ページ数が多い場合

100ページ以上ある場合、取得に1分以上かかることがあります。
その場合は以下を推奨：

1. **現在のページのみ取得（高速版）を使用**

```javascript
// ブラウザコンソールで実行
const items = extractBoothItemsCurrentPageOnly();
console.log('現在のページ:', items.length, '件');
```

2. **手動でページ範囲を指定**（将来実装予定）

```javascript
// 将来的にはページ範囲指定が可能に
const items = await extractBoothItems({ startPage: 1, endPage: 10 });
```

### ネットワーク環境

不安定なネットワーク環境では、途中で失敗する可能性があります。
その場合は再実行してください。

### BOOTHのDOM構造変更

BOOTHのページ構造が変更された場合、ページ数検出が正しく動作しない可能性があります。
その場合は、`getTotalPages()` 関数を更新してください。

---

## 🔧 カスタマイズ

### 待機時間の変更

レート制限をより慎重にしたい場合：

```javascript
// content.js内（215行目付近）
await new Promise(resolve => setTimeout(resolve, 1000)); // 500ms → 1000ms
```

### デバッグモード

詳細なログを出力したい場合：

```javascript
// content.js内に追加
console.log('[DEBUG] ページ', page, 'のURL:', url);
console.log('[DEBUG] 取得したHTML:', html.substring(0, 100));
console.log('[DEBUG] 解析結果:', pageItems);
```

---

## 🐛 トラブルシューティング

### Q: ページ数が1のままになる

**A:** ページネーション要素が見つかっていない可能性があります。

```javascript
// 手動確認
const paginationLinks = document.querySelectorAll('a[href*="page="]');
console.log('ページネーションリンク:', paginationLinks.length);
```

### Q: 途中でエラーが出る

**A:** ネットワークエラーの可能性があります。コンソールで確認：

```javascript
[BOOTH Import] ページ取得エラー: 5 Error: HTTP 500
```

→ 該当ページをスキップして続行します

### Q: 取得した件数が予想より少ない

**A:** 重複除去により、実際の商品数より少なくなります。

```javascript
// 重複を確認
const items = await extractBoothItems();
const uniqueIds = new Set(items.map(item => item.id));
console.log('取得件数:', items.length);
console.log('ユニークID数:', uniqueIds.size);
```

---

## 📊 統計情報

### ログ出力例

```
[BOOTH Import] 全ページ巡回開始: 10 ページ
[BOOTH Import] 現在のページを解析中...
[BOOTH Import] 商品リンク検出: 18 件
[BOOTH Import] ページ 2 / 10 完了 - 20 件取得（累計 38 件）
[BOOTH Import] ページ 3 / 10 完了 - 20 件取得（累計 58 件）
...
[BOOTH Import] ページ 10 / 10 完了 - 7 件取得（累計 187 件）
[BOOTH Import] 全ページ解析完了: 187 件（重複除去後）
```

### 進捗表示

画面右上に青い通知が表示されます：

```
🔄 ページ 1/10 を取得中...
🔄 ページ 2/10 を取得中...
...
✅ 全10ページ取得完了 - 187件
```

---

この機能により、数百件の購入履歴がある場合でも、ワンクリックで全商品を取得できます！🚀

