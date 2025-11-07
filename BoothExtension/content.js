/**
 * BOOTH Import Assistant - Content Script
 * 
 * BOOTH購入ライブラリページでDOM解析を実行し、
 * ローカルBridgeサーバーへ自動送信します。
 */

const BRIDGE_URL = 'http://localhost:4823/sync';
const WAIT_TIME = 3000; // DOM読み込み待機時間（ms）

// ログヘルパー関数
function logInfo(...args) {
  const timestamp = new Date().toISOString();
  console.log(`[${timestamp}][BOOTH-CS][INFO]`, ...args);
}

function logDebug(...args) {
  const timestamp = new Date().toISOString();
  console.log(`[${timestamp}][BOOTH-CS][DEBUG]`, ...args);
}

function logWarn(...args) {
  const timestamp = new Date().toISOString();
  console.warn(`[${timestamp}][BOOTH-CS][WARN]`, ...args);
}

function logError(...args) {
  const timestamp = new Date().toISOString();
  console.error(`[${timestamp}][BOOTH-CS][ERROR]`, ...args);
}

logInfo('=== Content Script 読み込み完了 ===');
logInfo('ページURL:', location.href);
logInfo('Bridge URL:', BRIDGE_URL);

/**
 * 指定されたDocumentオブジェクトから商品情報を解析（既存ロジック維持）
 */
function extractBoothItemsFromDOM(doc, processedIds = new Set()) {
  const items = [];
  
  try {
    // 商品リンク（/items/を含むリンク）をすべて取得
    const itemLinks = doc.querySelectorAll('a[href*="/items/"]');
    
    console.log('[BOOTH Import] 商品リンク検出:', itemLinks.length, '件');
    
    itemLinks.forEach((link) => {
      try {
        // 商品IDを抽出
        const match = link.href.match(/\/items\/(\d+)/);
        if (!match) return;
        
        const productId = match[1];
        const boothId = `booth_${productId}`;
        
        // 重複チェック
        if (processedIds.has(boothId)) {
          return;
        }
        processedIds.add(boothId);
        
        // 商品タイトル（リンクのテキスト）
        const title = link.textContent.trim() || '商品名不明';
        
        // 商品URL
        const productUrl = link.href;
        
        // 親要素または近隣要素から追加情報を取得
        const parentElement = link.closest('div, li, article, section') || link.parentElement;
        
        // 作者名（.booth.pmを含むリンクを探す）
        let author = '作者不明';
        if (parentElement) {
          const authorLink = parentElement.querySelector('a[href*=".booth.pm"]');
          if (authorLink && !authorLink.href.includes('/items/')) {
            author = authorLink.textContent.trim();
          }
        }
        
        // サムネイルURL（同じ親要素内のimg）
        let thumbnailUrl = '';
        if (parentElement) {
          const imgElement = parentElement.querySelector('img');
          if (imgElement && imgElement.src) {
            thumbnailUrl = imgElement.src;
          }
        }
        
        // ダウンロードURL（/downloadablesを含むリンク）- 複数対応
        let downloadUrls = [];
        if (parentElement) {
          const downloadLinks = parentElement.querySelectorAll('a[href*="/downloadables/"]');
          downloadLinks.forEach((link) => {
            if (link.href && !downloadUrls.some(dl => dl.url === link.href)) {
              // リンクテキストも取得（アバター名識別用）
              const linkText = link.textContent.trim();
              downloadUrls.push({
                url: link.href,
                label: linkText || 'ダウンロード'
              });
            }
          });
        }
        
        // 購入日（現在の日付）
        const purchaseDate = new Date().toISOString().split('T')[0];
        
        // 商品情報を追加
        const item = {
          id: boothId,
          title: title,
          author: author,
          productUrl: productUrl,
          thumbnailUrl: thumbnailUrl,
          downloadUrls: downloadUrls, // 配列形式
          purchaseDate: purchaseDate,
          localThumbnail: `BoothBridge/thumbnails/${boothId}.jpg`,
          installed: false,
          importPath: `Assets/ImportedAssets/${boothId}/`,
          notes: ''
        };
        
        items.push(item);
        
        console.log('[BOOTH Import] 商品解析成功:', {
          id: boothId,
          title: title.substring(0, 30) + (title.length > 30 ? '...' : ''),
          author: author,
          downloads: downloadUrls.length + '件'
        });
        
      } catch (e) {
        console.error('[BOOTH Import] 商品解析エラー:', e);
      }
    });
    
  } catch (e) {
    console.error('[BOOTH Import] DOM解析エラー:', e);
  }
  
  return items;
}

/**
 * ページネーションから全ページ数を取得
 */
