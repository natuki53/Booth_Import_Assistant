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
 * 指定されたDocumentオブジェクトから商品情報を解析（BOOTHの実際のHTML構造に完全対応）
 */
function extractBoothItemsFromDOM(doc, processedIds = new Set()) {
  const items = [];
  
  try {
    // 商品カード全体を直接取得
    // BOOTHの実際の構造: div.mb-16.bg-white.p-16.desktop:rounded-8.desktop:py-24.desktop:px-40
    const itemCards = doc.querySelectorAll('div.mb-16.bg-white.p-16');
    
    console.log('[BOOTH Import] 商品カード検出:', itemCards.length, '件');
    
    if (itemCards.length === 0) {
      console.warn('[BOOTH Import] ⚠️ 商品カードが見つかりません。ページ構造が変更された可能性があります。');
      return [];
    }
    
    itemCards.forEach((card, index) => {
      try {
        console.log('[BOOTH Import] === 商品カード', index + 1, '/', itemCards.length, '解析開始 ===');
        
        // 1. 商品ページリンクとIDを取得
        // タイトルのリンク: a[target="_blank"] で /items/ を含むもの
        const productLink = card.querySelector('a[target="_blank"][href*="/items/"]');
        if (!productLink) {
          console.warn('[BOOTH Import]   ⚠️ 商品リンクが見つかりません');
          return;
        }
        
        const match = productLink.href.match(/\/items\/(\d+)/);
        if (!match) {
          console.warn('[BOOTH Import]   ⚠️ 商品IDを抽出できません:', productLink.href);
          return;
        }
        
        const productId = match[1];
        const boothId = `booth_${productId}`;
        
        // 重複チェック
        if (processedIds.has(boothId)) {
          console.log('[BOOTH Import]   スキップ: 既に処理済み', boothId);
          return;
        }
        processedIds.add(boothId);
        
        console.log('[BOOTH Import]   商品ID:', productId);
        console.log('[BOOTH Import]   商品URL:', productLink.href);
        
        // 2. タイトル取得
        // div.text-text-default.font-bold.text-16.mb-8.break-all
        const titleDiv = card.querySelector('div.text-text-default.font-bold.text-16, div.font-bold.text-16');
        const title = titleDiv ? titleDiv.textContent.trim() : '商品名不明';
        console.log('[BOOTH Import]   タイトル:', title);
        
        // 3. サムネイル取得
        // img.l-library-item-thumbnail (高画質版)
        const thumbnailImg = card.querySelector('img.l-library-item-thumbnail');
        const thumbnailUrl = thumbnailImg ? thumbnailImg.src : '';
        if (thumbnailUrl) {
          console.log('[BOOTH Import]   サムネイル:', thumbnailUrl.substring(0, 60) + '...');
        } else {
          console.warn('[BOOTH Import]   ⚠️ サムネイルが見つかりません');
        }
        
        // 4. 作者名取得
        // div.text-14.text-text-gray600.break-all (作者名が入っているdiv)
        let author = '作者不明';
        const authorDiv = card.querySelector('div.text-14.text-text-gray600.break-all');
        if (authorDiv) {
          author = authorDiv.textContent.trim();
          console.log('[BOOTH Import]   作者:', author);
        } else {
          // フォールバック: .booth.pmを含むリンクから取得
          const authorLink = card.querySelector('a[href*=".booth.pm"]:not([href*="/items/"])');
          if (authorLink) {
            // リンク内のdivまたはテキストから作者名を取得
            const authorText = authorLink.querySelector('div.text-14');
            author = authorText ? authorText.textContent.trim() : authorLink.textContent.trim();
            console.log('[BOOTH Import]   作者（フォールバック）:', author);
          } else {
            console.warn('[BOOTH Import]   ⚠️ 作者情報が見つかりません');
          }
        }
        
        // 5. ダウンロードリンク取得（複数対応・アバター別・マテリアル対応）
        // 構造: div.mt-16.desktop:flex の中に、タイトル（div.min-w-0 > div.text-14）とリンク（div.mt-8 > a）がある
        let downloadUrls = [];
        
        // div.mt-16.desktop:flex（またはdesktop:flexを含む）を探す
        const downloadContainers = card.querySelectorAll('div.mt-16');
        
        console.log('[BOOTH Import]   ダウンロードコンテナ検索:', downloadContainers.length, '個');
        
        downloadContainers.forEach((container, containerIdx) => {
          // このコンテナ内にダウンロードリンクがあるか確認
          const downloadLink = container.querySelector('a[href*="downloadables"]');
          
          if (downloadLink && !downloadUrls.some(dl => dl.url === downloadLink.href)) {
            // タイトル（ラベル）を取得
            // div.min-w-0 > div.text-14 または div.text14 を探す
            let label = 'ダウンロード';
            
            // 方法1: div.min-w-0 内の div.text-14 または div.text14
            const minWidthDiv = container.querySelector('div.min-w-0, div[class*="min-w"]');
            if (minWidthDiv) {
              const labelDiv = minWidthDiv.querySelector('div.text-14, div.text14, div[class*="text-14"], div[class*="text14"]');
              if (labelDiv) {
                label = labelDiv.textContent.trim();
                console.log('[BOOTH Import]     [方法1] ラベル取得成功（div.min-w-0内）:', label);
              }
            }
            
            // 方法2: コンテナ直下の div.text-14 または div.text14
            if (label === 'ダウンロード') {
              const labelDiv = container.querySelector('div.text-14, div.text14, div[class*="text-14"], div[class*="text14"]');
              if (labelDiv) {
                label = labelDiv.textContent.trim();
                console.log('[BOOTH Import]     [方法2] ラベル取得成功（コンテナ直下）:', label);
              }
            }
            
            // 方法3: リンクの前の兄弟要素または親の兄弟要素を探す
            if (label === 'ダウンロード') {
              let sibling = downloadLink.parentElement;
              for (let i = 0; i < 5 && sibling; i++) {
                sibling = sibling.previousElementSibling;
                if (sibling) {
                  const labelDiv = sibling.querySelector('div.text-14, div.text14, div[class*="text-14"], div[class*="text14"]');
                  if (labelDiv) {
                    label = labelDiv.textContent.trim();
                    console.log('[BOOTH Import]     [方法3] ラベル取得成功（兄弟要素）:', label);
                    break;
                  }
                }
              }
            }
            
            // 方法4: リンクのテキスト
            if (label === 'ダウンロード') {
              const linkText = downloadLink.textContent.trim();
              if (linkText && linkText !== '' && linkText.length < 100) {
                label = linkText;
                console.log('[BOOTH Import]     [方法4] ラベル取得（リンクテキスト）:', label);
              }
            }
            
            // マテリアルかどうかを判定（日本語・英語両対応）
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
                              // マテリアルファイル名のパターン
                              labelLower.includes('mat_') ||
                              labelLower.includes('_mat') ||
                              labelLower.includes('textures') ||
                              labelLower.includes('materials');
            
            downloadUrls.push({
              url: downloadLink.href,
              label: label,
              isMaterial: isMaterial
            });
            
            console.log('[BOOTH Import]   ダウンロードリンク [' + downloadUrls.length + ']:');
            console.log('[BOOTH Import]     URL:', downloadLink.href);
            console.log('[BOOTH Import]     ラベル:', label);
            console.log('[BOOTH Import]     種類:', isMaterial ? '📦 マテリアル' : '👤 アバター');
          }
        });
        
        // ダウンロードリンクが見つからない場合、カード全体から探す
        if (downloadUrls.length === 0) {
          console.warn('[BOOTH Import]   ⚠️ 構造化されたダウンロードリンクが見つかりません');
          const allDownloadLinks = card.querySelectorAll('a[href*="downloadables"]');
          console.log('[BOOTH Import]   カード全体から再検索:', allDownloadLinks.length, '件');
          
          allDownloadLinks.forEach((dlLink, idx) => {
            if (dlLink.href && !downloadUrls.some(dl => dl.url === dlLink.href)) {
              // リンクの近くにあるテキストを探す（最大5階層まで遡る）
              let label = 'ダウンロード';
              let parent = dlLink.parentElement;
              
              for (let i = 0; i < 5 && parent; i++) {
                const labelDiv = parent.querySelector('div.text-14, div.text14, div[class*="text-14"], div[class*="text14"]');
                if (labelDiv) {
                  label = labelDiv.textContent.trim();
                  console.log('[BOOTH Import]     [フォールバック] ラベル取得（階層', i, '）:', label);
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
              
              console.log('[BOOTH Import]   [フォールバック] ダウンロードリンク [' + downloadUrls.length + ']:');
              console.log('[BOOTH Import]     URL:', dlLink.href);
              console.log('[BOOTH Import]     ラベル:', label);
              console.log('[BOOTH Import]     種類:', isMaterial ? '📦 マテリアル' : '👤 アバター');
            }
          });
        }
        
        // 並べ替え: アバター別を先に、マテリアルを後に
        downloadUrls.sort((a, b) => {
          if (a.isMaterial && !b.isMaterial) return 1;  // マテリアルを後ろに
          if (!a.isMaterial && b.isMaterial) return -1; // マテリアルを後ろに
          return 0;
        });
        
        console.log('[BOOTH Import]   ===================');
        console.log('[BOOTH Import]   ダウンロードリンク合計:', downloadUrls.length, '件');
        if (downloadUrls.length > 1) {
          const materialCount = downloadUrls.filter(dl => dl.isMaterial).length;
          const avatarCount = downloadUrls.length - materialCount;
          console.log('[BOOTH Import]   内訳:');
          console.log('[BOOTH Import]     👤 アバター別: ' + avatarCount + '件 (Unity側: プルダウン表示)');
          console.log('[BOOTH Import]     📦 マテリアル: ' + materialCount + '件 (Unity側: 必ず別表示)');
          
          // 各ダウンロードリンクの一覧を表示
          downloadUrls.forEach((dl, idx) => {
            console.log('[BOOTH Import]     [' + (idx + 1) + '] ' + (dl.isMaterial ? '📦' : '👤') + ' ' + dl.label);
          });
          
          console.log('[BOOTH Import]   ---');
          console.log('[BOOTH Import]   Unity UI表示方針:');
          console.log('[BOOTH Import]   - アバター別（isMaterial: false）: すべてプルダウンメニューに');
          console.log('[BOOTH Import]   - マテリアル（isMaterial: true）: 必ず別枠で表示');
        }
        console.log('[BOOTH Import]   ===================');
        
        // 購入日（現在の日付）
        const purchaseDate = new Date().toISOString().split('T')[0];
        
        // 商品情報を追加
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
        
        console.log('[BOOTH Import] ✓ 商品解析完了:', {
          id: boothId,
          title: title.substring(0, 40) + (title.length > 40 ? '...' : ''),
          author: author,
          downloads: downloadUrls.length + '件',
          thumbnail: thumbnailUrl ? 'あり' : 'なし'
        });
        
      } catch (e) {
        console.error('[BOOTH Import] 商品カード解析エラー:', e.message);
        console.error('[BOOTH Import] スタック:', e.stack);
      }
    });
    
  } catch (e) {
    console.error('[BOOTH Import] DOM解析エラー:', e.message);
    console.error('[BOOTH Import] スタック:', e.stack);
  }
  
  console.log('[BOOTH Import] =========================');
  console.log('[BOOTH Import] 解析完了: 全', items.length, '件');
  console.log('[BOOTH Import] =========================');
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
 * 同期処理（非同期対応）
 * Unityから明示的に呼び出された場合のみ実行
 */
async function performSync() {
  logInfo('=== 同期処理開始 ===');
  
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
  
  logInfo('✓ ページ確認OK - 同期を開始します');
  
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
    
    logInfo('=== 同期処理完了 ===');
    
  } catch (e) {
    hideProgressNotification();
    logError('=== 同期エラー ===');
    logError('エラーメッセージ:', e.message);
    logError('スタック:', e.stack);
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
  logInfo('=== Content Script 初期化 ===');
  
  // URLパラメータをチェック
  const urlParams = new URLSearchParams(window.location.search);
  const shouldSync = urlParams.get('sync') === 'true';
  
  if (shouldSync) {
    logInfo('✓ Unity起動による同期を検出: 同期を開始します');
    // URLパラメータを削除（履歴に残さないため）
    if (window.history && window.history.replaceState) {
      const cleanUrl = window.location.pathname + window.location.hash;
      window.history.replaceState({}, document.title, cleanUrl);
    }
    performSync();
  } else {
    logInfo('手動でページを開いた場合: 同期は実行されません');
    logInfo('Unity側の「同期」ボタンから同期してください');
  }
}


