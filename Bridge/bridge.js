const http = require('http');
const fs = require('fs');
const path = require('path');
const https = require('https');
const AdmZip = require('adm-zip');
const os = require('os');

// コマンドライン引数からプロジェクトパスを取得
let projectPath = '';
const args = process.argv.slice(2);
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--projectPath' && i + 1 < args.length) {
    projectPath = args[i + 1];
    break;
  }
}

if (!projectPath) {
  console.error('[BoothBridge][ERROR] --projectPath が指定されていません。');
  process.exit(1);
}

// 保存先ディレクトリ
const BRIDGE_DIR = path.join(projectPath, 'BoothBridge');
const THUMBNAILS_DIR = path.join(BRIDGE_DIR, 'thumbnails');
const JSON_FILE = path.join(BRIDGE_DIR, 'booth_assets.json');
const BACKUP_FILE = path.join(BRIDGE_DIR, 'booth_assets.backup.json');
const IMPORTED_ASSETS_DIR = path.join(projectPath, 'Assets', 'ImportedAssets');

// ダウンロードフォルダ（Windows/Mac/Linux対応）
const DOWNLOADS_DIR = path.join(os.homedir(), 'Downloads');

// ディレクトリ作成
if (!fs.existsSync(BRIDGE_DIR)) {
  fs.mkdirSync(BRIDGE_DIR, { recursive: true });
}
if (!fs.existsSync(THUMBNAILS_DIR)) {
  fs.mkdirSync(THUMBNAILS_DIR, { recursive: true });
}
if (!fs.existsSync(IMPORTED_ASSETS_DIR)) {
  fs.mkdirSync(IMPORTED_ASSETS_DIR, { recursive: true });
}

console.log('[BoothBridge] Bridge起動に成功しました');
console.log('[BoothBridge] OS:', os.platform(), os.arch());
console.log('[BoothBridge] プロジェクトパス:', projectPath);
console.log('[BoothBridge] ダウンロードフォルダ監視:', DOWNLOADS_DIR);

// JSONファイルの読み込み
function loadAssets() {
  if (fs.existsSync(JSON_FILE)) {
    try {
      const data = fs.readFileSync(JSON_FILE, 'utf-8');
      return JSON.parse(data);
    } catch (e) {
      console.error('[BoothBridge][ERROR] JSON読み込みエラー:', e.message);
      return [];
    }
  }
  return [];
}

// JSONファイルの保存
function saveAssets(assets) {
  try {
    // バックアップ作成
    if (fs.existsSync(JSON_FILE)) {
      fs.copyFileSync(JSON_FILE, BACKUP_FILE);
    }
    
    // 保存
    fs.writeFileSync(JSON_FILE, JSON.stringify(assets, null, 2), 'utf-8');
    console.log('[BoothBridge] JSON保存完了:', JSON_FILE);
  } catch (e) {
    console.error('[BoothBridge][ERROR] JSON保存エラー:', e.message);
  }
}

// サムネイルダウンロード
function downloadThumbnail(url, savePath) {
  return new Promise((resolve) => {
    https.get(url, (res) => {
      if (res.statusCode !== 200) {
        console.warn('[BoothBridge][WARN] サムネイルダウンロード失敗:', url);
        resolve(false);
        return;
      }
      
      const fileStream = fs.createWriteStream(savePath);
      res.pipe(fileStream);
      
      fileStream.on('finish', () => {
        fileStream.close();
        console.log('[BoothBridge] サムネイル保存完了:', savePath);
        resolve(true);
      });
      
      fileStream.on('error', (err) => {
        console.warn('[BoothBridge][WARN] サムネイル保存エラー:', err.message);
        resolve(false);
      });
    }).on('error', (err) => {
      console.warn('[BoothBridge][WARN] サムネイルダウンロードエラー:', err.message);
      resolve(false);
    });
  });
}

