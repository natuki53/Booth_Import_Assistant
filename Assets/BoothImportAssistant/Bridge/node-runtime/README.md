# Node.js Runtime for BOOTH Import Assistant

このフォルダには、プラットフォーム別のNode.jsランタイムバイナリを配置します。

## 📦 ダウンロード方法

VCC配布前に、以下のNode.jsバイナリをダウンロードして配置してください。

### Windows (win-x64)

1. **Node.js v20.x (LTS) をダウンロード**:
   - https://nodejs.org/dist/v20.11.0/node-v20.11.0-win-x64.zip
   
2. **解凍して配置**:
   ```
   node-v20.11.0-win-x64/
   └─ node.exe  ← これを win-x64/ フォルダにコピー
   ```

3. **最終的な配置**:
   ```
   Assets/BoothImportAssistant/Bridge/node-runtime/win-x64/node.exe
   ```

### macOS (osx-x64)

1. **Node.js v20.x (LTS) をダウンロード**:
   - Intel Mac: https://nodejs.org/dist/v20.11.0/node-v20.11.0-darwin-x64.tar.gz
   - Apple Silicon: https://nodejs.org/dist/v20.11.0/node-v20.11.0-darwin-arm64.tar.gz

2. **解凍して配置**:
   ```bash
   tar -xzf node-v20.11.0-darwin-x64.tar.gz
   cp node-v20.11.0-darwin-x64/bin/node osx-x64/
   chmod +x osx-x64/node
   ```

3. **最終的な配置**:
   ```
   Assets/BoothImportAssistant/Bridge/node-runtime/osx-x64/node
   ```

### Linux (linux-x64)

1. **Node.js v20.x (LTS) をダウンロード**:
   - https://nodejs.org/dist/v20.11.0/node-v20.11.0-linux-x64.tar.xz

2. **解凍して配置**:
   ```bash
   tar -xJf node-v20.11.0-linux-x64.tar.xz
   cp node-v20.11.0-linux-x64/bin/node linux-x64/
   chmod +x linux-x64/node
   ```

3. **最終的な配置**:
   ```
   Assets/BoothImportAssistant/Bridge/node-runtime/linux-x64/node
   ```

## 🎯 最終的なフォルダ構造

```
node-runtime/
  ├─ win-x64/
  │   └─ node.exe          (約50MB)
  ├─ osx-x64/
  │   └─ node              (約45MB)
  ├─ linux-x64/
  │   └─ node              (約45MB)
  └─ README.md
```

## ⚙️ 動作確認

バイナリを配置後、Unity Editorで：

1. 「Tools」→「BOOTH Library」を開く
2. 「同期」ボタンをクリック
3. Unityコンソールに以下のログが表示されるか確認:
   ```
   [BoothBridge] バンドルされたNode.jsを使用: Assets/BoothImportAssistant/Bridge/node-runtime/win-x64/node.exe
   ```

## 📝 注意事項

- **ファイルサイズ**: 合計約140MB
- **.gitignore**: `node-runtime/*/node*` を追加して、開発中は除外
- **配布時**: VCCリリース時のみこれらのバイナリを含める
- **ライセンス**: Node.jsはMITライセンス（再配布可能）

## 🔄 更新方法

Node.jsのバージョンを更新する場合：

1. 新しいバージョンのバイナリをダウンロード
2. 古いバイナリと置き換え
3. 動作確認
4. VCC配布パッケージを更新

## ⚠️ 開発者向け

開発中にシステムのNode.jsを使いたい場合：

- バンドルされたNode.jsが見つからない場合、自動的にシステムのNode.jsにフォールバックします
- `BridgeManager.cs` の `FindNodePath()` メソッドを参照

