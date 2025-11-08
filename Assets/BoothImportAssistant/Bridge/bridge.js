const http = require('http');
const fs = require('fs');
const path = require('path');
const https = require('https');
const AdmZip = require('adm-zip');
const os = require('os');

// ログレベル
const LOG_LEVEL = {
  DEBUG: 0,
  INFO: 1,
  WARN: 2,
  ERROR: 3
};

const CURRENT_LOG_LEVEL = LOG_LEVEL.DEBUG; // 本番環境ではINFOに変更

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

logInfo('=== BOOTH Bridge 起動開始 ===');

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

logInfo('プロジェクトパス指定:', projectPath);

// 保存先ディレクトリ
const BRIDGE_DIR = path.join(projectPath, 'BoothBridge');
const THUMBNAILS_DIR = path.join(BRIDGE_DIR, 'thumbnails');
const JSON_FILE = path.join(BRIDGE_DIR, 'booth_assets.json');
const BACKUP_FILE = path.join(BRIDGE_DIR, 'booth_assets.backup.json');
const TEMP_PACKAGE_DIR = path.join(BRIDGE_DIR, 'temp'); // 一時的な.unitypackage配置場所

// ダウンロードフォルダ（Windows/Mac/Linux対応）
const DOWNLOADS_DIR = path.join(os.homedir(), 'Downloads');

logInfo('=== ディレクトリ設定 ===');
logDebug('BRIDGE_DIR:', BRIDGE_DIR);
logDebug('THUMBNAILS_DIR:', THUMBNAILS_DIR);
logDebug('JSON_FILE:', JSON_FILE);
logDebug('BACKUP_FILE:', BACKUP_FILE);
logDebug('TEMP_PACKAGE_DIR:', TEMP_PACKAGE_DIR);
logDebug('DOWNLOADS_DIR:', DOWNLOADS_DIR);

// ディレクトリ作成
try {
  if (!fs.existsSync(BRIDGE_DIR)) {
    logInfo('BoothBridgeディレクトリを作成:', BRIDGE_DIR);
    fs.mkdirSync(BRIDGE_DIR, { recursive: true });
  }
  if (!fs.existsSync(THUMBNAILS_DIR)) {
    logInfo('Thumbnailsディレクトリを作成:', THUMBNAILS_DIR);
    fs.mkdirSync(THUMBNAILS_DIR, { recursive: true });
  }
  if (!fs.existsSync(TEMP_PACKAGE_DIR)) {
    logInfo('一時パッケージディレクトリを作成:', TEMP_PACKAGE_DIR);
    fs.mkdirSync(TEMP_PACKAGE_DIR, { recursive: true });
  }
  logInfo('ディレクトリ初期化完了');
} catch (e) {
  logError('ディレクトリ作成エラー:', e.message);
  logError('スタックトレース:', e.stack);
  process.exit(1);
}

logInfo('=== システム情報 ===');
logInfo('OS:', os.platform(), os.arch());
logInfo('Node.js:', process.version);
logInfo('ホームディレクトリ:', os.homedir());
logInfo('ダウンロードフォルダ監視:', DOWNLOADS_DIR);

// ダウンロードフォルダの存在確認
if (!fs.existsSync(DOWNLOADS_DIR)) {
  logWarn('⚠️ ダウンロードフォルダが見つかりません:', DOWNLOADS_DIR);
  logWarn('自動ダウンロード検知が動作しない可能性があります');
} else {
  logInfo('✓ ダウンロードフォルダ確認OK');
}

logInfo('=== Bridge起動成功 ===');