// ZIP展開処理
async function extractZip(zipPath, boothId, subFolder = '') {
  try {
    console.log('[BoothBridge] ZIP展開開始:', zipPath);
    
    const zip = new AdmZip(zipPath);
    
    // 一時展開先ディレクトリ
    const tempExtractPath = path.join(os.tmpdir(), `booth_temp_${boothId}_${Date.now()}`);
    
    // 一時ディレクトリ作成
    if (!fs.existsSync(tempExtractPath)) {
      fs.mkdirSync(tempExtractPath, { recursive: true });
    }
    
    // 一時フォルダに展開
    zip.extractAllTo(tempExtractPath, true);
    console.log('[BoothBridge] 一時展開完了:', tempExtractPath);
    
    // .unitypackage ファイルを検索
    const unitypackageFiles = findUnityPackageFiles(tempExtractPath);
    
    if (unitypackageFiles.length === 0) {
      console.warn('[BoothBridge][WARN] .unitypackageファイルが見つかりません:', tempExtractPath);
      // 一時フォルダを削除
      fs.rmSync(tempExtractPath, { recursive: true, force: true });
      return false;
    }
    
    console.log('[BoothBridge] .unitypackageファイル検出:', unitypackageFiles.length, '個');
    
    // ImportedAssets ディレクトリに .unitypackage をコピー
    let finalPath = path.join(IMPORTED_ASSETS_DIR, boothId);
    if (subFolder) {
      finalPath = path.join(finalPath, subFolder);
      console.log('[BoothBridge] サブフォルダに配置:', subFolder);
    }
    
    // 最終配置先ディレクトリ作成
    if (!fs.existsSync(finalPath)) {
      fs.mkdirSync(finalPath, { recursive: true });
    }
    
    // .unitypackage ファイルのみをコピー
    const copiedFiles = [];
    for (const unitypackageFile of unitypackageFiles) {
      const fileName = path.basename(unitypackageFile);
      const destPath = path.join(finalPath, fileName);
      
      fs.copyFileSync(unitypackageFile, destPath);
      console.log('[BoothBridge] .unitypackageコピー完了:', destPath);
      copiedFiles.push(destPath);
    }
    
    // 一時フォルダを削除
    fs.rmSync(tempExtractPath, { recursive: true, force: true });
    console.log('[BoothBridge] 一時フォルダ削除完了');
    
    // JSONを更新
    const assets = loadAssets();
    const asset = assets.find(a => a.id === boothId);
    if (asset) {
      asset.installed = true;
      asset.importPath = subFolder 
        ? `Assets/ImportedAssets/${boothId}/${subFolder}/`
        : `Assets/ImportedAssets/${boothId}/`;
      saveAssets(assets);
    }
    
    console.log('[BoothBridge] インポート準備完了:', copiedFiles.length, '個のunitypackage');
    return true;
  } catch (e) {
    console.error('[BoothBridge][ERROR] ZIP展開エラー:', e.message);
    return false;
  }
}

// .unitypackageファイルを再帰的に検索
function findUnityPackageFiles(dir) {
  const results = [];
  
  function searchDir(currentDir) {
    const items = fs.readdirSync(currentDir);
    
    for (const item of items) {
      const fullPath = path.join(currentDir, item);
      const stat = fs.statSync(fullPath);
      
      if (stat.isDirectory()) {
        searchDir(fullPath);
      } else if (item.toLowerCase().endsWith('.unitypackage')) {
        results.push(fullPath);
      }
    }
  }
  
  searchDir(dir);
  return results;
}

// ダウンロードフォルダの監視
let watchedFiles = new Set();

// ダウンロード追跡マップ: filename → { boothId, downloadId, timestamp }
const downloadTrackingMap = new Map();

function watchDownloadsFolder() {
  if (!fs.existsSync(DOWNLOADS_DIR)) {
    console.warn('[BoothBridge][WARN] ダウンロードフォルダが見つかりません:', DOWNLOADS_DIR);
    return;
  }
  
  fs.watch(DOWNLOADS_DIR, async (eventType, filename) => {
    if (!filename || !filename.endsWith('.zip')) {
      return;
    }
    
    // 重複処理防止
    if (watchedFiles.has(filename)) {
      return;
    }
    watchedFiles.add(filename);
    
    // ファイル書き込み完了待機
    await new Promise(resolve => setTimeout(resolve, 1000));
    
    const zipPath = path.join(DOWNLOADS_DIR, filename);
    
    // ファイル存在確認
    if (!fs.existsSync(zipPath)) {
      watchedFiles.delete(filename);
      return;
    }
    
    let boothId = null;
    let subFolder = '';
    
    // パターン1: booth_<id>.zip または booth_<id>_<num>.zip（従来の命名規則）
    const boothMatch = filename.match(/^booth_(\d+)(?:_(\d+))?\.zip$/);
    if (boothMatch) {
      boothId = `booth_${boothMatch[1]}`;
      if (boothMatch[2]) {
        subFolder = `variant_${boothMatch[2]}`;
      }
      console.log('[BoothBridge] ZIP検知（命名規則）:', filename, '→ ID:', boothId);
    }
    
    // パターン2: 任意のファイル名で、downloadTrackingMapに登録済み
    if (!boothId && downloadTrackingMap.has(filename)) {
      const tracking = downloadTrackingMap.get(filename);
      boothId = tracking.boothId;
      console.log('[BoothBridge] ZIP検知（追跡登録）:', filename, '→ ID:', boothId);
      
      // 追跡情報から削除（使い終わったら削除）
      downloadTrackingMap.delete(filename);
    }
    
    // 商品IDが特定できた場合のみ展開
    if (boothId) {
      await extractZip(zipPath, boothId, subFolder);
    } else {
      console.log('[BoothBridge] スキップ（BOOTHファイルではない）:', filename);
    }
    
    watchedFiles.delete(filename);
  });
  
  console.log('[BoothBridge] ダウンロードフォルダ監視開始');
}

