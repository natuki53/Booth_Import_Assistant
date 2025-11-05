# 自動ダウンロード特定機能ガイド（v0.1.0・未テスト）

## 📌 概要

v0.1.0（開発版）の実装では、**ファイル名の変更が不要**です。

> ⚠️ **注意**: この機能はまだ実際のBOOTHページでテストされていません。

従来は、ダウンロードしたZIPファイルを `booth_<id>.zip` にリネームする必要がありましたが、  
**Chrome拡張が自動的にファイルと商品IDを紐付ける**ため、元のファイル名のままで自動展開されます。

---

## 🚀 使い方

### 1. 同期（初回のみ）

Unity の BOOTH Library で「同期」ボタンをクリック
- BOOTHライブラリページが開く
- Chrome拡張が商品情報を自動取得
- **ダウンロードURLマップを作成**（商品ID ↔ ダウンロードURLの対応表）

### 2. ダウンロード

Unity の BOOTH Library でダウンロードボタンをクリック
- BOOTHのダウンロードページが開く
- **BOOTHのダウンロードボタンをクリックするだけ**
- ファイル名の変更は不要！

### 3. 自動展開

ダウンロード完了後、**自動的に**：
- Chrome拡張がダウンロードを検知
- ダウンロードURLから商品IDを特定
- Bridgeにファイル名と商品IDを通知
- Bridgeが正しいディレクトリに展開
- Unity の BOOTH Library に「✅ インポート済み」と表示

---

## 🔧 技術的な仕組み

### アーキテクチャ

```
┌─────────────┐
│   Unity     │ 1. ダウンロードボタンをクリック
│             │    Application.OpenURL(downloadUrl)
└──────┬──────┘
       │
       ↓
┌─────────────┐
│   Browser   │ 2. BOOTHページが開く
│             │    ユーザーがダウンロードボタンをクリック
└──────┬──────┘
       │
       ↓
┌──────────────────────────────────────────────┐
│  Chrome Extension (background.js)           │
│                                              │
│  3. downloads.onCreated                     │
│     → ダウンロード開始検知                     │
│     → downloadUrlMap から商品ID特定           │
│                                              │
│  4. downloads.onChanged                     │
│     → ダウンロード完了検知                     │
│     → Bridgeに通知（ファイル名 + 商品ID）       │
└──────┬───────────────────────────────────────┘
       │
       ↓
┌─────────────┐
│   Bridge    │ 5. ファイル監視
│  (Node.js)  │    → downloadTrackingMap から商品ID取得
│             │    → 正しいディレクトリに展開
└─────────────┘
```

---

## 📂 データフロー

### 1. 同期時（content.js → background.js）

```javascript
// content.js
function sendDownloadMapToBackground(items) {
  const downloadMap = {};
  
  for (const item of items) {
    if (item.downloadUrls && item.downloadUrls.length > 0) {
      downloadMap[item.id] = item.downloadUrls.map(dl => dl.url);
    }
  }
  
  chrome.runtime.sendMessage({
    type: 'UPDATE_DOWNLOAD_MAP',
    data: downloadMap
  });
}
```

**データ例:**
```json
{
  "booth_1234567": [
    "https://booth.pm/downloadables/111",
    "https://booth.pm/downloadables/222"
  ],
  "booth_7654321": [
    "https://booth.pm/downloadables/333"
  ]
}
```

### 2. ダウンロード開始時（background.js）

```javascript
chrome.downloads.onCreated.addListener(async (downloadItem) => {
  const url = downloadItem.url;
  
  // downloadUrlMapから商品ID検索
  for (const [mapUrl, info] of downloadUrlMap.entries()) {
    if (url.includes(mapUrl)) {
      boothId = info.boothId;
      downloadIndex = info.index;
      break;
    }
  }
  
  // 追跡開始
  downloadTracking.set(downloadItem.id, {
    boothId: boothId,
    downloadIndex: downloadIndex,
    filename: downloadItem.filename
  });
});
```

### 3. ダウンロード完了時（background.js → Bridge）

```javascript
chrome.downloads.onChanged.addListener(async (delta) => {
  if (delta.state && delta.state.current === 'complete') {
    const tracking = downloadTracking.get(delta.id);
    
    // Bridgeに通知
    await fetch('http://localhost:4823/download-notify', {
      method: 'POST',
      body: JSON.stringify({
        filename: tracking.filename,
        boothId: tracking.boothId,
        url: tracking.url
      })
    });
  }
});
```

### 4. Bridge側での処理

```javascript
// ダウンロード通知を受信
app.post('/download-notify', (req, res) => {
  const { filename, boothId } = req.body;
  
  // ファイル名と商品IDを紐付け
  downloadTrackingMap.set(filename, {
    boothId: boothId,
    timestamp: Date.now()
  });
});

// ダウンロードフォルダ監視
fs.watch(DOWNLOADS_DIR, async (eventType, filename) => {
  if (!filename.endsWith('.zip')) return;
  
  // downloadTrackingMapから商品ID取得
  if (downloadTrackingMap.has(filename)) {
    const tracking = downloadTrackingMap.get(filename);
    const boothId = tracking.boothId;
    
    // 展開
    await extractZip(zipPath, boothId, subFolder);
  }
});
```

