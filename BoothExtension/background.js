/**
 * BOOTH Import Assistant - Background Service Worker
 * 
 * ダウンロード監視と自動処理
 */

const BRIDGE_URL = 'http://localhost:4823';

// ログヘルパー関数
function logInfo(...args) {
  const timestamp = new Date().toISOString();
  console.log(`[${timestamp}][BOOTH-BG][INFO]`, ...args);
}

function logDebug(...args) {
  const timestamp = new Date().toISOString();
  console.log(`[${timestamp}][BOOTH-BG][DEBUG]`, ...args);
}

function logWarn(...args) {
  const timestamp = new Date().toISOString();
  console.warn(`[${timestamp}][BOOTH-BG][WARN]`, ...args);
}

function logError(...args) {
  const timestamp = new Date().toISOString();
  console.error(`[${timestamp}][BOOTH-BG][ERROR]`, ...args);
}

// ダウンロード中のファイルを追跡
const downloadTracking = new Map();

// ダウンロードURLと商品IDの対応マップ: downloadUrl → boothId
const downloadUrlMap = new Map();

logInfo('=== Background Service Worker 起動 ===');
logInfo('Bridge URL:', BRIDGE_URL);
logInfo('Chrome拡張バージョン:', chrome.runtime.getManifest().version);

/**
 * ダウンロード開始時の処理
 */
