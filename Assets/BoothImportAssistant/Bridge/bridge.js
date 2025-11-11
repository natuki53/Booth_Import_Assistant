const http = require('http');
const fs = require('fs');
const path = require('path');
const https = require('https');
const AdmZip = require('adm-zip');
const os = require('os');
const { execSync } = require('child_process');

// ログレベル
const LOG_LEVEL = {
  DEBUG: 0,
  INFO: 1,
  WARN: 2,
  ERROR: 3
};

const CURRENT_LOG_LEVEL = LOG_LEVEL.ERROR; // エラーのみ出力

// ログ出力ヘルパー
function log(level, ...args) {
  if (level >= CURRENT_LOG_LEVEL) {
    const timestamp = new Date().toISOString();
    const levelName = Object.keys(LOG_LEVEL).find(key => LOG_LEVEL[key] === level);
    console.log(`[${timestamp}][BoothBridge][${levelName}]`, ...args);
  }
}

function logDebug(...args) { log(LOG_LEVEL.DEBUG, ...args); }
function logInfo(...args) { log(LOG_LEVEL.INFO, ...args); }
function logWarn(...args) { log(LOG_LEVEL.WARN, ...args); }
function logError(...args) { log(LOG_LEVEL.ERROR, ...args); }

// Bridge起動

// 進捗状態管理
let currentProgress = {
  active: false,
  stage: '', // 'downloading', 'extracting', 'completed'
  fileName: '',
  progress: 0, // 0-100
  message: ''
};

// コマンドライン引数からプロジェクトパスを取得
let projectPath = '';
const args = process.argv.slice(2);
logDebug('コマンドライン引数:', args);

for (let i = 0; i < args.length; i++) {
  if (args[i] === '--projectPath' && i + 1 < args.length) {
    projectPath = args[i + 1];
    break;
  }
}

if (!projectPath) {
  logError('--projectPath が指定されていません。');
  logError('使用方法: node bridge.js --projectPath "/path/to/project"');
  process.exit(1);
}

// プロジェクトパス設定完了

