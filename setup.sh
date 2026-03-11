#!/bin/bash

# スクリプトの実行を停止するエラーが発生した場合
set -e

echo "NDI-Telopプロジェクトの環境構築を開始します。"

# .NET SDK 8.0のインストール
if ! command -v dotnet &> /dev/null
then
    echo ".NET SDK 8.0をインストールします..."
    sudo apt update
    sudo apt install -y dotnet-sdk-8.0
    echo ".NET SDK 8.0のインストールが完了しました。"
else
    echo ".NET SDK 8.0は既にインストールされています。"
fi

# Gitのインストール
if ! command -v git &> /dev/null
then
    echo "Gitをインストールします..."
    sudo apt update
    sudo apt install -y git
    echo "Gitのインストールが完了しました。"
else
    echo "Gitは既にインストールされています。"
fi

# プロジェクトのビルド
echo "プロジェクトをビルドします..."
dotnet build src/NdiTelop/NdiTelop.csproj
echo "プロジェクトのビルドが完了しました。"

echo "環境構築が完了しました。"