---

## 🛡️ エラーハンドリング

### パターン1: downloadUrlMapに見つからない場合

```javascript
// フォールバック: URLパターンから抽出
const downloadMatch = url.match(/downloadables\/(\d+)/);
if (downloadMatch) {
  downloadId = downloadMatch[1];
  console.log('ダウンロードID:', downloadId);
}
```

### パターン2: Bridgeが起動していない場合

```javascript
try {
  await fetch(`${BRIDGE_URL}/download-notify`, { ... });
} catch (e) {
  console.error('Bridge通知エラー:', e);
  // エラーでも継続（次回同期時に再取得）
}
```

### パターン3: 古いエントリの削除

```javascript
// 1時間以上前のエントリを削除
const oneHourAgo = Date.now() - 60 * 60 * 1000;
for (const [key, value] of downloadTrackingMap.entries()) {
  if (value.timestamp < oneHourAgo) {
    downloadTrackingMap.delete(key);
  }
}
```

---

## 📋 権限について

### manifest.json

```json
{
  "permissions": [
    "activeTab",
    "downloads"  // ← v0.1.0 で追加
  ],
  "host_permissions": [
    "https://manage.booth.pm/*",
    "https://accounts.booth.pm/*",
    "https://booth.pm/*",          // ← v0.1.0 で追加
    "https://*.booth.pm/*"         // ← v0.1.0 で追加
  ],
  "background": {
    "service_worker": "background.js"  // ← v0.1.0 で追加
  }
}
```

### 必要な権限

1. **downloads**: ダウンロード監視に必要
   - `chrome.downloads.onCreated`
   - `chrome.downloads.onChanged`

2. **host_permissions**: BOOTHの全サブドメインにアクセス
   - ダウンロードファイルのURLが `booth.pximg.net` などの可能性があるため

---

## 🔍 デバッグ

### Chrome拡張のコンソール

1. `chrome://extensions/` を開く
2. BOOTH Import Assistant の「Service Worker」をクリック
3. 以下のログを確認：

```
[BOOTH Import BG] Background Service Worker 起動
[BOOTH Import BG] ダウンロードマップ更新: 50 商品
[BOOTH Import BG] URL登録完了: 75 個のURL
[BOOTH Import BG] ダウンロード開始: https://booth.pm/downloadables/123456
[BOOTH Import BG] マップから商品ID特定: booth_1234567 index: 0
[BOOTH Import BG] ダウンロード追跡開始: 42 → booth_1234567
[BOOTH Import BG] ダウンロード完了: example.zip
[BOOTH Import BG] Bridge通知成功
```

### Bridgeのコンソール

```
[BoothBridge] ダウンロード通知受信: example.zip
[BoothBridge] ダウンロード追跡登録: example.zip -> booth_1234567
[BoothBridge] ZIP検知（追跡登録）: example.zip → ID: booth_1234567
[BoothBridge] ZIP展開開始: booth_1234567
[BoothBridge] ZIP展開完了
```

---

## ⚠️ トラブルシューティング

### 問題1: ファイル名の変更を求められる

**原因**: Chrome拡張の更新が反映されていない

**解決策**:
1. `chrome://extensions/` を開く
2. BOOTH Import Assistant の「更新」ボタンをクリック
3. ページを再読み込み

### 問題2: 自動展開されない

**原因**: ダウンロードマップが作成されていない

**解決策**:
1. Unity で「同期」を再実行
2. Chrome拡張のコンソールで「URL登録完了」を確認

### 問題3: 複数ダウンロード時にサブフォルダに分かれない

**原因**: ダウンロードインデックスが取得できていない

**確認事項**:
- Chrome拡張のコンソールで `index: 0`, `index: 1` などが表示されているか
- Bridgeのコンソールで `variant_1`, `variant_2` などが表示されているか

---

## 📊 パフォーマンス

### メモリ使用量

- **downloadUrlMap**: 商品数 × ダウンロード数 × 約100バイト
  - 例: 100商品 × 3ダウンロード = 約30KB
- **downloadTracking**: 同時ダウンロード数 × 約200バイト
  - 例: 5個同時 = 約1KB

### 制限事項

- **downloadTrackingMap**: 1時間後に自動削除
- **downloadUrlMap**: 同期実行時に更新（永続化なし）

---

## 🎯 今後の改善予定

### v1.2

- [ ] ダウンロード進捗の可視化
- [ ] ダウンロード失敗時の再試行機能
- [ ] ダウンロード履歴の永続化

### v2.0

- [ ] Firefox対応
- [ ] Safari対応
- [ ] カスタムダウンロードフォルダ対応

---

## 📖 関連ドキュメント

- [README.md](README.md) - プロジェクト全体の概要
- [MULTIPLE_DOWNLOADS.md](MULTIPLE_DOWNLOADS.md) - 複数ダウンロード完全ガイド
- [CHANGELOG.md](CHANGELOG.md) - 変更履歴