// HTTPサーバー
const PORT = 4823;
const server = http.createServer(async (req, res) => {
  // CORS設定（localhostのみ）
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
  
  if (req.method === 'OPTIONS') {
    res.writeHead(200);
    res.end();
    return;
  }
  
  if (req.method === 'POST' && req.url === '/sync') {
    let body = '';
    
    req.on('data', chunk => {
      body += chunk.toString();
    });
    
    req.on('end', async () => {
      try {
        const newAssets = JSON.parse(body);
        console.log('[BoothBridge] 同期データ受信:', newAssets.length, '件');
        
        // 既存データ読み込み
        let existingAssets = loadAssets();
        
        // 同期処理（IDごとに上書き・追加）
        for (const newAsset of newAssets) {
          const existingIndex = existingAssets.findIndex(a => a.id === newAsset.id);
          
          // サムネイルダウンロード
          if (newAsset.thumbnailUrl) {
            const thumbnailFilename = `${newAsset.id}.jpg`;
            const thumbnailPath = path.join(THUMBNAILS_DIR, thumbnailFilename);
            
            // 既存ファイルがなければダウンロード
            if (!fs.existsSync(thumbnailPath)) {
              const success = await downloadThumbnail(newAsset.thumbnailUrl, thumbnailPath);
              if (success) {
                newAsset.localThumbnail = `BoothBridge/thumbnails/${thumbnailFilename}`;
              } else {
                newAsset.localThumbnail = '';
              }
            } else {
              newAsset.localThumbnail = `BoothBridge/thumbnails/${thumbnailFilename}`;
            }
          }
          
          // 既存データの保持（installed, importPath）
          if (existingIndex >= 0) {
            const existing = existingAssets[existingIndex];
            newAsset.installed = existing.installed || false;
            newAsset.importPath = existing.importPath || '';
            existingAssets[existingIndex] = newAsset;
          } else {
            newAsset.installed = false;
            newAsset.importPath = '';
            existingAssets.push(newAsset);
          }
        }
        
        // 保存
        saveAssets(existingAssets);
        
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: true, count: newAssets.length }));
        
        console.log('[BoothBridge] 同期完了');
      } catch (e) {
        console.error('[BoothBridge][ERROR] 同期処理エラー:', e.message);
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: false, error: e.message }));
      }
    });
  } else if (req.method === 'POST' && req.url === '/download-notify') {
    // Chrome拡張からダウンロード通知を受信
    let body = '';
    
    req.on('data', chunk => {
      body += chunk.toString();
    });
    
    req.on('end', () => {
      try {
        const data = JSON.parse(body);
        console.log('[BoothBridge] ダウンロード通知受信:', data.filename);
        
        // ファイル名と商品IDを紐付け
        if (data.filename && (data.boothId || data.downloadId)) {
          downloadTrackingMap.set(data.filename, {
            boothId: data.boothId,
            downloadId: data.downloadId,
            url: data.url,
            timestamp: data.timestamp || Date.now()
          });
          
          console.log('[BoothBridge] ダウンロード追跡登録:', data.filename, '->', data.boothId || data.downloadId);
          
          // 古いエントリを削除（1時間以上前）
          const oneHourAgo = Date.now() - 60 * 60 * 1000;
          for (const [key, value] of downloadTrackingMap.entries()) {
            if (value.timestamp < oneHourAgo) {
              downloadTrackingMap.delete(key);
            }
          }
        }
        
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: true }));
        
      } catch (e) {
        console.error('[BoothBridge][ERROR] ダウンロード通知エラー:', e.message);
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: false, error: e.message }));
      }
    });
  } else {
    res.writeHead(404);
    res.end('Not Found');
  }
});

// サーバー起動
server.listen(PORT, 'localhost', () => {
  console.log('[BoothBridge] HTTPサーバー起動完了: http://localhost:' + PORT);
});

// エラーハンドリング
server.on('error', (err) => {
  if (err.code === 'EADDRINUSE') {
    console.error('[BoothBridge][ERROR] ポート', PORT, 'は既に使用されています。');
  } else {
    console.error('[BoothBridge][ERROR] サーバーエラー:', err.message);
  }
  process.exit(1);
});

// ダウンロードフォルダ監視開始
watchDownloadsFolder();

// 終了処理
process.on('SIGINT', () => {
  console.log('[BoothBridge] Bridge終了');
  process.exit(0);
});

process.on('SIGTERM', () => {
  console.log('[BoothBridge] Bridge終了');
  process.exit(0);
});

