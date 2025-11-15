#!/bin/bash

# VPMパッケージ用ZIPファイルを作成するスクリプト
# Windows互換性を確保するため、パスを正規化してZIPを作成

VERSION=$1
if [ -z "$VERSION" ]; then
    echo "Usage: ./create-zip.sh <version>"
    echo "Example: ./create-zip.sh 1.0.4"
    exit 1
fi

ZIP_NAME="com.natuki.booth-import-assistant-${VERSION}.zip"
TEMP_DIR=$(mktemp -d)

echo "Creating ZIP file: ${ZIP_NAME}"

# 一時ディレクトリにファイルをコピー（パスを正規化）
cd Assets/BoothImportAssistant
find . -type f ! -name "*.DS_Store" ! -path "*/node_modules/*" ! -path "*/__MACOSX/*" ! -name "*.meta" | while read file; do
    # パスを正規化（スラッシュに統一）
    normalized_path=$(echo "$file" | sed 's|^\./||' | tr -d '\r')
    dest_path="${TEMP_DIR}/${normalized_path}"
    mkdir -p "$(dirname "$dest_path")"
    cp "$file" "$dest_path"
done

# ZIPファイルを作成（-Xオプションで余分なメタデータを除外、-rで再帰的）
cd "$TEMP_DIR"
zip -r -X "../../${ZIP_NAME}" . -x "*.DS_Store" "*/node_modules/*" "*/__MACOSX/*" "*.meta"

# 一時ディレクトリを削除
cd ../..
rm -rf "$TEMP_DIR"

# SHA256ハッシュを計算
if command -v shasum &> /dev/null; then
    SHA256=$(shasum -a 256 "${ZIP_NAME}" | awk '{print toupper($1)}')
    echo "SHA256: ${SHA256}"
    echo ""
    echo "Add this to index.json:"
    echo "  \"zipSHA256\": \"${SHA256}\","
fi

echo "ZIP file created: ${ZIP_NAME}"

