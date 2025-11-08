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
  console.log('[BOOTH]', ...args);
}

function logWarn(...args) {
  console.warn('[BOOTH]', ...args);
}

function logError(...args) {
  console.error('[BOOTH]', ...args);
}

// Content Script 読み込み完了

/**
 * 指定されたDocumentオブジェクトから商品情報を解析（BOOTHの実際のHTML構造に完全対応）
 */
function extractBoothItemsFromDOM(doc, processedIds = new Set()) {
  const items = [];
  
  try {
    const itemCards = doc.querySelectorAll('div.mb-16.bg-white.p-16');
    
    if (itemCards.length === 0) {
      logWarn('商品カードが見つかりません');
      return [];
    }
    
    itemCards.forEach((card, index) => {
      try {
        
        const productLink = card.querySelector('a[target="_blank"][href*="/items/"]');
        if (!productLink) return;
        
        const match = productLink.href.match(/\/items\/(\d+)/);
        if (!match) return;
        
        const productId = match[1];
        const boothId = `booth_${productId}`;
        
        if (processedIds.has(boothId)) return;
        processedIds.add(boothId);
        
        const titleDiv = card.querySelector('div.text-text-default.font-bold.text-16, div.font-bold.text-16');
        const title = titleDiv ? titleDiv.textContent.trim() : '商品名不明';
        
        const thumbnailImg = card.querySelector('img.l-library-item-thumbnail');
        const thumbnailUrl = thumbnailImg ? thumbnailImg.src : '';
        
        let author = '作者不明';
        const authorDiv = card.querySelector('div.text-14.text-text-gray600.break-all');
        if (authorDiv) {
          author = authorDiv.textContent.trim();
        } else {
          const authorLink = card.querySelector('a[href*=".booth.pm"]:not([href*="/items/"])');
          if (authorLink) {
            const authorText = authorLink.querySelector('div.text-14');
            author = authorText ? authorText.textContent.trim() : authorLink.textContent.trim();
          }
        }
        
        let downloadUrls = [];
        const downloadContainers = card.querySelectorAll('div.mt-16');
        
        downloadContainers.forEach((container) => {
          const downloadLink = container.querySelector('a[href*="downloadables"]');
          
          if (downloadLink && !downloadUrls.some(dl => dl.url === downloadLink.href)) {
            let label = 'ダウンロード';
            
            const minWidthDiv = container.querySelector('div.min-w-0, div[class*="min-w"]');
            if (minWidthDiv) {
              const labelDiv = minWidthDiv.querySelector('div.text-14, div.text14, div[class*="text-14"], div[class*="text14"]');
              if (labelDiv) label = labelDiv.textContent.trim();
            }
            
            if (label === 'ダウンロード') {
              const labelDiv = container.querySelector('div.text-14, div.text14, div[class*="text-14"], div[class*="text14"]');
              if (labelDiv) label = labelDiv.textContent.trim();
            }
            
            if (label === 'ダウンロード') {
              let sibling = downloadLink.parentElement;
              for (let i = 0; i < 5 && sibling; i++) {
                sibling = sibling.previousElementSibling;
                if (sibling) {
                  const labelDiv = sibling.querySelector('div.text-14, div.text14, div[class*="text-14"], div[class*="text14"]');
                  if (labelDiv) {
                    label = labelDiv.textContent.trim();
                    break;
                  }
                }
              }
            }
            
            if (label === 'ダウンロード') {
              const linkText = downloadLink.textContent.trim();
              if (linkText && linkText !== '' && linkText.length < 100) {
                label = linkText;
              }
            }
            
            const labelLower = label.toLowerCase();
            const isMaterial = labelLower.includes('マテリアル') || 
                              labelLower.includes('まてりある') ||
                              labelLower.includes('material') ||
                              labelLower.includes('共通') ||
                              labelLower.includes('きょうつう') ||
                              labelLower.includes('common') ||
                              labelLower.includes('texture') ||
                              labelLower.includes('テクスチャ') ||
                              labelLower.includes('shader') ||
                              labelLower.includes('シェーダー') ||
                              labelLower.includes('mat_') ||
                              labelLower.includes('_mat') ||
                              labelLower.includes('textures') ||
                              labelLower.includes('materials');
            
            downloadUrls.push({
              url: downloadLink.href,
              label: label,
              isMaterial: isMaterial
            });
          }
        });
        
        if (downloadUrls.length === 0) {
          const allDownloadLinks = card.querySelectorAll('a[href*="downloadables"]');
          
          allDownloadLinks.forEach((dlLink) => {
            if (dlLink.href && !downloadUrls.some(dl => dl.url === dlLink.href)) {
              let label = 'ダウンロード';
              let parent = dlLink.parentElement;
              
              for (let i = 0; i < 5 && parent; i++) {
                const labelDiv = parent.querySelector('div.text-14, div.text14, div[class*="text-14"], div[class*="text14"]');
                if (labelDiv) {
                  label = labelDiv.textContent.trim();
                  break;
                }
                parent = parent.parentElement;
              }
              
              const labelLower = label.toLowerCase();
              const isMaterial = labelLower.includes('マテリアル') || 
                                labelLower.includes('material') ||
                                labelLower.includes('共通') ||
                                labelLower.includes('common');
              
              downloadUrls.push({
                url: dlLink.href,
                label: label,
                isMaterial: isMaterial
              });
            }
          });
        }
        
        downloadUrls.sort((a, b) => {
          if (a.isMaterial && !b.isMaterial) return 1;
          if (!a.isMaterial && b.isMaterial) return -1;
          return 0;
        });
        
        const purchaseDate = new Date().toISOString().split('T')[0];
        
        const item = {
          id: boothId,
          title: title,
          author: author,
          productUrl: productLink.href,
          thumbnailUrl: thumbnailUrl,
          downloadUrls: downloadUrls,
          purchaseDate: purchaseDate,
          localThumbnail: `BoothBridge/thumbnails/${boothId}.jpg`,
          installed: false,
          importPath: `Assets/ImportedAssets/${boothId}/`,
          notes: ''
        };
        
        items.push(item);
        
      } catch (e) {
        logError('商品カード解析エラー:', e.message);
      }
    });
    
  } catch (e) {
    logError('DOM解析エラー:', e.message);
  }
  
  return items;
}

