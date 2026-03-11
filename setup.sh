#!/bin/bash

# スクリプトの実行を停止するエラーが発生した場合
set -e

echo "NDI-Telopプロジェクトの環境構築を開始します。"

# .NET SDK 8.0のインストール
# 以前のセッションでapt経由でインストール済みだが、他の環境での実行を考慮し含める
# Microsoftパッケージリポジトリの追加とキーの登録
if ! command -v dotnet &> /dev/null
then
    echo ".NET SDK 8.0をインストールします..."
    sudo apt update
    sudo apt install -y dotnet-sdk-8.0
    echo ".NET SDK 8.0のインストールが完了しました。"
else
    echo ".NET SDK 8.0は既にインストールされています。"
fi

# Gitのインストール（もし入っていなければ）
if ! command -v git &> /dev/null
then
    echo "Gitをインストールします..."
    sudo apt update
    sudo apt install -y git
    echo "Gitのインストールが完了しました。"
else
    echo "Gitは既にインストールされています。"
fi

# リポジトリのクローン
if [ ! -d "NDI-Telop" ]; then
    echo "NDI-Telopリポジトリをクローンします..."
    git clone https://github.com/rnakamura1997/NDI-Telop.git
    echo "NDI-Telopリポジトリのクローンが完了しました。"
else
    echo "NDI-Telopリポジトリは既に存在します。"
fi

cd NDI-Telop

# プロジェクトのビルド
echo "プロジェクトをビルドします..."
dotnet build src/NdiTelop/NdiTelop.csproj
echo "プロジェクトのビルドが完了しました。"

echo "環境構築が完了しました。"
