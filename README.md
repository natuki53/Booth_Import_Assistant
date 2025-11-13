# BOOTH Import Assistant

**BOOTHライブラリから直接Unityプロジェクトにアセットをインポートするツール**  
VRChatアバター改変の効率化ツールです。

<div align="center">

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-blue.svg)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg)]()
[![Version](https://img.shields.io/badge/Version-1.0.2-green.svg)](https://github.com/natuki53/Booth_Import_Assistant/releases)

</div>

---

## 特徴

- **Node.js組み込み済み** - 追加インストール不要！
- **VCC対応** - ワンクリックでインストール
- **自動インポート** - ダウンロード→展開→インポートを全自動化
- **プログレスバー** - リアルタイムで進捗を表示
- **複数ダウンロード対応** - アバター別・マテリアル別に自動分類
- **完全ローカル** - 外部通信なし・セキュア設計
- **クロスプラットフォーム** - Windows/Mac/Linux対応

---

## インストール方法

<div align="center">

[Add to VCC](https://natuki53.github.io/Booth_Import_Assistant/)

</div>

**使い方:**
1. 上の「VCCに追加」ボタンをクリック
2. VCCが自動起動してリポジトリが追加されます
3. プロジェクトに移動して「BOOTH Import Assistant」を追加
4. 完了！Node.jsのインストールは不要です

---

### 手動インストール

1. [Releases](https://github.com/natuki53/Booth_Import_Assistant/releases) から最新版をダウンロード
2. `Assets/BoothImportAssistant/` フォルダを自分のUnityプロジェクトの `Assets/` フォルダにコピー
3. 完了！ **Node.jsのインストールは不要です**

### ブラウザ拡張機能のインストール（必須）

1. Chrome または Edge を開く
2. `chrome://extensions/` にアクセス
3. 右上の「デベロッパーモード」を有効化
4. 「パッケージ化されていない拡張機能を読み込む」をクリック
5. リポジトリの `BoothExtension/` フォルダを選択

---

## 使い方

### 初回セットアップ

1. **Unity Editorで「BOOTH Library」を開く**
   - メニューから `Tools > BOOTH Library` を選択

2. **「同期」ボタンをクリック**
   - BOOTHライブラリページが開きます
   - 全ページを自動的に巡回してデータを取得

3. **完了！**
   - Unity側に購入したアセット一覧が表示されます
   - サムネイル付きで見やすく表示

### アセットのダウンロードとインポート

1. **Unity側で「DL」ボタンをクリック**
   - BOOTHのダウンロードページが開きます

2. **BOOTHでダウンロードボタンをクリック**
   - ZIPファイルがダウンロードフォルダに保存されます
   - ファイル名の変更は不要です

3. **自動的にインポート開始！**
   - プログレスバーで進捗を確認
   - ZIP展開 → .unitypackage検出 → Unityにインポート
   - インポート後、.unitypackageファイルは自動削除

#### 複数ダウンロード対応

**アバター別ダウンロード:**
- ドロップダウンメニューで選択
- 例：「萌ちゃん用」「桔梗ちゃん用」

**マテリアル:**
- 別ボタンで表示
- 「マテリアル」「マテリアル 2」など

---

## 動作フロー

```
1. Unity「同期」ボタン
   ↓
2. Bridge自動起動（Node.js）
   ↓
3. BOOTHライブラリページが開く
   ↓
4. 拡張機能が全ページを自動巡回
   ↓
5. Bridgeへデータ送信
   ↓
6. JSON保存・サムネイルDL
   ↓
7. Unity UIが自動更新
   ↓
8. ダウンロード検知
   ↓
9. 自動展開・インポート
   ↓
10. 完了！
```

---

## 必要なもの

- Unity 2022.3.
- Chrome または Edge ブラウザ
- BOOTHアカウント

**注意**: Node.jsは**パッケージに組み込まれています**。追加インストールは不要です！

---

## セキュリティについて

本ツールは以下のセキュリティ方針で設計されています：

- **外部通信なし**: すべての処理がローカルで完結
- **認証情報不使用**: Cookie・トークン・パスワードを扱わない
- **BOOTH公式リンクのみ**: 商品ページは公式URLのみ使用
- **localhost限定**: Bridgeはローカルホストのみ
- **ユーザー操作必須**: 自動ログイン・自動ダウンロードなし

**本ツールはBOOTH公式ツールではありません。個人の責任でご利用ください。**

---

## 技術スタック

- **Unity Editor拡張**: C#
- **Bridge サーバー**: Node.js v20
- **ブラウザ拡張**: JavaScript (Manifest V3)
- **通信**: HTTP (localhost:49729)
- **データ形式**: JSON

---

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) を参照

**Node.js**: MIT License (組み込みバイナリ)

---

## 免責事項

本ツールは非公式ツールです。BOOTHの仕様変更により動作しなくなる可能性があります。
本ツールの使用により生じたいかなる損害についても、作者は一切の責任を負いません。

---

## 関連リンク

- **BOOTH**: https://booth.pm/
- **VCC**: https://vcc.docs.vrchat.com/
- **ALCOM**: https://vrc-get.anatawa12.com/ja/alcom/