/**
 * ページネーションから全ページ数を取得
 */
function getTotalPages(doc) {
  try {
    const paginationLinks = doc.querySelectorAll('a[href*="page="], .pagination a, nav a');
    let maxPage = 1;
    
    paginationLinks.forEach((link) => {
      const match = link.href.match(/[?&]page=(\d+)/);
      if (match) {
        const pageNum = parseInt(match[1], 10);
        if (pageNum > maxPage) maxPage = pageNum;
      }
    });
    
    const pageTexts = doc.querySelectorAll('.pagination, nav, [class*="page"]');
    pageTexts.forEach((elem) => {
      const text = elem.textContent;
      const match = text.match(/(\d+)\s*\/\s*(\d+)/);
      if (match) {
        const totalPages = parseInt(match[2], 10);
        if (totalPages > maxPage) maxPage = totalPages;
      }
    });
    
    return maxPage;
  } catch (e) {
    logError('ページ数取得エラー:', e);
    return 1;
  }
}

/**
 * 指定ページのHTMLを取得してDOMに変換
 */
async function fetchPageDOM(pageNum) {
  try {
    const url = `${location.origin}${location.pathname}?page=${pageNum}`;
    const response = await fetch(url, {
      credentials: 'same-origin',
      headers: { 'Accept': 'text/html' }
    });
    
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    
    const html = await response.text();
    const parser = new DOMParser();
    return parser.parseFromString(html, 'text/html');
  } catch (e) {
    logError('ページ取得エラー:', pageNum, e.message);
    return null;
  }
}

/**
 * 全ページを巡回して商品情報を取得（メイン関数）
 */