function getTotalPages(doc) {
  try {
    // ページネーション要素を探す（BOOTHの実際の構造に応じて調整が必要）
    const paginationLinks = doc.querySelectorAll('a[href*="page="], .pagination a, nav a');
    
    let maxPage = 1;
    
    paginationLinks.forEach((link) => {
      const match = link.href.match(/[?&]page=(\d+)/);
      if (match) {
        const pageNum = parseInt(match[1], 10);
        if (pageNum > maxPage) {
          maxPage = pageNum;
        }
      }
    });
    
    // ページ番号を含むテキスト要素も確認（例: "1 / 5" など）
    const pageTexts = doc.querySelectorAll('.pagination, nav, [class*="page"]');
    pageTexts.forEach((elem) => {
      const text = elem.textContent;
      const match = text.match(/(\d+)\s*\/\s*(\d+)/);
      if (match) {
        const totalPages = parseInt(match[2], 10);
        if (totalPages > maxPage) {
          maxPage = totalPages;
        }
      }
    });
    
    console.log('[BOOTH Import] 検出されたページ数:', maxPage);
    return maxPage;
    
  } catch (e) {
    console.error('[BOOTH Import] ページ数取得エラー:', e);
    return 1;
  }
}

/**
 * 指定ページのHTMLを取得してDOMに変換
 */
async function fetchPageDOM(pageNum) {
  try {
    const url = `${location.origin}${location.pathname}?page=${pageNum}`;
    console.log('[BOOTH Import] ページ取得中:', pageNum, '→', url);
    
    const response = await fetch(url, {
      credentials: 'same-origin',
      headers: {
        'Accept': 'text/html'
      }
    });
    
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    
    const html = await response.text();
    const parser = new DOMParser();
    const doc = parser.parseFromString(html, 'text/html');
    
    return doc;
    
  } catch (e) {
    console.error('[BOOTH Import] ページ取得エラー:', pageNum, e);
    return null;
  }
}

/**
 * 全ページを巡回して商品情報を取得（メイン関数）
 */
async function extractBoothItems() {
  logInfo('=== 全ページ巡回開始 ===');
  
  const allItems = [];
  const processedIds = new Set(); // 全ページ通しての重複除去
  
  try {
    // 現在のページから全ページ数を取得
    const totalPages = getTotalPages(document);
    logInfo(`検出ページ数: ${totalPages}ページ`);
    
    if (totalPages === 0) {
      logWarn('ページ数が0です。ページ構造が変更された可能性があります。');
      return [];
    }
    
    // 現在のページ（1ページ目）を解析
    logInfo('現在のページ（1ページ目）を解析中...');
    if (typeof showProgressNotification === 'function') {
      showProgressNotification(`🔄 ページ 1/${totalPages} を取得中...`);
    }
    
    const currentPageItems = extractBoothItemsFromDOM(document, processedIds);
    allItems.push(...currentPageItems);
    logInfo(`ページ1完了: ${currentPageItems.length}件取得`);
    
    // 2ページ目以降を取得して解析
    for (let page = 2; page <= totalPages; page++) {
      // レート制限を考慮して少し待機
      logDebug(`ページ${page}取得前に500ms待機...`);
      await new Promise(resolve => setTimeout(resolve, 500));
      
      if (typeof showProgressNotification === 'function') {
        showProgressNotification(`🔄 ページ ${page}/${totalPages} を取得中...`);
      }
      
      logDebug(`ページ${page}/${totalPages}を取得中...`);
      const pageDoc = await fetchPageDOM(page);
      
      if (pageDoc) {
        const pageItems = extractBoothItemsFromDOM(pageDoc, processedIds);
        allItems.push(...pageItems);
        logInfo(`ページ${page}/${totalPages}完了: ${pageItems.length}件取得（累計${allItems.length}件）`);
      } else {
        logWarn(`⚠️ ページ${page}の取得に失敗しました`);
      }
    }
    
    logInfo('=== 全ページ解析完了 ===');
    logInfo(`総取得件数: ${allItems.length}件（重複除去後）`);
    logInfo(`処理済みID数: ${processedIds.size}個`);
    
    if (typeof showProgressNotification === 'function') {
      showProgressNotification(`✅ 全${totalPages}ページ取得完了 - ${allItems.length}件`);
    }
    
  } catch (e) {
    logError('=== 全ページ取得エラー ===');
    logError('エラーメッセージ:', e.message);
    logError('スタック:', e.stack);
  }
  
  return allItems;
}

/**
 * 現在のページのみを解析（デバッグ用・高速）
 */
