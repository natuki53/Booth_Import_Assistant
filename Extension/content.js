/**
 * BOOTH Import Assistant - Content Script
 * 
 * BOOTH購入ライブラリページでDOM解析を実行し、
 * ローカルBridgeサーバーへ自動送信します。
 */

const BRIDGE_URL = 'http://localhost:49729/sync';
const WAIT_TIME = 3000; // DOM読み込み待機時間（ms）

// ログヘルパー関数（エラーのみ）
function logError(...args) {
  console.error('[BOOTH]', ...args);
}

/**
 * 指定されたDocumentオブジェクトから商品情報を解析（BOOTHの実際のHTML構造に完全対応）
 * @param {Document} doc - 解析対象のドキュメント
 * @param {Set} processedIds - 処理済みID
 * @param {string} source - ソース ('purchased' or 'gift')
 */
function extractBoothItemsFromDOM(doc, processedIds = new Set(), source = 'purchased') {
  const items = [];
  
  try {
    const itemCards = doc.querySelectorAll('div.mb-16.bg-white.p-16');
    
    if (itemCards.length === 0) {
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
        
        const item = {
          id: boothId,
          title: title,
          author: author,
          productUrl: productLink.href,
          thumbnailUrl: thumbnailUrl,
          downloadUrls: downloadUrls,
          localThumbnail: `BoothBridge/thumbnails/${boothId}.jpg`,
          installed: false,
          importPath: `Assets/ImportedAssets/${boothId}/`,
          notes: '',
          source: source  // 購入またはギフトを識別
        };
        
        items.push(item);
        
      } catch (e) {
        // スキップ
      }
    });
    
  } catch (e) {
    // スキップ
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
    return 1;
  }
}

/**
 * 指定ページのHTMLを取得してDOMに変換
 * @param {number} pageNum - ページ番号
 * @param {string} path - パス ('/library' or '/library/gifts')
 */
async function fetchPageDOM(pageNum, path = '/library') {
  try {
    const url = `${location.origin}${path}?page=${pageNum}`;
    const response = await fetch(url, {
      credentials: 'same-origin',
      headers: { 'Accept': 'text/html' }
    });
    
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    
    const html = await response.text();
    const parser = new DOMParser();
    return parser.parseFromString(html, 'text/html');
  } catch (e) {
    return null;
  }
}

/**
 * 全ページを巡回して商品情報を取得（メイン関数）
 * 購入した商品とギフトの両方を取得
 */