// アプリケーションデータの保存先を取得（OS別）
function getAppDataPath() {
  let appDataPath;
  
  if (os.platform() === 'win32') {
    // Windows: %LOCALAPPDATA%\Booth_Import_Assistant
    appDataPath = path.join(process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local'), 'Booth_Import_Assistant');
  } else if (os.platform() === 'darwin') {
    // Mac: ~/Library/Application Support/Booth_Import_Assistant
    appDataPath = path.join(os.homedir(), 'Library', 'Application Support', 'Booth_Import_Assistant');
  } else {
    // Linux: ~/.config/Booth_Import_Assistant
    appDataPath = path.join(os.homedir(), '.config', 'Booth_Import_Assistant');
  }
  
  logDebug('アプリケーションデータパス:', appDataPath);
  return appDataPath;
}

// 保存先ディレクトリ
const APP_DATA_DIR = getAppDataPath(); // グローバルなアプリケーションデータ
const THUMBNAILS_DIR = path.join(APP_DATA_DIR, 'thumbnails');
const JSON_FILE = path.join(APP_DATA_DIR, 'booth_assets.json');
const BACKUP_FILE = path.join(APP_DATA_DIR, 'booth_assets.backup.json');
const TEMP_PACKAGE_DIR = path.join(projectPath, 'BoothBridge', 'temp'); // プロジェクト固有の一時ファイル

// ダウンロードフォルダを取得（Windows/Mac/Linux対応）
function getDownloadsFolder() {
  // Windowsの場合、shell:downloadsを解決
  if (os.platform() === 'win32') {
    try {
      // 方法1: Shell.Application COMオブジェクトでshell:downloadsを解決
      try {
        const shellResult = execSync('powershell -Command "(New-Object -ComObject Shell.Application).NameSpace(\'shell:Downloads\').Self.Path"', {
          encoding: 'utf8',
          stdio: ['pipe', 'pipe', 'pipe'],
          timeout: 5000
        }).trim();
        
        if (shellResult && fs.existsSync(shellResult)) {
          logDebug('Shell.Applicationでダウンロードフォルダを取得:', shellResult);
          return shellResult;
        }
      } catch (shellError) {
        // Shell.Application失敗時は次の方法を試す
      }
      
      // 方法2: レジストリから取得（shell:downloadsで変更された場合も対応）
      try {
        const regResult = execSync('powershell -Command "(Get-ItemProperty -Path \'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Shell Folders\' -Name \'{374DE290-123F-4565-9164-39C4925E467B}\' -ErrorAction SilentlyContinue).\'{374DE290-123F-4565-9164-39C4925E467B}\'"', {
          encoding: 'utf8',
          stdio: ['pipe', 'pipe', 'pipe'],
          timeout: 5000
        }).trim();
        
        if (regResult && fs.existsSync(regResult)) {
          logDebug('レジストリからダウンロードフォルダを取得:', regResult);
          return regResult;
        }
      } catch (regError) {
        // レジストリアクセス失敗時は無視
      }
    } catch (error) {
      // PowerShell実行失敗時はフォールバック
      logWarn('PowerShellでダウンロードフォルダを取得できませんでした。デフォルトパスを使用します。');
    }
  }
  
  // Mac/Linux、またはWindowsでフォールバック
  const defaultPath = path.join(os.homedir(), 'Downloads');
  logDebug('デフォルトのダウンロードフォルダを使用:', defaultPath);
  return defaultPath;
}

const DOWNLOADS_DIR = getDownloadsFolder();

// ディレクトリ設定完了

// ディレクトリ作成
try {
  if (!fs.existsSync(APP_DATA_DIR)) {
    fs.mkdirSync(APP_DATA_DIR, { recursive: true });
  }
  if (!fs.existsSync(THUMBNAILS_DIR)) {
    fs.mkdirSync(THUMBNAILS_DIR, { recursive: true });
  }
  if (!fs.existsSync(TEMP_PACKAGE_DIR)) {
    fs.mkdirSync(TEMP_PACKAGE_DIR, { recursive: true });
  }
} catch (e) {
  logError('ディレクトリ作成エラー:', e.message);
  logError('スタックトレース:', e.stack);
  process.exit(1);
}

// ダウンロードフォルダの存在確認
if (!fs.existsSync(DOWNLOADS_DIR)) {
  logWarn('⚠️ ダウンロードフォルダが見つかりません:', DOWNLOADS_DIR);
}

// JSONファイルの読み込み
function loadAssets() {
  if (fs.existsSync(JSON_FILE)) {
    try {
      const data = fs.readFileSync(JSON_FILE, 'utf-8');
      const assets = JSON.parse(data);
      return assets;
    } catch (e) {
      logError('JSON読み込みエラー:', e.message);
      
      // バックアップから復元を試みる
      if (fs.existsSync(BACKUP_FILE)) {
        try {
          const backupData = fs.readFileSync(BACKUP_FILE, 'utf-8');
          const assets = JSON.parse(backupData);
          return assets;
        } catch (backupError) {
          logError('バックアップからの復元も失敗:', backupError.message);
        }
      }
      
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
    const jsonString = JSON.stringify(assets, null, 2);
    fs.writeFileSync(JSON_FILE, jsonString, 'utf-8');
  } catch (e) {
    logError('JSON保存エラー:', e.message);
  }
}

// サムネイルダウンロード
function downloadThumbnail(url, savePath) {
  return new Promise((resolve) => {
    const request = https.get(url, (res) => {
      if (res.statusCode !== 200) {
        resolve(false);
        return;
      }
      
      const fileStream = fs.createWriteStream(savePath);
      res.pipe(fileStream);
      
      fileStream.on('finish', () => {
        fileStream.close();
        resolve(true);
      });
      
      fileStream.on('error', (err) => {
        logError('サムネイル保存エラー:', err.message);
        resolve(false);
      });
    });
    
    request.on('error', (err) => {
      logError('サムネイルダウンロードエラー:', err.message);
      resolve(false);
    });
    
    // タイムアウト設定（30秒）
    request.setTimeout(30000, () => {
      request.destroy();
      resolve(false);
    });
  });
}

// ZIP展開処理
// シンプルなZIP展開&インポート関数
async function extractAndImportZip(zipPath, originalFilename) {
  
  // 進捗状態を初期化
  currentProgress = {
    active: true,
    stage: 'extracting',
    fileName: originalFilename,
    progress: 0,
    message: 'ZIP展開中...'
  };
  
  try {
    // ZIPファイルの存在確認
    if (!fs.existsSync(zipPath)) {
      logError('ZIPファイルが存在しません:', zipPath);
      currentProgress.active = false;
      return false;
    }
    
    currentProgress.progress = 10;
    currentProgress.message = 'ZIPファイルを読み込み中...';
    
    // ZIP読み込み
    const zip = new AdmZip(zipPath);
    const zipEntries = zip.getEntries();
    
    currentProgress.progress = 30;
    currentProgress.message = `ZIP展開中... (${zipEntries.length}個のファイル)`;
    
    // 一時展開先ディレクトリ
    const tempExtractPath = path.join(os.tmpdir(), `booth_temp_${Date.now()}`);
    
    // 一時ディレクトリ作成
    fs.mkdirSync(tempExtractPath, { recursive: true });
    
    // ZIP展開
    zip.extractAllTo(tempExtractPath, true);
    
    currentProgress.progress = 60;
    currentProgress.message = '.unitypackageファイルを検索中...';
    
    // .unitypackage ファイルを検索
    const unitypackageFiles = findUnityPackageFiles(tempExtractPath);
    
    if (unitypackageFiles.length === 0) {
      logError('.unitypackageファイルが見つかりません');
      fs.rmSync(tempExtractPath, { recursive: true, force: true });
      currentProgress = {
        active: false,
        stage: 'error',
        fileName: originalFilename,
        progress: 0,
        message: '.unitypackageファイルが見つかりませんでした'
      };
      return false;
    }
    
    currentProgress.progress = 70;
    currentProgress.message = `.unitypackageファイルをコピー中... (${unitypackageFiles.length}個)`;
    
    // ImportedAssets に .unitypackage をコピー
    const copiedFiles = [];
    
    for (let i = 0; i < unitypackageFiles.length; i++) {
      const unitypackageFile = unitypackageFiles[i];
      const fileName = path.basename(unitypackageFile);
      const destPath = path.join(TEMP_PACKAGE_DIR, fileName);
      
      currentProgress.progress = 70 + ((i + 1) / unitypackageFiles.length) * 20;
      currentProgress.message = `コピー中: ${fileName}`;
      
      fs.copyFileSync(unitypackageFile, destPath);
      copiedFiles.push(destPath);
    }
    
    currentProgress.progress = 95;
    currentProgress.message = 'クリーンアップ中...';
    
    // 一時フォルダを削除
    fs.rmSync(tempExtractPath, { recursive: true, force: true });
    
    currentProgress = {
      active: false,
      stage: 'completed',
      fileName: originalFilename,
      progress: 100,
      message: `完了: ${copiedFiles.length}個のunitypackage`
    };
    
    // 3秒後に進捗状態をリセット
    setTimeout(() => {
      currentProgress = {
        active: false,
        stage: '',
        fileName: '',
        progress: 0,
        message: ''
      };
    }, 3000);
    
    return true;
    
  } catch (e) {
    logError('=== ZIP展開エラー ===');
    logError('エラーメッセージ:', e.message);
    logError('スタックトレース:', e.stack);
    logError('ZIPファイル:', zipPath);
    
    currentProgress = {
      active: false,
      stage: 'error',
      fileName: originalFilename,
      progress: 0,
      message: `エラー: ${e.message}`
    };
    
    return false;
  }
}

async function extractZip(zipPath, boothId, subFolder = '') {
  
  try {
    // ZIPファイルの存在と読み取り可能性を確認
    if (!fs.existsSync(zipPath)) {
      logError('ZIPファイルが存在しません:', zipPath);
      return false;
    }
    
    // ZIP読み込み
    const zip = new AdmZip(zipPath);
    const zipEntries = zip.getEntries();
    
    // 一時展開先ディレクトリ
    const tempExtractPath = path.join(os.tmpdir(), `booth_temp_${boothId}_${Date.now()}`);
    
    // 一時ディレクトリ作成
    if (!fs.existsSync(tempExtractPath)) {
      fs.mkdirSync(tempExtractPath, { recursive: true });
    }
    
    // 一時フォルダに展開
    zip.extractAllTo(tempExtractPath, true);
    
    // .unitypackage ファイルを検索
    const unitypackageFiles = findUnityPackageFiles(tempExtractPath);
    
    if (unitypackageFiles.length === 0) {
      logError('.unitypackageファイルが見つかりません');
      
      // 一時フォルダを削除
      fs.rmSync(tempExtractPath, { recursive: true, force: true });
      return false;
    }
    
    // 一時ディレクトリに .unitypackage をコピー
    const finalPath = TEMP_PACKAGE_DIR;
    
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
      copiedFiles.push(destPath);
    }
    
    // 一時フォルダを削除
    fs.rmSync(tempExtractPath, { recursive: true, force: true });
    
    // JSONを更新
    const assets = loadAssets();
    const asset = assets.find(a => a.id === boothId);
    if (asset) {
      asset.installed = true;
      asset.importPath = `BoothBridge/temp/`;
      saveAssets(assets);
    }
    
    return true;
    
  } catch (e) {
    logError('ZIP展開エラー:', e.message);
    return false;
  }
}

// .unitypackageファイルを再帰的に検索
function findUnityPackageFiles(dir) {
  const results = [];
  
  function searchDir(currentDir) {
    try {
      const items = fs.readdirSync(currentDir);
      
      for (const item of items) {
        try {
          const fullPath = path.join(currentDir, item);
          const stat = fs.statSync(fullPath);
          
          if (stat.isDirectory()) {
            searchDir(fullPath);
          } else {
            if (item.toLowerCase().endsWith('.unitypackage')) {
              results.push(fullPath);
            }
          }
        } catch (itemError) {
          // スキップ
        }
      }
    } catch (dirError) {
      // スキップ
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
    logError('ダウンロードフォルダが見つかりません:', DOWNLOADS_DIR);
    return;
  }
  
  try {
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
      
      // ZIP展開
      await extractAndImportZip(zipPath, filename);
      
      watchedFiles.delete(filename);
    });
  } catch (e) {
    logError('ダウンロードフォルダ監視設定エラー:', e.message);
  }
}

