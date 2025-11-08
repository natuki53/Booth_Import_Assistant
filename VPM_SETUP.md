# VPMリポジトリセットアップガイド

このドキュメントは、BOOTH Import AssistantをVPM（VRChat Package Manager）リポジトリとして配布するためのセットアップ手順です。

## 📋 前提条件

- GitHubアカウント
- リポジトリの管理者権限

## 🚀 セットアップ手順

### 1. GitHub Pagesを有効化

1. GitHubリポジトリページを開く
2. 「Settings」→「Pages」に移動
3. 「Source」を「Deploy from a branch」に設定
4. 「Branch」を「gh-pages」に設定
5. 「Save」をクリック

### 2. ワークフローの実行

GitHub Actionsが自動的に実行され、`index.json`がGitHub Pagesにデプロイされます。

- ワークフロー: `.github/workflows/deploy-vpm.yml`
- デプロイ先: `https://natuki53.github.io/Booth_Import_Assistant/index.json`

### 3. 動作確認

以下のURLにアクセスして、JSONが正しく表示されることを確認：

```
https://natuki53.github.io/Booth_Import_Assistant/index.json
```

### 4. VCC/ALCOMでテスト

1. VCCまたはALCOMを開く
2. Settings → Packages → Add Repository
3. URLを入力: `https://natuki53.github.io/Booth_Import_Assistant/index.json`
4. パッケージが表示されることを確認

## 📦 リリース手順

### 新しいバージョンをリリースする場合

1. **package.jsonのバージョンを更新**
   ```json
   {
     "version": "1.0.1"
   }
   ```

2. **index.jsonに新しいバージョンを追加**
   ```json
   {
     "packages": {
       "com.natuki.booth-import-assistant": {
         "versions": {
           "1.0.1": {
             "version": "1.0.1",
             "url": "https://github.com/natuki53/Booth_Import_Assistant/releases/download/v1.0.1/com.natuki.booth-import-assistant-1.0.1.zip",
             ...
           }
         }
       }
     }
   }
   ```

3. **パッケージZIPを作成**
   ```bash
   # Assets/BoothImportAssistant/ フォルダをZIPに圧縮
   cd Assets
   zip -r ../com.natuki.booth-import-assistant-1.0.1.zip BoothImportAssistant/
   ```

4. **GitHub Releaseを作成**
   - Tag: `v1.0.1`
   - Title: `BOOTH Import Assistant v1.0.1`
   - ZIPファイルをアップロード

5. **コミット＆プッシュ**
   ```bash
   git add .
   git commit -m "Release v1.0.1"
   git push
   ```

6. **GitHub Actionsが自動デプロイ**
   - `index.json`がGitHub Pagesに更新されます

## 🔧 index.jsonの構造

```json
{
  "name": "リポジトリ名",
  "author": "作者名",
  "url": "リポジトリURL",
  "id": "リポジトリID",
  "packages": {
    "パッケージ名": {
      "versions": {
        "バージョン": {
          "name": "パッケージ名",
          "displayName": "表示名",
          "version": "バージョン",
          "unity": "Unityバージョン",
          "description": "説明",
          "url": "ZIPファイルのURL",
          "zipSHA256": "SHA256ハッシュ（オプション）"
        }
      }
    }
  }
}
```

## 📝 注意事項

- `index.json`はGitHub Pagesでホストされるため、リポジトリの`gh-pages`ブランチに配置されます
- ZIPファイルはGitHub Releasesでホストします（大きなファイルをgit履歴に含めないため）
- VCC/ALCOMは`index.json`のURLを直接参照します

## 🔗 参考リンク

- [VPM公式ドキュメント](https://vcc.docs.vrchat.com/vpm/)
- [VPMリポジトリの作り方](https://vcc.docs.vrchat.com/vpm/repos)
- [GitHub Pages](https://pages.github.com/)