async function extractBoothItems() {
  const allItems = [];
  const processedIds = new Set();
  
  try {
    // ========== 購入した商品を取得 ==========
    const purchasedPath = '/library';
    const currentPath = location.pathname;
    const isPurchasedPage = !currentPath.includes('/gifts');
    
    // 現在のページが購入ページの場合
    if (isPurchasedPage) {
      const purchasedTotalPages = getTotalPages(document);
      
      if (typeof showProgressNotification === 'function') {
        showProgressNotification(`🔄 購入商品 ページ 1/${purchasedTotalPages} を取得中...`);
      }
      
      const currentPageItems = extractBoothItemsFromDOM(document, processedIds, 'purchased');
      allItems.push(...currentPageItems);
      
      for (let page = 2; page <= purchasedTotalPages; page++) {
        await new Promise(resolve => setTimeout(resolve, 500));
        
        if (typeof showProgressNotification === 'function') {
          showProgressNotification(`🔄 購入商品 ページ ${page}/${purchasedTotalPages} を取得中...`);
        }
        
        const pageDoc = await fetchPageDOM(page, purchasedPath);
        if (pageDoc) {
          const pageItems = extractBoothItemsFromDOM(pageDoc, processedIds, 'purchased');
          allItems.push(...pageItems);
        }
      }
    } else {
      // ギフトページの場合は購入ページを別途取得
      const firstPageDoc = await fetchPageDOM(1, purchasedPath);
      if (firstPageDoc) {
        const purchasedTotalPages = getTotalPages(firstPageDoc);
        
        for (let page = 1; page <= purchasedTotalPages; page++) {
          if (typeof showProgressNotification === 'function') {
            showProgressNotification(`🔄 購入商品 ページ ${page}/${purchasedTotalPages} を取得中...`);
          }
          
          const pageDoc = page === 1 ? firstPageDoc : await fetchPageDOM(page, purchasedPath);
          if (pageDoc) {
            const pageItems = extractBoothItemsFromDOM(pageDoc, processedIds, 'purchased');
            allItems.push(...pageItems);
          }
          
          if (page < purchasedTotalPages) {
            await new Promise(resolve => setTimeout(resolve, 500));
          }
        }
      }
    }
    
    // ========== ギフトを取得 ==========
    const giftsPath = '/library/gifts';
    
    // ギフトの最初のページを取得
    const isGiftPage = currentPath.includes('/gifts');
    let giftsFirstPageDoc = isGiftPage ? document : await fetchPageDOM(1, giftsPath);
    
    if (giftsFirstPageDoc) {
      const giftsTotalPages = getTotalPages(giftsFirstPageDoc);
      
      if (giftsTotalPages > 0) {
        // 最初のページを処理
        if (typeof showProgressNotification === 'function') {
          showProgressNotification(`🎁 ギフト ページ 1/${giftsTotalPages} を取得中...`);
        }
        
        const firstPageItems = extractBoothItemsFromDOM(giftsFirstPageDoc, processedIds, 'gift');
        allItems.push(...firstPageItems);
        
        // 2ページ目以降を処理
        for (let page = 2; page <= giftsTotalPages; page++) {
          await new Promise(resolve => setTimeout(resolve, 500));
          
          if (typeof showProgressNotification === 'function') {
            showProgressNotification(`🎁 ギフト ページ ${page}/${giftsTotalPages} を取得中...`);
          }
          
          const pageDoc = await fetchPageDOM(page, giftsPath);
          if (pageDoc) {
            const pageItems = extractBoothItemsFromDOM(pageDoc, processedIds, 'gift');
            allItems.push(...pageItems);
          }
        }
      }
    }
    
    if (typeof showProgressNotification === 'function') {
      const purchasedCount = allItems.filter(item => item.source === 'purchased').length;
      const giftCount = allItems.filter(item => item.source === 'gift').length;
      showProgressNotification(`✅ 取得完了 - 購入:${purchasedCount}件 ギフト:${giftCount}件`);
    }
  } catch (e) {
    // スキップ
  }
  
  return allItems;
}

function extractBoothItemsCurrentPageOnly() {
  const processedIds = new Set();
  const currentPath = location.pathname;
  const source = currentPath.includes('/gifts') ? 'gift' : 'purchased';
  return extractBoothItemsFromDOM(document, processedIds, source);
}

function saveBoothLibraryJSON(items) {
  try {
    const blob = new Blob([JSON.stringify(items, null, 2)], { type: 'application/json' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = 'booth_library.json';
    link.click();
  } catch (e) {
    // スキップ
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
      showNotification('✅ Unityへの同期が完了しました！', 'success');
    } else {
      showNotification('❌ Bridgeへの接続に失敗しました', 'error');
    }
  } catch (e) {
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
      // エラーは無視
    });
  } catch (e) {
    // エラーは無視
  }
}

async function performSync() {
  const validHosts = ['manage.booth.pm', 'accounts.booth.pm'];
  
  if (!validHosts.includes(location.hostname) || !location.pathname.startsWith('/library')) {
    return;
  }
  
  await new Promise(resolve => setTimeout(resolve, WAIT_TIME));
  
  try {
    showProgressNotification('🔄 BOOTH商品を取得中...');
    
    const items = await extractBoothItems();
    hideProgressNotification();
    
    if (items.length === 0) {
      showNotification('⚠️ 商品が見つかりませんでした。ページを更新してください。', 'error');
      return;
    }
    
    await syncToBridge(items);
    sendDownloadMapToBackground(items);
  } catch (e) {
    hideProgressNotification();
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