// HTTPサーバー
const PORT = 49729;
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
  
  // 進捗情報取得エンドポイント
  if (req.method === 'GET' && req.url === '/progress') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(currentProgress));
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
        
        // 既存データ読み込み
        let existingAssets = loadAssets();
        
        // 同期処理（IDごとに上書き・追加）
        let updatedCount = 0;
        let addedCount = 0;
        let thumbnailDownloadCount = 0;
        
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
                newAsset.localThumbnail = thumbnailPath;
                thumbnailDownloadCount++;
              } else {
                newAsset.localThumbnail = '';
              }
            } else {
              newAsset.localThumbnail = thumbnailPath;
            }
          }
          
          // 既存データの保持（installed, importPath）
          if (existingIndex >= 0) {
            const existing = existingAssets[existingIndex];
            newAsset.installed = existing.installed || false;
            newAsset.importPath = existing.importPath || '';
            existingAssets[existingIndex] = newAsset;
            updatedCount++;
          } else {
            newAsset.installed = false;
            newAsset.importPath = '';
            existingAssets.push(newAsset);
            addedCount++;
          }
        }
        
        // 保存
        saveAssets(existingAssets);
        
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ 
          success: true, 
          count: newAssets.length,
          updated: updatedCount,
          added: addedCount,
          thumbnails: thumbnailDownloadCount
        }));
        
      } catch (e) {
        logError('同期処理エラー:', e.message);
        
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
        
        // ファイル名と商品IDを紐付け
        if (data.filename && (data.boothId || data.downloadId)) {
          downloadTrackingMap.set(data.filename, {
            boothId: data.boothId,
            downloadId: data.downloadId,
            url: data.url,
            timestamp: data.timestamp || Date.now()
          });
          
          // 古いエントリを削除（1時間以上前）
          const oneHourAgo = Date.now() - 60 * 60 * 1000;
          for (const [key, value] of downloadTrackingMap.entries()) {
            if (value.timestamp < oneHourAgo) {
              downloadTrackingMap.delete(key);
            }
          }
        }
        
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: true, trackingMapSize: downloadTrackingMap.size }));
        
      } catch (e) {
        logError('ダウンロード通知エラー:', e.message);
        
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
  console.log('[BoothBridge] 起動完了 - http://localhost:' + PORT);
});

// エラーハンドリング
server.on('error', (err) => {
  if (err.code === 'EADDRINUSE') {
    logError(`ポート ${PORT} は既に使用されています`);
  } else if (err.code === 'EACCES') {
    logError(`ポート ${PORT} へのアクセスが拒否されました`);
  } else {
    logError('サーバーエラー:', err.message);
  }
  
  process.exit(1);
});

// ダウンロードフォルダ監視開始
watchDownloadsFolder();

// 終了処理
process.on('SIGINT', () => {
  server.close();
  process.exit(0);
});

process.on('SIGTERM', () => {
  server.close();
  process.exit(0);
});

// 未処理のエラーをキャッチ
process.on('uncaughtException', (err) => {
  logError('未処理の例外エラー:', err.message);
  process.exit(1);
});

process.on('unhandledRejection', (reason, promise) => {
  logError('未処理のPromise拒否:', reason);
  process.exit(1);
});



