/**
 * BOOTH Import Assistant - Background Service Worker
 * 
 * ダウンロード監視と自動処理
 */

const BRIDGE_URL = 'http://localhost:49729';

// ログヘルパー関数
function logInfo(...args) {
  console.log('[BOOTH-BG]', ...args);
}

function logWarn(...args) {
  console.warn('[BOOTH-BG]', ...args);
}

function logError(...args) {
  console.error('[BOOTH-BG]', ...args);
}

// ダウンロード中のファイルを追跡
const downloadTracking = new Map();

// ダウンロードURLと商品IDの対応マップ: downloadUrl → boothId
const downloadUrlMap = new Map();

/**
 * ダウンロード開始時の処理
 */
chrome.downloads.onCreated.addListener(async (downloadItem) => {
  try {
    const url = downloadItem.url;
    
    if (!url.includes('booth.pm') && !url.includes('booth.pximg.net')) {
      return;
    }
    
    let boothId = null;
    let downloadIndex = 0;
    let downloadId = null;
    
    // 方法1: downloadUrlMapから検索
    for (const [mapUrl, info] of downloadUrlMap.entries()) {
      const urlBase = url.split('?')[0];
      if (url.includes(mapUrl) || mapUrl.includes(urlBase)) {
        boothId = info.boothId;
        downloadIndex = info.index;
        break;
      }
    }
    
    // 方法2: URLパターンから抽出
    if (!boothId) {
      const downloadMatch = url.match(/downloadables\/(\d+)/);
      if (downloadMatch) {
        downloadId = downloadMatch[1];
      }
      
      const itemMatch = url.match(/items\/(\d+)/);
      if (itemMatch) {
        boothId = `booth_${itemMatch[1]}`;
      }
    }
    
    if (!downloadId && !boothId) {
      logWarn('商品ID特定不可:', url.substring(0, 60));
      return;
    }
    
    downloadTracking.set(downloadItem.id, {
      url: url,
      downloadId: downloadId,
      boothId: boothId,
      downloadIndex: downloadIndex,
      filename: downloadItem.filename || 'unknown',
      startTime: Date.now()
    });
    
    logInfo('ダウンロード追跡:', boothId || downloadId);
  } catch (e) {
    logError('ダウンロード開始エラー:', e.message);
  }
});

/**
 * ダウンロードファイル名が確定した時の処理
 */
chrome.downloads.onChanged.addListener(async (delta) => {
  try {
    if (!downloadTracking.has(delta.id)) return;
    
    const tracking = downloadTracking.get(delta.id);
    
    if (delta.filename && delta.filename.current) {
      tracking.filename = delta.filename.current;
    }
    
    if (delta.state && delta.state.current === 'complete') {
      logInfo('ダウンロード完了:', tracking.boothId || tracking.downloadId);
      
      // chrome.downloads.search()で完全なファイルパスを取得
      chrome.downloads.search({ id: delta.id }, async (items) => {
        if (items && items.length > 0) {
          const downloadItem = items[0];
          tracking.fullPath = downloadItem.filename; // 完全なファイルパス
          logInfo('完全なファイルパス取得:', tracking.fullPath);
        }
        
        await notifyBridgeDownload(tracking);
        downloadTracking.delete(delta.id);
      });
    }
    
    if (delta.state && delta.state.current === 'interrupted') {
      logWarn('ダウンロード中断:', tracking.boothId || tracking.downloadId);
      downloadTracking.delete(delta.id);
    }
    
    if (delta.error) {
      logError('ダウンロードエラー:', tracking.boothId || tracking.downloadId, delta.error.current);
    }
  } catch (e) {
    logError('ダウンロード変更エラー:', e.message);
  }
});

/**
 * Bridgeにダウンロード情報を通知
 */
async function notifyBridgeDownload(tracking) {
  try {
    const filename = tracking.filename.split(/[/\\]/).pop();
    const notifyData = {
      filename: filename,
      fullPath: tracking.fullPath || tracking.filename, // 完全なファイルパスを追加
      downloadId: tracking.downloadId,
      boothId: tracking.boothId,
      url: tracking.url,
      timestamp: Date.now()
    };
    
    logInfo('Bridge通知データ:', notifyData);
    
    const response = await fetch(`${BRIDGE_URL}/download-notify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(notifyData)
    });
    
    if (response.ok) {
      logInfo('Bridge通知成功:', notifyData.boothId || notifyData.downloadId);
    } else {
      logWarn('Bridge通知失敗:', response.status);
    }
  } catch (e) {
    logError('Bridge通知エラー:', e.message);
  }
}

/**
 * メッセージ受信（content scriptから）
 */
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'UPDATE_DOWNLOAD_MAP') {
    try {
      const downloadMap = message.data;
      downloadUrlMap.clear();
      
      let totalUrls = 0;
      for (const [boothId, urls] of Object.entries(downloadMap)) {
        for (let i = 0; i < urls.length; i++) {
          downloadUrlMap.set(urls[i], { boothId: boothId, index: i });
          totalUrls++;
        }
      }
      
      logInfo('URLマップ更新:', totalUrls, '個');
      sendResponse({ success: true, count: totalUrls });
    } catch (e) {
      logError('マップ更新エラー:', e.message);
      sendResponse({ success: false, error: e.message });
    }
  }
  else if (message.type === 'BOOTH_DOWNLOAD_START') {
    sendResponse({ success: true });
  }
  else {
    logWarn('未知のメッセージ:', message.type);
    sendResponse({ success: false, error: 'Unknown message type' });
  }
  
  return true;
});