function extractBoothItemsCurrentPageOnly() {
  console.log('[BOOTH Import] 現在のページのみを解析します');
  const processedIds = new Set();
  return extractBoothItemsFromDOM(document, processedIds);
}

/**
 * JSON保存用の補助関数（デバッグ用）
 */
function saveBoothLibraryJSON(items) {
  try {
    const blob = new Blob([JSON.stringify(items, null, 2)], { type: 'application/json' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = 'booth_library.json';
    link.click();
    console.log('[BOOTH Import] JSONファイルを保存しました');
  } catch (e) {
    console.error('[BOOTH Import] JSON保存エラー:', e);
  }
}

/**
 * Bridgeへ送信
 */
async function syncToBridge(items) {
  logInfo('=== Bridge送信開始 ===');
  logInfo(`送信データ件数: ${items.length}件`);
  
  try {
    // データサイズをログ
    const jsonString = JSON.stringify(items);
    const dataSizeKB = (jsonString.length / 1024).toFixed(2);
    logDebug(`データサイズ: ${dataSizeKB} KB`);
    
    logDebug('Bridge URL:', BRIDGE_URL);
    logDebug('送信開始...');
    
    const response = await fetch(BRIDGE_URL, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: jsonString
    });
    
    logDebug(`HTTPレスポンス: ${response.status} ${response.statusText}`);
    
    if (response.ok) {
      const result = await response.json();
      logInfo('✓ 同期完了');
      logInfo('レスポンス:', result);
      
      if (result.updated !== undefined) {
        logInfo(`  更新: ${result.updated}件`);
        logInfo(`  追加: ${result.added}件`);
        logInfo(`  サムネイルDL: ${result.thumbnails}件`);
      }
      
      // ページ上部に成功メッセージを表示
      showNotification('✅ Unityへの同期が完了しました！', 'success');
    } else {
      const errorText = await response.text().catch(() => '');
      logError('=== Bridge応答エラー ===');
      logError(`HTTPステータス: ${response.status} ${response.statusText}`);
      logError('レスポンスボディ:', errorText);
      showNotification('❌ Bridgeへの接続に失敗しました', 'error');
    }
  } catch (e) {
    logError('=== Bridge送信エラー ===');
    logError('エラータイプ:', e.name);
    logError('エラーメッセージ:', e.message);
    logError('スタック:', e.stack);
    
    if (e.name === 'TypeError' && e.message.includes('fetch')) {
      logError('');
      logError('🔴 Bridgeに接続できません');
      logError('原因: Bridgeが起動していないか、ポート4823が使用できません');
      logError('');
      logError('対処方法:');
      logError('  1. Unityを開く');
      logError('  2. Tools > BOOTH Library を開く');
      logError('  3. 「同期」ボタンを押してBridgeを起動');
      logError('  4. このページをリロードして再試行');
    }
    
    showNotification('❌ Unityが起動していません。Bridgeを起動してから再試行してください。', 'error');
  }
}

/**
 * 通知メッセージ表示
 */
function showNotification(message, type = 'info') {
  // 既存の通知を削除
  const existing = document.getElementById('booth-import-notification');
  if (existing) {
    existing.remove();
  }
  
  const notification = document.createElement('div');
  notification.id = 'booth-import-notification';
  notification.textContent = message;
  notification.style.cssText = `
    position: fixed;
    top: 20px;
    right: 20px;
    z-index: 10000;
    padding: 16px 24px;
    border-radius: 8px;
    font-size: 14px;
    font-weight: bold;
    color: white;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
    animation: slideIn 0.3s ease-out;
    ${type === 'success' ? 'background: #4CAF50;' : 'background: #f44336;'}
  `;
  
  document.body.appendChild(notification);
  
  // 5秒後に削除
  setTimeout(() => {
    notification.style.animation = 'slideOut 0.3s ease-out';
    setTimeout(() => notification.remove(), 300);
  }, 5000);
}

/**
 * アニメーション定義
 */
const style = document.createElement('style');
style.textContent = `
  @keyframes slideIn {
    from { transform: translateX(100%); opacity: 0; }
    to { transform: translateX(0); opacity: 1; }
  }
  @keyframes slideOut {
    from { transform: translateX(0); opacity: 1; }
    to { transform: translateX(100%); opacity: 0; }
  }
`;
document.head.appendChild(style);

/**
 * 進捗通知表示（更新可能）
 */
function showProgressNotification(message) {
  let notification = document.getElementById('booth-import-progress');
  
  if (!notification) {
    notification = document.createElement('div');
    notification.id = 'booth-import-progress';
    notification.style.cssText = `
      position: fixed;
      top: 20px;
      right: 20px;
      z-index: 10000;
      padding: 16px 24px;
      border-radius: 8px;
      font-size: 14px;
      font-weight: bold;
      color: white;
      background: #2196F3;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
      animation: slideIn 0.3s ease-out;
    `;
    document.body.appendChild(notification);
  }
  
  notification.textContent = message;
}

/**
 * 進捗通知を削除
 */
function hideProgressNotification() {
  const notification = document.getElementById('booth-import-progress');
  if (notification) {
    notification.style.animation = 'slideOut 0.3s ease-out';
    setTimeout(() => notification.remove(), 300);
  }
}

/**
 * ダウンロードURLマップをbackground.jsに送信
 */
function sendDownloadMapToBackground(items) {
  logInfo('=== ダウンロードマップ送信 ===');
  
  try {
    // 商品IDとダウンロードURLの対応マップを作成
    const downloadMap = {};
    let totalUrls = 0;
    
    for (const item of items) {
      if (item.downloadUrls && item.downloadUrls.length > 0) {
        downloadMap[item.id] = item.downloadUrls.map(dl => dl.url);
        totalUrls += item.downloadUrls.length;
      }
    }
    
    const productCount = Object.keys(downloadMap).length;
    logInfo(`マップ作成完了: ${productCount}商品, ${totalUrls}個のURL`);
    logDebug('マップ詳細:', downloadMap);
    
    // background.jsに送信
    logDebug('Background Scriptに送信中...');
    chrome.runtime.sendMessage({
      type: 'UPDATE_DOWNLOAD_MAP',
      data: downloadMap
    }, (response) => {
      if (chrome.runtime.lastError) {
        logWarn('⚠️ Background通信エラー:', chrome.runtime.lastError);
        logWarn('Background Scriptが起動していない可能性があります');
      } else {
        logInfo('✓ ダウンロードマップ送信完了');
        logDebug('レスポンス:', response);
      }
    });
    
  } catch (e) {
    logError('=== ダウンロードマップ送信エラー ===');
    logError('エラーメッセージ:', e.message);
    logError('スタック:', e.stack);
  }
}

/**
 * 自動同期処理（非同期対応）
 */
async function autoSync() {
  logInfo('=== 自動同期処理開始 ===');
  
  // URLチェック（manage.booth.pm または accounts.booth.pm）
  const validHosts = ['manage.booth.pm', 'accounts.booth.pm'];
  
  logDebug('現在のホスト:', location.hostname);
  logDebug('現在のパス:', location.pathname);
  
  if (!validHosts.includes(location.hostname)) {
    logWarn('このホストでは動作しません:', location.hostname);
    logWarn('有効なホスト:', validHosts.join(', '));
    return;
  }
  
  if (!location.pathname.startsWith('/library')) {
    logWarn('購入ライブラリページではありません:', location.pathname);
    logWarn('購入ライブラリページでのみ動作します');
    return;
  }
  
  logInfo('✓ ページ確認OK - 自動同期を開始します');
  
  // DOM読み込み待機
  logDebug(`DOM読み込み待機中（${WAIT_TIME}ms）...`);
  await new Promise(resolve => setTimeout(resolve, WAIT_TIME));
  logDebug('✓ DOM読み込み待機完了');
  
  try {
    // 全ページ取得開始
    logInfo('商品情報取得開始...');
    showProgressNotification('🔄 BOOTH商品を取得中...');
    
    const startTime = Date.now();
    const items = await extractBoothItems();
    const elapsedTime = ((Date.now() - startTime) / 1000).toFixed(1);
    
    hideProgressNotification();
    
    logInfo(`✓ 商品情報取得完了: ${items.length}件 (${elapsedTime}秒)`);
    
    if (items.length === 0) {
      logWarn('⚠️ 商品が見つかりませんでした');
      logWarn('原因考察:');
      logWarn('  - BOOTHで購入した商品がない');
      logWarn('  - ページ構造が変更された可能性');
      logWarn('  - DOM読み込みが完了していない');
      showNotification('⚠️ 商品が見つかりませんでした。ページを更新してください。', 'error');
      return;
    }
    
    // Bridge同期
    await syncToBridge(items);
    
    // ダウンロードURLマップをbackground.jsに送信
    sendDownloadMapToBackground(items);
    
    logInfo('=== 自動同期処理完了 ===');
    
  } catch (e) {
    hideProgressNotification();
    logError('=== 同期エラー ===');
    logError('エラーメッセージ:', e.message);
    logError('スタック:', e.stack);
    showNotification('❌ 同期中にエラーが発生しました', 'error');
  }
}

// ページ読み込み完了時に実行
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', autoSync);
} else {
  autoSync();
}