chrome.downloads.onCreated.addListener(async (downloadItem) => {
  try {
    const url = downloadItem.url;
    
    logDebug('=== ダウンロード開始イベント ===');
    logDebug('ダウンロードID:', downloadItem.id);
    logDebug('URL:', url);
    logDebug('MIME:', downloadItem.mime);
    logDebug('初期ファイル名:', downloadItem.filename);
    
    // BOOTHのダウンロードURLか確認
    if (!url.includes('booth.pm') && !url.includes('booth.pximg.net')) {
      logDebug('スキップ（非BOOTHドメイン）');
      return;
    }
    
    logInfo('✓ BOOTHダウンロード検知:', url.substring(0, 80) + '...');
    
    // URLから商品IDを特定
    let boothId = null;
    let downloadIndex = 0;
    let downloadId = null;
    
    // 方法1: downloadUrlMapから検索（最も確実）
    logDebug('方法1: ダウンロードURLマップから検索...');
    logDebug(`マップサイズ: ${downloadUrlMap.size}`);
    
    for (const [mapUrl, info] of downloadUrlMap.entries()) {
      logDebug(`  比較: ${mapUrl.substring(0, 50)}...`);
      
      if (url.includes(mapUrl) || mapUrl.includes(url.split('?')[0])) {
        boothId = info.boothId;
        downloadIndex = info.index;
        logInfo('✓ マップから商品ID特定成功');
        logInfo(`  商品ID: ${boothId}`);
        logInfo(`  インデックス: ${downloadIndex}`);
        break;
      }
    }
    
    // 方法2: URLパターンから抽出（フォールバック）
    if (!boothId) {
      logDebug('方法2: URLパターンから抽出を試みます...');
      
      // パターン1: /downloadables/<id>
      const downloadMatch = url.match(/downloadables\/(\d+)/);
      if (downloadMatch) {
        downloadId = downloadMatch[1];
        logInfo('✓ ダウンロードID抽出:', downloadId);
      }
      
      // パターン2: /items/<id> (リダイレクト前)
      const itemMatch = url.match(/items\/(\d+)/);
      if (itemMatch) {
        boothId = `booth_${itemMatch[1]}`;
        logInfo('✓ URLから商品ID抽出:', boothId);
      }
    }
    
    if (!downloadId && !boothId) {
      logWarn('⚠️ 商品ID特定できませんでした');
      logWarn('URL:', url);
      logWarn('このダウンロードは追跡されません');
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
    
    logInfo('✓ ダウンロード追跡開始');
    logInfo(`  ダウンロードID: ${downloadItem.id}`);
    logInfo(`  商品ID: ${boothId || downloadId}`);
    logDebug(`  追跡マップサイズ: ${downloadTracking.size}`);
    
  } catch (e) {
    logError('=== ダウンロード開始エラー ===');
    logError('エラーメッセージ:', e.message);
    logError('スタック:', e.stack);
    logError('ダウンロードItem:', downloadItem);
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
    
    logDebug('ダウンロード変更イベント:', delta.id);
    
    // ファイル名が確定した場合
    if (delta.filename && delta.filename.current) {
      tracking.filename = delta.filename.current;
      logInfo('✓ ファイル名確定:', tracking.filename);
    }
    
    // ダウンロード完了時
    if (delta.state && delta.state.current === 'complete') {
      const elapsedTime = ((Date.now() - tracking.startTime) / 1000).toFixed(1);
      logInfo('=== ダウンロード完了 ===');
      logInfo(`ファイル名: ${tracking.filename}`);
      logInfo(`商品ID: ${tracking.boothId || tracking.downloadId}`);
      logInfo(`所要時間: ${elapsedTime}秒`);
      
      // Bridgeにダウンロード情報を通知
      await notifyBridgeDownload(tracking);
      
      // 追跡情報を削除
      downloadTracking.delete(delta.id);
      logDebug(`追跡情報削除 (残り: ${downloadTracking.size})`);
    }
    
    // ダウンロード中断時
    if (delta.state && delta.state.current === 'interrupted') {
      logWarn('⚠️ ダウンロード中断:', tracking.filename);
      logWarn('商品ID:', tracking.boothId || tracking.downloadId);
      downloadTracking.delete(delta.id);
    }
    
    // エラー時
    if (delta.error) {
      logError('ダウンロードエラー:', delta.error.current);
      logError('ファイル名:', tracking.filename);
      logError('商品ID:', tracking.boothId || tracking.downloadId);
    }
    
  } catch (e) {
    logError('=== ダウンロード変更エラー ===');
    logError('エラーメッセージ:', e.message);
    logError('スタック:', e.stack);
    logError('Delta:', delta);
  }
});

/**
 * Bridgeにダウンロード情報を通知
 */
async function notifyBridgeDownload(tracking) {
  logInfo('=== Bridge通知開始 ===');
  
  try {
    // ファイル名から実際のファイル名を取得（パスを除去）
    const filename = tracking.filename.split(/[/\\]/).pop();
    
    const notifyData = {
      filename: filename,
      downloadId: tracking.downloadId,
      boothId: tracking.boothId,
      url: tracking.url,
      timestamp: Date.now()
    };
    
    logInfo('通知データ:');
    logInfo(`  ファイル名: ${filename}`);
    logInfo(`  商品ID: ${notifyData.boothId || notifyData.downloadId}`);
    logDebug(`  URL: ${tracking.url}`);
    logDebug('  完全な通知データ:', JSON.stringify(notifyData, null, 2));
    
    // Bridgeに通知
    logDebug(`Bridge URL: ${BRIDGE_URL}/download-notify`);
    
    const response = await fetch(`${BRIDGE_URL}/download-notify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(notifyData)
    });
    
    logDebug(`HTTPレスポンス: ${response.status} ${response.statusText}`);
    
    if (response.ok) {
      const result = await response.json();
      logInfo('✓ Bridge通知成功');
      logDebug('レスポンス:', result);
    } else {
      const errorText = await response.text().catch(() => 'レスポンスボディ取得失敗');
      logWarn('⚠️ Bridge通知失敗');
      logWarn(`HTTPステータス: ${response.status} ${response.statusText}`);
      logWarn('レスポンスボディ:', errorText);
      logWarn('Bridgeが起動していない可能性があります');
    }
    
  } catch (e) {
    logError('=== Bridge通知エラー ===');
    logError('エラータイプ:', e.name);
    logError('エラーメッセージ:', e.message);
    
    if (e.name === 'TypeError' && e.message.includes('fetch')) {
      logError('');
      logError('🔴 Bridgeに接続できません');
      logError('原因: Bridgeが起動していないか、ポート4823が使用できません');
      logError('');
      logError('対処方法:');
      logError('  1. Unityを開いて「同期」ボタンを押す');
      logError('  2. Bridgeが起動するのを確認');
      logError('  3. 再度ダウンロードを試す');
    } else {
      logError('スタック:', e.stack);
    }
    
    // エラーでも継続（Bridgeが起動していない可能性があるため）
  }
}

/**
 * メッセージ受信（content scriptから）
 */
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  logDebug('=== メッセージ受信 ===');
  logDebug('タイプ:', message.type);
  logDebug('送信元:', sender.tab ? `タブID: ${sender.tab.id}` : '不明');
  
  if (message.type === 'UPDATE_DOWNLOAD_MAP') {
    // ダウンロードURLマップを更新
    try {
      const downloadMap = message.data;
      const productCount = Object.keys(downloadMap).length;
      
      logInfo('=== ダウンロードマップ更新 ===');
      logInfo(`商品数: ${productCount}`);
      
      // downloadUrlMap をクリアして新しいマップを構築
      downloadUrlMap.clear();
      
      let totalUrls = 0;
      for (const [boothId, urls] of Object.entries(downloadMap)) {
        logDebug(`  ${boothId}: ${urls.length}個のダウンロードURL`);
        
        for (let i = 0; i < urls.length; i++) {
          const url = urls[i];
          downloadUrlMap.set(url, {
            boothId: boothId,
            index: i
          });
          totalUrls++;
        }
      }
      
      logInfo(`✓ URL登録完了: ${totalUrls}個のURL`);
      logDebug(`マップサイズ: ${downloadUrlMap.size}`);
      
      sendResponse({ success: true, count: totalUrls });
      
    } catch (e) {
      logError('=== ダウンロードマップ更新エラー ===');
      logError('エラーメッセージ:', e.message);
      logError('スタック:', e.stack);
      sendResponse({ success: false, error: e.message });
    }
  }
  else if (message.type === 'BOOTH_DOWNLOAD_START') {
    // content scriptからダウンロード情報を受信
    logInfo('ダウンロード開始情報受信');
    logDebug('データ:', message.data);
    sendResponse({ success: true });
  }
  else {
    logWarn('未知のメッセージタイプ:', message.type);
    sendResponse({ success: false, error: 'Unknown message type' });
  }
  
  return true; // 非同期応答のため
});