// JSONファイルの読み込み
function loadAssets() {
  logDebug('JSON読み込み開始:', JSON_FILE);
  
  if (fs.existsSync(JSON_FILE)) {
    try {
      const data = fs.readFileSync(JSON_FILE, 'utf-8');
      const assets = JSON.parse(data);
      logInfo(`JSON読み込み成功: ${assets.length}件のアセット`);
      return assets;
    } catch (e) {
      logError('JSON読み込みエラー:', e.message);
      logError('スタックトレース:', e.stack);
      logError('ファイルパス:', JSON_FILE);
      
      // バックアップから復元を試みる
      if (fs.existsSync(BACKUP_FILE)) {
        logWarn('バックアップファイルからの復元を試みます...');
        try {
          const backupData = fs.readFileSync(BACKUP_FILE, 'utf-8');
          const assets = JSON.parse(backupData);
          logInfo(`バックアップから復元成功: ${assets.length}件のアセット`);
          return assets;
        } catch (backupError) {
          logError('バックアップからの復元も失敗:', backupError.message);
        }
      }
      
      return [];
    }
  }
  
  logDebug('JSONファイルが存在しません（初回起動）');
  return [];
}

// JSONファイルの保存
function saveAssets(assets) {
  logDebug('JSON保存開始:', JSON_FILE);
  logDebug('保存データ件数:', assets.length);
  
  try {
    // バックアップ作成
    if (fs.existsSync(JSON_FILE)) {
      logDebug('既存JSONのバックアップを作成');
      fs.copyFileSync(JSON_FILE, BACKUP_FILE);
      const backupStats = fs.statSync(BACKUP_FILE);
      logDebug(`バックアップ完了: ${backupStats.size} bytes`);
    }
    
    // 保存
    const jsonString = JSON.stringify(assets, null, 2);
    fs.writeFileSync(JSON_FILE, jsonString, 'utf-8');
    
    const stats = fs.statSync(JSON_FILE);
    logInfo(`✓ JSON保存完了: ${assets.length}件 (${stats.size} bytes)`);
    logDebug('保存先:', JSON_FILE);
    
  } catch (e) {
    logError('JSON保存エラー:', e.message);
    logError('スタックトレース:', e.stack);
    logError('保存先パス:', JSON_FILE);
    logError('データ件数:', assets.length);
  }
}

// サムネイルダウンロード
function downloadThumbnail(url, savePath) {
  logDebug('サムネイルダウンロード開始:', url);
  
  return new Promise((resolve) => {
    const request = https.get(url, (res) => {
      logDebug(`サムネイルHTTPレスポンス: ${res.statusCode} (${url})`);
      
      if (res.statusCode !== 200) {
        logWarn('サムネイルダウンロード失敗: HTTPステータス', res.statusCode);
        logWarn('URL:', url);
        resolve(false);
        return;
      }
      
      const fileStream = fs.createWriteStream(savePath);
      let downloadedBytes = 0;
      
      res.on('data', (chunk) => {
        downloadedBytes += chunk.length;
      });
      
      res.pipe(fileStream);
      
      fileStream.on('finish', () => {
        fileStream.close();
        logInfo(`✓ サムネイル保存完了: ${downloadedBytes} bytes`);
        logDebug('保存先:', savePath);
        resolve(true);
      });
      
      fileStream.on('error', (err) => {
        logWarn('サムネイル保存エラー:', err.message);
        logDebug('保存先:', savePath);
        logDebug('スタックトレース:', err.stack);
        resolve(false);
      });
    });
    
    request.on('error', (err) => {
      logWarn('サムネイルダウンロードエラー:', err.message);
      logDebug('URL:', url);
      logDebug('スタックトレース:', err.stack);
      resolve(false);
    });
    
    // タイムアウト設定（30秒）
    request.setTimeout(30000, () => {
      logWarn('サムネイルダウンロードタイムアウト（30秒）');
      logDebug('URL:', url);
      request.destroy();
      resolve(false);
    });
  });
}

