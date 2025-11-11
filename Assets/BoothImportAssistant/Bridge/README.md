# BOOTH Import Assistant - Bridge Server

Node.jsベースのローカルHTTPサーバー

## 機能

1. **同期エンドポイント** (`POST /sync`)
   - ブラウザ拡張から購入リスト受信
   - JSON保存
   - サムネイルダウンロード

2. **ZIP監視**
   - Windowsダウンロードフォルダを監視
   - `booth_<ID>.zip` を自動検知
   - Unityプロジェクトに自動展開

3. **データ管理**
   - JSON自動バックアップ（1世代）
   - サムネイルキャッシュ管理

## セットアップ

```bash
npm install
```

## 起動方法

Unityから自動起動されます。手動起動する場合：

```bash
node bridge.js --projectPath "C:/Path/To/UnityProject"
```

## 設定

- **ポート**: 49729（固定）
- **バインド**: localhost のみ
- **監視フォルダ**: `%USERPROFILE%/Downloads`

## ログ

すべてのログは `[BoothBridge]` プレフィックス付きで標準出力されます。
Unityコンソールにリダイレクトされます。

## エラーハンドリング

- ポート競合: エラーログ出力して終了
- サムネDL失敗: 警告ログ出力して継続
- ZIP展開失敗: エラーログ出力して継続

## 依存パッケージ

- `adm-zip`: ZIP展開用