async function extractBoothItems() {
  const allItems = [];
  const processedIds = new Set();
  
  try {
    const totalPages = getTotalPages(document);
    if (totalPages === 0) return [];
    
    if (typeof showProgressNotification === 'function') {
      showProgressNotification(`🔄 ページ 1/${totalPages} を取得中...`);
    }
    
    const currentPageItems = extractBoothItemsFromDOM(document, processedIds);
    allItems.push(...currentPageItems);
    
    for (let page = 2; page <= totalPages; page++) {
      await new Promise(resolve => setTimeout(resolve, 500));
      
      if (typeof showProgressNotification === 'function') {
        showProgressNotification(`🔄 ページ ${page}/${totalPages} を取得中...`);
      }
      
      const pageDoc = await fetchPageDOM(page);
      if (pageDoc) {
        const pageItems = extractBoothItemsFromDOM(pageDoc, processedIds);
        allItems.push(...pageItems);
      }
    }
    
    if (typeof showProgressNotification === 'function') {
      showProgressNotification(`✅ 全${totalPages}ページ取得完了 - ${allItems.length}件`);
    }
  } catch (e) {
    logError('全ページ取得エラー:', e.message);
  }
  
  return allItems;
}

function extractBoothItemsCurrentPageOnly() {
  const processedIds = new Set();
  return extractBoothItemsFromDOM(document, processedIds);
}

function saveBoothLibraryJSON(items) {
  try {
    const blob = new Blob([JSON.stringify(items, null, 2)], { type: 'application/json' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = 'booth_library.json';
    link.click();
  } catch (e) {
    logError('JSON保存エラー:', e.message);
  }
}

async function syncToBridge(items) {
  try {
    const response = await fetch(BRIDGE_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(items)
    });
    
    if (response.ok) {
      const result = await response.json();
      logInfo(`同期完了: ${items.length}件 (更新:${result.updated}, 追加:${result.added})`);
      showNotification('✅ Unityへの同期が完了しました！', 'success');
    } else {
      logError('Bridge応答エラー:', response.status);
      showNotification('❌ Bridgeへの接続に失敗しました', 'error');
    }
  } catch (e) {
    logError('Bridge送信エラー:', e.message);
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

function sendDownloadMapToBackground(items) {
  try {
    const downloadMap = {};
    for (const item of items) {
      if (item.downloadUrls && item.downloadUrls.length > 0) {
        downloadMap[item.id] = item.downloadUrls.map(dl => dl.url);
      }
    }
    
    chrome.runtime.sendMessage({
      type: 'UPDATE_DOWNLOAD_MAP',
      data: downloadMap
    }, (response) => {
      if (chrome.runtime.lastError) {
        logWarn('Background通信エラー:', chrome.runtime.lastError.message);
      }
    });
  } catch (e) {
    logError('ダウンロードマップ送信エラー:', e.message);
  }
}

async function performSync() {
  const validHosts = ['manage.booth.pm', 'accounts.booth.pm'];
  
  if (!validHosts.includes(location.hostname) || !location.pathname.startsWith('/library')) {
    logWarn('BOOTHライブラリページではありません');
    return;
  }
  
  await new Promise(resolve => setTimeout(resolve, WAIT_TIME));
  
  try {
    logInfo('同期開始');
    showProgressNotification('🔄 BOOTH商品を取得中...');
    
    const items = await extractBoothItems();
    hideProgressNotification();
    
    if (items.length === 0) {
      logWarn('商品が見つかりませんでした');
      showNotification('⚠️ 商品が見つかりませんでした。ページを更新してください。', 'error');
      return;
    }
    
    logInfo(`取得完了: ${items.length}件`);
    await syncToBridge(items);
    sendDownloadMapToBackground(items);
  } catch (e) {
    hideProgressNotification();
    logError('同期エラー:', e.message);
    showNotification('❌ 同期中にエラーが発生しました', 'error');
  }
}

// ページ読み込み完了時の処理
// Unityから開かれた場合のみ同期（URLパラメータで判定）
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', checkAndSync);
} else {
  checkAndSync();
}

function checkAndSync() {
  const urlParams = new URLSearchParams(window.location.search);
  const shouldSync = urlParams.get('sync') === 'true';
  
  if (shouldSync) {
    if (window.history && window.history.replaceState) {
      const cleanUrl = window.location.pathname + window.location.hash;
      window.history.replaceState({}, document.title, cleanUrl);
    }
    performSync();
  }
}


