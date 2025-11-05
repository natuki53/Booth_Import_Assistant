/**
 * BOOTH Import Assistant - Background Service Worker
 * 
 * ダウンロード監視と自動処理
 */

const BRIDGE_URL = 'http://localhost:4823';

// ダウンロード中のファイルを追跡
const downloadTracking = new Map();

// ダウンロードURLと商品IDの対応マップ: downloadUrl → boothId
const downloadUrlMap = new Map();

console.log('[BOOTH Import BG] Background Service Worker 起動');

/**
 * ダウンロード開始時の処理
 */
chrome.downloads.onCreated.addListener(async (downloadItem) => {
  try {
    const url = downloadItem.url;
    
    // BOOTHのダウンロードURLか確認
    // 例: https://booth.pm/downloadables/123456 または
    //     https://booth.pximg.net/... または
    //     https://*.booth.pm/...
    
    if (!url.includes('booth.pm') && !url.includes('booth.pximg.net')) {
      return;
    }
    
    console.log('[BOOTH Import BG] ダウンロード開始:', url);
    
    // URLから商品IDを特定
    let boothId = null;
    let downloadIndex = 0;
    let downloadId = null;
    
    // 方法1: downloadUrlMapから検索（最も確実）
    // URLは完全一致しない可能性があるので、部分一致で検索
    for (const [mapUrl, info] of downloadUrlMap.entries()) {
      if (url.includes(mapUrl) || mapUrl.includes(url.split('?')[0])) {
        boothId = info.boothId;
        downloadIndex = info.index;
        console.log('[BOOTH Import BG] マップから商品ID特定:', boothId, 'index:', downloadIndex);
        break;
      }
    }
    
    // 方法2: URLパターンから抽出（フォールバック）
    if (!boothId) {
      // パターン1: /downloadables/<id>
      const downloadMatch = url.match(/downloadables\/(\d+)/);
      if (downloadMatch) {
        downloadId = downloadMatch[1];
        console.log('[BOOTH Import BG] ダウンロードID:', downloadId);
      }
      
      // パターン2: /items/<id> (リダイレクト前)
      const itemMatch = url.match(/items\/(\d+)/);
      if (itemMatch) {
        boothId = `booth_${itemMatch[1]}`;
        console.log('[BOOTH Import BG] URLから商品ID抽出:', boothId);
      }
    }
    
    if (!downloadId && !boothId) {
      console.log('[BOOTH Import BG] 商品ID特定できず:', url);
      return;
    }
    
    // ダウンロード情報を保存
    downloadTracking.set(downloadItem.id, {
      url: url,
      downloadId: downloadId,
      boothId: boothId,
      downloadIndex: downloadIndex,
      filename: downloadItem.filename || 'unknown',
      startTime: Date.now()
    });
    
    console.log('[BOOTH Import BG] ダウンロード追跡開始:', downloadItem.id, '→', boothId);
    
  } catch (e) {
    console.error('[BOOTH Import BG] ダウンロード開始エラー:', e);
  }
});

/**
 * ダウンロードファイル名が確定した時の処理
 */
chrome.downloads.onChanged.addListener(async (delta) => {
  try {
    if (!downloadTracking.has(delta.id)) {
      return;
    }
    
    const tracking = downloadTracking.get(delta.id);
    
    // ファイル名が確定した場合
    if (delta.filename && delta.filename.current) {
      tracking.filename = delta.filename.current;
      console.log('[BOOTH Import BG] ファイル名確定:', tracking.filename);
    }
    
    // ダウンロード完了時
    if (delta.state && delta.state.current === 'complete') {
      console.log('[BOOTH Import BG] ダウンロード完了:', tracking.filename);
      
      // Bridgeにダウンロード情報を通知
      await notifyBridgeDownload(tracking);
      
      // 追跡情報を削除
      downloadTracking.delete(delta.id);
    }
    
    // ダウンロード中断時
    if (delta.state && delta.state.current === 'interrupted') {
      console.warn('[BOOTH Import BG] ダウンロード中断:', tracking.filename);
      downloadTracking.delete(delta.id);
    }
    
  } catch (e) {
    console.error('[BOOTH Import BG] ダウンロード変更エラー:', e);
  }
});

/**
 * Bridgeにダウンロード情報を通知
 */
async function notifyBridgeDownload(tracking) {
  try {
    // ファイル名から実際のファイル名を取得（パスを除去）
    const filename = tracking.filename.split(/[/\\]/).pop();
    
    console.log('[BOOTH Import BG] Bridge通知:', {
      filename: filename,
      downloadId: tracking.downloadId,
      boothId: tracking.boothId
    });
    
    // Bridgeに通知
    const response = await fetch(`${BRIDGE_URL}/download-notify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        filename: filename,
        downloadId: tracking.downloadId,
        boothId: tracking.boothId,
        url: tracking.url,
        timestamp: Date.now()
      })
    });
    
    if (response.ok) {
      console.log('[BOOTH Import BG] Bridge通知成功');
    } else {
      console.warn('[BOOTH Import BG] Bridge通知失敗:', response.status);
    }
    
  } catch (e) {
    console.error('[BOOTH Import BG] Bridge通知エラー:', e);
    // エラーでも継続（Bridgeが起動していない可能性）
  }
}

/**
 * メッセージ受信（content scriptから）
 */
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'UPDATE_DOWNLOAD_MAP') {
    // ダウンロードURLマップを更新
    try {
      const downloadMap = message.data;
      console.log('[BOOTH Import BG] ダウンロードマップ更新:', Object.keys(downloadMap).length, '商品');
      
      // downloadUrlMap をクリアして新しいマップを構築
      downloadUrlMap.clear();
      
      for (const [boothId, urls] of Object.entries(downloadMap)) {
        for (let i = 0; i < urls.length; i++) {
          const url = urls[i];
          downloadUrlMap.set(url, {
            boothId: boothId,
            index: i
          });
        }
      }
      
      console.log('[BOOTH Import BG] URL登録完了:', downloadUrlMap.size, '個のURL');
      sendResponse({ success: true, count: downloadUrlMap.size });
      
    } catch (e) {
      console.error('[BOOTH Import BG] マップ更新エラー:', e);
      sendResponse({ success: false, error: e.message });
    }
  }
  else if (message.type === 'BOOTH_DOWNLOAD_START') {
    // content scriptからダウンロード情報を受信
    console.log('[BOOTH Import BG] ダウンロード情報受信:', message.data);
    sendResponse({ success: true });
  }
  
  return true; // 非同期応答のため
});