// ZIP展開処理
// シンプルなZIP展開&インポート関数
async function extractAndImportZip(zipPath, originalFilename) {
  logInfo('=== ZIP展開&インポート処理開始 ===');
  logInfo('ZIPファイル:', zipPath);
  
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
    logDebug('ZIP読み込み中...');
    const zip = new AdmZip(zipPath);
    const zipEntries = zip.getEntries();
    logInfo(`ZIP内ファイル数: ${zipEntries.length}個`);
    
    currentProgress.progress = 30;
    currentProgress.message = `ZIP展開中... (${zipEntries.length}個のファイル)`;
    
    // 一時展開先ディレクトリ
    const tempExtractPath = path.join(os.tmpdir(), `booth_temp_${Date.now()}`);
    logDebug('一時展開先:', tempExtractPath);
    
    // 一時ディレクトリ作成
    fs.mkdirSync(tempExtractPath, { recursive: true });
    
    // ZIP展開
    logInfo('ZIP展開中...');
    zip.extractAllTo(tempExtractPath, true);
    logInfo('✓ ZIP展開完了');
    
    currentProgress.progress = 60;
    currentProgress.message = '.unitypackageファイルを検索中...';
    
    // .unitypackage ファイルを検索
    logDebug('.unitypackageファイルを検索中...');
    const unitypackageFiles = findUnityPackageFiles(tempExtractPath);
    
    if (unitypackageFiles.length === 0) {
      logWarn('⚠️ .unitypackageファイルが見つかりません');
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
    
    logInfo(`✓ .unitypackageファイル検出: ${unitypackageFiles.length}個`);
    
    currentProgress.progress = 70;
    currentProgress.message = `.unitypackageファイルをコピー中... (${unitypackageFiles.length}個)`;
    
    // ImportedAssets に .unitypackage をコピー
    logInfo('.unitypackageファイルをコピー中...');
    const copiedFiles = [];
    
    for (let i = 0; i < unitypackageFiles.length; i++) {
      const unitypackageFile = unitypackageFiles[i];
      const fileName = path.basename(unitypackageFile);
      const destPath = path.join(TEMP_PACKAGE_DIR, fileName);
      
      logDebug(`コピー: ${fileName}`);
      logDebug(`  → ${destPath}`);
      
      currentProgress.progress = 70 + ((i + 1) / unitypackageFiles.length) * 20;
      currentProgress.message = `コピー中: ${fileName}`;
      
      fs.copyFileSync(unitypackageFile, destPath);
      
      const destStats = fs.statSync(destPath);
      logInfo(`✓ コピー完了: ${fileName} (${(destStats.size / 1024 / 1024).toFixed(2)} MB)`);
      copiedFiles.push(destPath);
    }
    
    currentProgress.progress = 95;
    currentProgress.message = 'クリーンアップ中...';
    
    // 一時フォルダを削除
    logDebug('一時フォルダを削除中...');
    fs.rmSync(tempExtractPath, { recursive: true, force: true });
    logDebug('✓ 一時フォルダ削除完了');
    
    logInfo('=== ZIP展開&インポート処理完了 ===');
    logInfo(`✓ インポート準備完了: ${copiedFiles.length}個のunitypackage`);
    
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
  logInfo('=== ZIP展開処理開始 ===');
  logInfo('ZIPファイル:', zipPath);
  logInfo('商品ID:', boothId);
  logInfo('サブフォルダ:', subFolder || '(なし)');
  
  try {
    // ZIPファイルの存在と読み取り可能性を確認
    if (!fs.existsSync(zipPath)) {
      logError('ZIPファイルが存在しません:', zipPath);
      return false;
    }
    
    const zipStats = fs.statSync(zipPath);
    logDebug(`ZIPファイルサイズ: ${zipStats.size} bytes (${(zipStats.size / 1024 / 1024).toFixed(2)} MB)`);
    
    // ZIP読み込み
    logDebug('ZIP読み込み中...');
    const zip = new AdmZip(zipPath);
    const zipEntries = zip.getEntries();
    logInfo(`ZIP内ファイル数: ${zipEntries.length}個`);
    
    // ZIP内容をログ出力（デバッグ用）
    logDebug('=== ZIP内容一覧 ===');
    zipEntries.forEach((entry, index) => {
      if (index < 20) { // 最初の20件のみ表示
        logDebug(`  [${index + 1}] ${entry.entryName} (${entry.header.size} bytes)`);
      }
    });
    if (zipEntries.length > 20) {
      logDebug(`  ... 他 ${zipEntries.length - 20}件`);
    }
    
    // 一時展開先ディレクトリ
    const tempExtractPath = path.join(os.tmpdir(), `booth_temp_${boothId}_${Date.now()}`);
    logDebug('一時展開先:', tempExtractPath);
    
    // 一時ディレクトリ作成
    if (!fs.existsSync(tempExtractPath)) {
      fs.mkdirSync(tempExtractPath, { recursive: true });
      logDebug('一時ディレクトリ作成完了');
    }
    
    // 一時フォルダに展開
    logInfo('ZIP展開中...');
    zip.extractAllTo(tempExtractPath, true);
    logInfo('✓ ZIP一時展開完了:', tempExtractPath);
    
    // .unitypackage ファイルを検索
    logDebug('.unitypackageファイルを検索中...');
    const unitypackageFiles = findUnityPackageFiles(tempExtractPath);
    
    if (unitypackageFiles.length === 0) {
      logWarn('⚠️ .unitypackageファイルが見つかりません');
      logWarn('展開先:', tempExtractPath);
      logWarn('このZIPにはUnityPackageが含まれていない可能性があります');
      
      // 一時フォルダを削除
      fs.rmSync(tempExtractPath, { recursive: true, force: true });
      logDebug('一時フォルダ削除完了');
      return false;
    }
    
    logInfo(`✓ .unitypackageファイル検出: ${unitypackageFiles.length}個`);
    unitypackageFiles.forEach((file, index) => {
      const fileStats = fs.statSync(file);
      logInfo(`  [${index + 1}] ${path.basename(file)} (${(fileStats.size / 1024 / 1024).toFixed(2)} MB)`);
    });
    
    // 一時ディレクトリに .unitypackage をコピー
    const finalPath = TEMP_PACKAGE_DIR;
    
    logDebug('最終配置先:', finalPath);
    
    // 最終配置先ディレクトリ作成
    if (!fs.existsSync(finalPath)) {
      fs.mkdirSync(finalPath, { recursive: true });
      logDebug('配置先ディレクトリ作成完了');
    }
    
    // .unitypackage ファイルのみをコピー
    logInfo('.unitypackageファイルをコピー中...');
    const copiedFiles = [];
    
    for (const unitypackageFile of unitypackageFiles) {
      const fileName = path.basename(unitypackageFile);
      const destPath = path.join(finalPath, fileName);
      
      logDebug(`コピー: ${fileName}`);
      logDebug(`  元: ${unitypackageFile}`);
      logDebug(`  先: ${destPath}`);
      
      fs.copyFileSync(unitypackageFile, destPath);
      
      const destStats = fs.statSync(destPath);
      logInfo(`✓ コピー完了: ${fileName} (${(destStats.size / 1024 / 1024).toFixed(2)} MB)`);
      copiedFiles.push(destPath);
    }
    
    // 一時フォルダを削除
    logDebug('一時フォルダを削除中...');
    fs.rmSync(tempExtractPath, { recursive: true, force: true });
    logDebug('✓ 一時フォルダ削除完了');
    
    // JSONを更新
    logDebug('JSON更新中（installed状態を更新）...');
    const assets = loadAssets();
    const asset = assets.find(a => a.id === boothId);
    if (asset) {
      asset.installed = true;
      asset.importPath = `BoothBridge/temp/`;
      saveAssets(assets);
      logInfo(`✓ アセット情報更新: ${asset.title}`);
    } else {
      logWarn(`⚠️ アセット情報が見つかりません（ID: ${boothId}）`);
    }
    
    logInfo('=== ZIP展開処理完了 ===');
    logInfo(`✓ インポート準備完了: ${copiedFiles.length}個のunitypackage`);
    copiedFiles.forEach((file, index) => {
      logInfo(`  [${index + 1}] ${file}`);
    });
    
    return true;
    
  } catch (e) {
    logError('=== ZIP展開エラー ===');
    logError('エラーメッセージ:', e.message);
    logError('スタックトレース:', e.stack);
    logError('ZIPファイル:', zipPath);
    logError('商品ID:', boothId);
    logError('サブフォルダ:', subFolder);
    return false;
  }
}

// .unitypackageファイルを再帰的に検索
function findUnityPackageFiles(dir) {
  logDebug('.unitypackageファイル検索開始:', dir);
  const results = [];
  let scannedDirs = 0;
  let scannedFiles = 0;
  
  function searchDir(currentDir) {
    try {
      const items = fs.readdirSync(currentDir);
      scannedDirs++;
      
      for (const item of items) {
        try {
          const fullPath = path.join(currentDir, item);
          const stat = fs.statSync(fullPath);
          
          if (stat.isDirectory()) {
            searchDir(fullPath);
          } else {
            scannedFiles++;
            if (item.toLowerCase().endsWith('.unitypackage')) {
              logDebug(`  ✓ 発見: ${fullPath}`);
              results.push(fullPath);
            }
          }
        } catch (itemError) {
          logWarn(`ファイルアクセスエラー（スキップ）: ${item}`, itemError.message);
        }
      }
    } catch (dirError) {
      logWarn(`ディレクトリ読み込みエラー: ${currentDir}`, dirError.message);
    }
  }
  
  searchDir(dir);
  
  logDebug(`検索完了: ${scannedDirs}個のディレクトリ、${scannedFiles}個のファイルをスキャン`);
  logDebug(`検出結果: ${results.length}個の.unitypackageファイル`);
  
  return results;
}

// ダウンロードフォルダの監視
let watchedFiles = new Set();

// ダウンロード追跡マップ: filename → { boothId, downloadId, timestamp }
const downloadTrackingMap = new Map();

function watchDownloadsFolder() {
  logInfo('=== ダウンロードフォルダ監視設定 ===');
  logInfo('監視対象:', DOWNLOADS_DIR);
  
  if (!fs.existsSync(DOWNLOADS_DIR)) {
    logWarn('⚠️ ダウンロードフォルダが見つかりません:', DOWNLOADS_DIR);
    logWarn('自動ダウンロード検知は動作しません');
    return;
  }
  
  try {
    fs.watch(DOWNLOADS_DIR, async (eventType, filename) => {
      logDebug(`ファイルシステムイベント: ${eventType}, ファイル名: ${filename}`);
      
      if (!filename || !filename.endsWith('.zip')) {
        logDebug('スキップ（非ZIPファイル）:', filename);
        return;
      }
      
      // 重複処理防止
      if (watchedFiles.has(filename)) {
        logDebug('スキップ（処理中）:', filename);
        return;
      }
      watchedFiles.add(filename);
      
      logInfo('=== 新規ZIPファイル検知 ===');
      logInfo('ファイル名:', filename);
      
      // ファイル書き込み完了待機
      logDebug('ファイル書き込み完了待機中（1秒）...');
      await new Promise(resolve => setTimeout(resolve, 1000));
      
      const zipPath = path.join(DOWNLOADS_DIR, filename);
      
      // ファイル存在確認
      if (!fs.existsSync(zipPath)) {
        logWarn('ファイルが見つかりません（削除された？）:', zipPath);
        watchedFiles.delete(filename);
        return;
      }
      
      const zipStats = fs.statSync(zipPath);
      logInfo(`ファイルサイズ: ${(zipStats.size / 1024 / 1024).toFixed(2)} MB`);
      
      // シンプルに展開（IDの特定は不要）
      logInfo('ZIP展開を開始します');
      await extractAndImportZip(zipPath, filename);
      
      watchedFiles.delete(filename);
      logDebug('ファイル処理完了:', filename);
    });
    
    logInfo('✓ ダウンロードフォルダ監視開始成功');
    
  } catch (e) {
    logError('ダウンロードフォルダ監視設定エラー:', e.message);
    logError('スタックトレース:', e.stack);
  }
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
  
  // 進捗情報取得エンドポイント
  if (req.method === 'GET' && req.url === '/progress') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(currentProgress));
    return;
  }
  
  if (req.method === 'POST' && req.url === '/sync') {
    logInfo('=== /sync エンドポイント呼び出し ===');
    let body = '';
    let bodySize = 0;
    
    req.on('data', chunk => {
      body += chunk.toString();
      bodySize += chunk.length;
    });
    
    req.on('end', async () => {
      try {
        logDebug(`受信データサイズ: ${bodySize} bytes`);
        
        const newAssets = JSON.parse(body);
        logInfo(`✓ 同期データ受信: ${newAssets.length}件`);
        
        // 各アセットの概要をログ出力
        if (newAssets.length > 0) {
          logDebug('受信アセット一覧:');
          newAssets.forEach((asset, index) => {
            if (index < 10) { // 最初の10件のみ表示
              logDebug(`  [${index + 1}] ${asset.id}: ${asset.title.substring(0, 30)}...`);
            }
          });
          if (newAssets.length > 10) {
            logDebug(`  ... 他 ${newAssets.length - 10}件`);
          }
        }
        
        // 既存データ読み込み
        logDebug('既存データ読み込み中...');
        let existingAssets = loadAssets();
        logInfo(`既存データ: ${existingAssets.length}件`);
        
        // 同期処理（IDごとに上書き・追加）
        logInfo('同期処理開始...');
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
              logDebug(`サムネイルダウンロード: ${newAsset.id}`);
              const success = await downloadThumbnail(newAsset.thumbnailUrl, thumbnailPath);
              if (success) {
                newAsset.localThumbnail = `BoothBridge/thumbnails/${thumbnailFilename}`;
                thumbnailDownloadCount++;
              } else {
                newAsset.localThumbnail = '';
                logWarn(`サムネイルダウンロード失敗: ${newAsset.id}`);
              }
            } else {
              logDebug(`サムネイル既存: ${newAsset.id}`);
              newAsset.localThumbnail = `BoothBridge/thumbnails/${thumbnailFilename}`;
            }
          }
          
          // 既存データの保持（installed, importPath）
          if (existingIndex >= 0) {
            const existing = existingAssets[existingIndex];
            newAsset.installed = existing.installed || false;
            newAsset.importPath = existing.importPath || '';
            existingAssets[existingIndex] = newAsset;
            updatedCount++;
            logDebug(`更新: ${newAsset.id}`);
          } else {
            newAsset.installed = false;
            newAsset.importPath = '';
            existingAssets.push(newAsset);
            addedCount++;
            logDebug(`追加: ${newAsset.id}`);
          }
        }
        
        // 保存
        logInfo('データ保存中...');
        saveAssets(existingAssets);
        
        logInfo('=== 同期処理完了 ===');
        logInfo(`✓ 更新: ${updatedCount}件`);
        logInfo(`✓ 追加: ${addedCount}件`);
        logInfo(`✓ サムネイルDL: ${thumbnailDownloadCount}件`);
        logInfo(`✓ 合計: ${existingAssets.length}件`);
        
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ 
          success: true, 
          count: newAssets.length,
          updated: updatedCount,
          added: addedCount,
          thumbnails: thumbnailDownloadCount
        }));
        
      } catch (e) {
        logError('=== 同期処理エラー ===');
        logError('エラーメッセージ:', e.message);
        logError('スタックトレース:', e.stack);
        logError('受信データサイズ:', bodySize, 'bytes');
        
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: false, error: e.message }));
      }
    });
  } else if (req.method === 'POST' && req.url === '/download-notify') {
    // Chrome拡張からダウンロード通知を受信
    logInfo('=== /download-notify エンドポイント呼び出し ===');
    let body = '';
    
    req.on('data', chunk => {
      body += chunk.toString();
    });
    
    req.on('end', () => {
      try {
        const data = JSON.parse(body);
        logInfo('✓ ダウンロード通知受信');
        logDebug('通知データ:', JSON.stringify(data, null, 2));
        
        // ファイル名と商品IDを紐付け
        if (data.filename && (data.boothId || data.downloadId)) {
          downloadTrackingMap.set(data.filename, {
            boothId: data.boothId,
            downloadId: data.downloadId,
            url: data.url,
            timestamp: data.timestamp || Date.now()
          });
          
          logInfo('✓ ダウンロード追跡登録完了');
          logInfo(`  ファイル名: ${data.filename}`);
          logInfo(`  商品ID: ${data.boothId || data.downloadId}`);
          logDebug(`  URL: ${data.url}`);
          logDebug(`  追跡マップサイズ: ${downloadTrackingMap.size}`);
          
          // 古いエントリを削除（1時間以上前）
          const oneHourAgo = Date.now() - 60 * 60 * 1000;
          let cleanedCount = 0;
          for (const [key, value] of downloadTrackingMap.entries()) {
            if (value.timestamp < oneHourAgo) {
              downloadTrackingMap.delete(key);
              cleanedCount++;
            }
          }
          
          if (cleanedCount > 0) {
            logDebug(`古い追跡情報を${cleanedCount}件削除しました`);
          }
        } else {
          logWarn('⚠️ 不完全な通知データ:', data);
        }
        
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: true, trackingMapSize: downloadTrackingMap.size }));
        
      } catch (e) {
        logError('=== ダウンロード通知エラー ===');
        logError('エラーメッセージ:', e.message);
        logError('スタックトレース:', e.stack);
        logError('受信データ:', body);
        
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
logInfo('=== HTTPサーバー起動 ===');
server.listen(PORT, 'localhost', () => {
  logInfo('✓ HTTPサーバー起動完了');
  logInfo(`  URL: http://localhost:${PORT}`);
  logInfo(`  エンドポイント: /sync, /download-notify`);
  logInfo('');
  logInfo('===========================================');
  logInfo('🚀 BOOTH Bridge 準備完了');
  logInfo('===========================================');
  logInfo('');
  logInfo('📝 利用方法:');
  logInfo('  1. Chrome拡張がインストールされているか確認');
  logInfo('  2. BOOTHライブラリページを開いて同期');
  logInfo('  3. ダウンロードしたZIPファイルが自動処理されます');
  logInfo('');
});

// エラーハンドリング
server.on('error', (err) => {
  logError('=== HTTPサーバーエラー ===');
  
  if (err.code === 'EADDRINUSE') {
    logError(`ポート ${PORT} は既に使用されています。`);
    logError('');
    logError('対処方法:');
    logError(`  1. 他のBridgeプロセスが起動していないか確認`);
    logError(`  2. ポート ${PORT} を使用している他のアプリケーションを終了`);
    logError(`  3. Bridgeを再起動`);
  } else if (err.code === 'EACCES') {
    logError(`ポート ${PORT} へのアクセスが拒否されました（権限エラー）`);
    logError('1024以下のポート番号には管理者権限が必要です');
  } else {
    logError('サーバーエラー:', err.message);
    logError('エラーコード:', err.code);
    logError('スタックトレース:', err.stack);
  }
  
  process.exit(1);
});

// ダウンロードフォルダ監視開始
watchDownloadsFolder();

// 終了処理
process.on('SIGINT', () => {
  logInfo('');
  logInfo('===========================================');
  logInfo('🛑 Bridge終了シグナル受信（SIGINT）');
  logInfo('===========================================');
  logInfo('');
  logInfo('クリーンアップ中...');
  
  // サーバーをクローズ
  server.close(() => {
    logInfo('✓ HTTPサーバー停止完了');
  });
  
  logInfo('✓ Bridge終了');
  process.exit(0);
});

process.on('SIGTERM', () => {
  logInfo('');
  logInfo('===========================================');
  logInfo('🛑 Bridge終了シグナル受信（SIGTERM）');
  logInfo('===========================================');
  logInfo('');
  logInfo('クリーンアップ中...');
  
  // サーバーをクローズ
  server.close(() => {
    logInfo('✓ HTTPサーバー停止完了');
  });
  
  logInfo('✓ Bridge終了');
  process.exit(0);
});

// 未処理のエラーをキャッチ
process.on('uncaughtException', (err) => {
  logError('');
  logError('===========================================');
  logError('💥 未処理の例外エラー');
  logError('===========================================');
  logError('エラーメッセージ:', err.message);
  logError('スタックトレース:', err.stack);
  logError('');
  logError('Bridgeを再起動してください');
  process.exit(1);
});

process.on('unhandledRejection', (reason, promise) => {
  logError('');
  logError('===========================================');
  logError('💥 未処理のPromise拒否');
  logError('===========================================');
  logError('理由:', reason);
  logError('Promise:', promise);
  logError('');
  logError('Bridgeを再起動してください');
  process.exit(1);
});


