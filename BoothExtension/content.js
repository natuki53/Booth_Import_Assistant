/**
 * BOOTH Import Assistant - Content Script
 * 
 * BOOTH購入ライブラリページでDOM解析を実行し、
 * ローカルBridgeサーバーへ自動送信します。
 */

const BRIDGE_URL = 'http://localhost:4823/sync';
const WAIT_TIME = 3000; // DOM読み込み待機時間（ms）

console.log('[BOOTH Import] Content Script 読み込み完了');

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
  const allItems = [];
  const processedIds = new Set(); // 全ページ通しての重複除去
  
  try {
    // 現在のページから全ページ数を取得
    const totalPages = getTotalPages(document);
    
    console.log('[BOOTH Import] 全ページ巡回開始:', totalPages, 'ページ');
    
    // 現在のページ（1ページ目）を解析
    console.log('[BOOTH Import] 現在のページを解析中...');
    if (typeof showProgressNotification === 'function') {
      showProgressNotification(`🔄 ページ 1/${totalPages} を取得中...`);
    }
    
    const currentPageItems = extractBoothItemsFromDOM(document, processedIds);
    allItems.push(...currentPageItems);
    
    // 2ページ目以降を取得して解析
    for (let page = 2; page <= totalPages; page++) {
      // レート制限を考慮して少し待機
      await new Promise(resolve => setTimeout(resolve, 500));
      
      if (typeof showProgressNotification === 'function') {
        showProgressNotification(`🔄 ページ ${page}/${totalPages} を取得中...`);
      }
      
      const pageDoc = await fetchPageDOM(page);
      
      if (pageDoc) {
        const pageItems = extractBoothItemsFromDOM(pageDoc, processedIds);
        allItems.push(...pageItems);
        console.log('[BOOTH Import] ページ', page, '/', totalPages, '完了 -', pageItems.length, '件取得（累計', allItems.length, '件）');
      } else {
        console.warn('[BOOTH Import] ページ', page, 'の取得に失敗');
      }
    }
    
    console.log('[BOOTH Import] 全ページ解析完了:', allItems.length, '件（重複除去後）');
    
    if (typeof showProgressNotification === 'function') {
      showProgressNotification(`✅ 全${totalPages}ページ取得完了 - ${allItems.length}件`);
    }
    
  } catch (e) {
    console.error('[BOOTH Import] 全ページ取得エラー:', e);
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
  try {
    console.log('[BOOTH Import] Bridge送信開始:', items.length, '件');
    
    const response = await fetch(BRIDGE_URL, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(items)
    });
    
    if (response.ok) {
      const result = await response.json();
      console.log('[BOOTH Import] 同期完了:', result);
      
      // ページ上部に成功メッセージを表示
      showNotification('✅ Unityへの同期が完了しました！', 'success');
    } else {
      console.error('[BOOTH Import] Bridge応答エラー:', response.status);
      showNotification('❌ Bridgeへの接続に失敗しました', 'error');
    }
  } catch (e) {
    console.error('[BOOTH Import] Bridge送信エラー:', e);
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
  try {
    // 商品IDとダウンロードURLの対応マップを作成
    const downloadMap = {};
    
    for (const item of items) {
      if (item.downloadUrls && item.downloadUrls.length > 0) {
        downloadMap[item.id] = item.downloadUrls.map(dl => dl.url);
      }
    }
    
    // background.jsに送信
    chrome.runtime.sendMessage({
      type: 'UPDATE_DOWNLOAD_MAP',
      data: downloadMap
    }, (response) => {
      if (chrome.runtime.lastError) {
        console.warn('[BOOTH Import] Background通信エラー:', chrome.runtime.lastError);
      } else {
        console.log('[BOOTH Import] ダウンロードマップ送信完了:', Object.keys(downloadMap).length, '商品');
      }
    });
    
  } catch (e) {
    console.error('[BOOTH Import] ダウンロードマップ送信エラー:', e);
  }
}

/**
 * 自動同期処理（非同期対応）
 */
async function autoSync() {
  // URLチェック（manage.booth.pm または accounts.booth.pm）
  const validHosts = ['manage.booth.pm', 'accounts.booth.pm'];
  if (!validHosts.includes(location.hostname)) {
    console.log('[BOOTH Import]', location.hostname, 'では動作しません');
    return;
  }
  
  if (!location.pathname.startsWith('/library')) {
    console.log('[BOOTH Import] 購入ライブラリページでのみ動作します');
    return;
  }
  
  console.log('[BOOTH Import] 自動同期開始');
  
  // DOM読み込み待機
  await new Promise(resolve => setTimeout(resolve, WAIT_TIME));
  
  try {
    // 全ページ取得開始
    showProgressNotification('🔄 BOOTH商品を取得中...');
    
    const items = await extractBoothItems();
    
    hideProgressNotification();
    
    if (items.length === 0) {
      console.warn('[BOOTH Import] 商品が見つかりませんでした');
      showNotification('⚠️ 商品が見つかりませんでした。ページを更新してください。', 'error');
      return;
    }
    
    await syncToBridge(items);
    
    // ダウンロードURLマップをbackground.jsに送信
    sendDownloadMapToBackground(items);
    
  } catch (e) {
    hideProgressNotification();
    console.error('[BOOTH Import] 同期エラー:', e);
    showNotification('❌ 同期中にエラーが発生しました', 'error');
  }
}

// ページ読み込み完了時に実行
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', autoSync);
} else {
  autoSync();
}

