# EffekseerForYMM4

[![Build Status](https://github.com/takoyakisoft/EffekseerForYMM4/actions/workflows/build.yml/badge.svg)](https://github.com/takoyakisoft/EffekseerForYMM4/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)

![Image](docs/EffekseerForYMM4.png)

Effekseerで作成したエフェクトを、ゆっくりMovieMaker4（YMM4）上で再生・合成するためのプラグインです。

<p align="center">
  <img src="docs/sample.gif" width="100%" alt="sample">
</p>

## v1.2.0 の主な変更

- GitHub Releasesに加えてBOOTH向けの配布zipを整備
- 透視投影／正投影の切り替えと正投影サイズの設定を追加
- 日本語、絵文字、CP932外文字、長いパスを含むエフェクトファイルの読み込みを改善
- 連続再生時の状態更新を見直し、不要なエフェクト再生成や一時割り当てを削減
- ネイティブブリッジをC++/CLIから純粋なネイティブC++のC ABI DLLへ移行
- Effekseerエフェクトから参照される音声ファイルを再生する音声エフェクトを現行ネイティブ構成へ移行

## インストール方法

1. [GitHub Releases](https://github.com/takoyakisoft/EffekseerForYMM4/releases)またはBOOTHから最新の`EffekseerForYMM4-vX.Y.Z.zip`をダウンロードします。
2. zipを展開し、`EffekseerForYMM4-vX.Y.Z.ymme`を開きます。
3. YMM4の案内に従ってプラグインをインストールします。
4. YMM4が起動中の場合は再起動します。

## 使い方

1. 映像エフェクトに「Effekseerビデオエフェクト」が追加されます。
2. エフェクトファイル（`.efkefc` / `.efk`）を選択して再生します。
3. 音声も使用する場合は、同じアイテムの音声エフェクトに「Effekseer音声エフェクト」を追加し、同じエフェクトファイルを選択します。

### エフェクトファイルの入手と作成

エフェクトファイル（`.efkefc` / `.efk`）は、Effekseerを使用して作成・編集できます。

以下からEffekseerをダウンロードし、同梱されている`Sample`フォルダ内のエフェクトを使用するか、ご自身でエフェクトを作成してください。

[Effekseer 1.7.3.0（Windows版）](https://github.com/effekseer/Effekseer/releases/download/1.7.3.0/Effekseer1.7.3.0Win.zip)

**注意：**

`.efkproj`はEffekseerのプロジェクトファイルのため、本プラグインから直接読み込むことはできません。

Effekseerでプロジェクトを開き、「ファイル」→「エクスポート」→「標準形式」からEffekseerファイル（`*.efk`）として保存してください。

参照するテクスチャや音声などの外部リソースを正しく読み込むため、書き出し先は`.efkproj`と同じフォルダーを推奨します。別の場所に保存した場合、外部リソースへの相対パスが解決できず、正しく表示・再生されないことがあります。

## 動作環境

- YukkuriMovieMaker4 v4.49.0.2
- Windows 11（64bit）
- DirectX 11対応のグラフィックス環境

## 開発者向け

### ソリューション構成

- `EffekseerForNative`
  - 純粋ネイティブC++のC ABI DLLです。
  - Effekseer本体、DX11レンダラー、C ABI境界を1プロジェクトでビルドします。
- `EffekseerForYMM4`
  - YMM4プラグイン本体です。
  - UI、ローカライズ、エフェクト描画、ネイティブDLL呼び出しを担当します。
- `YukkuriMovieMaker.Generator`
  - 翻訳CSVから`resx`とクラスを生成するソースジェネレーターです。
- `EffekseerForYMM4.Tests`
  - プラグインの回帰テストを格納するテストプロジェクトです。
  - 通常のプラグインビルドには必要ありません。

### 描画処理

描画にはYMM4のDirect3D 11デバイスとImmediate Contextを借用します。

ネイティブ側はデバイスとImmediate ContextのCOM参照を保持しますが、デバイス自体は作成・所有しません。通常の連続描画ホットパスではmanaged heap allocationを行いません。共有Immediate Contextを使用する描画区間はC#側で直列化し、ネイティブ描画後にRenderTargetとViewportを描画前の状態へ復元します。

### ビルド構成

- 通常の開発では`Debug|x64`を使用します。
- 配布物の確認やGitHub Actionsと同じ条件で確認する場合は`Release|x64`を使用します。
- このリポジトリでは実行対象を`x64`に固定しています。

### 配布用ファイル

- 翻訳リソースはビルド時に`ar-sa`、`en-us`、`es-es`、`id-id`、`ko-kr`、`zh-cn`、`zh-tw`の`EffekseerForYMM4.resources.dll`として出力されます。
- ネイティブDLLはプラグインフォルダ直下に`EffekseerForNative.dll`として配置し、その場所から直接読み込みます。
- 診断ログはプラグインフォルダ直下の`EffekseerForYMM4.log`へ出力します。単一ファイルが2MBを超えると古い行を削除して末尾約1MBを保持します。
- Releaseビルドの既定レベルはWarning、DebugビルドはInformationです。必要な場合は環境変数`EFFEKSEERFORYMM4_LOG_LEVEL`で変更できます。
- BOOTH配布用zipには、YMM4インストーラー用の`EffekseerForYMM4-vX.Y.Z.ymme`、`Readme.txt`、`THIRD_PARTY_NOTICES.txt`を格納します。
- `ymme`にはプラグインDLL、動作に必要なネイティブDLL、翻訳リソース、`Readme.txt`、`LICENSE.txt`、`THIRD_PARTY_NOTICES.txt`だけを格納します。YMM4本体が提供するDLLは同梱しません。

### ビルド前提

- `Directory.Build.props`に`YMM4DirPath`を設定すると、ビルド後にYMM4の`user/plugin/EffekseerForYMM4`へプラグインが自動コピーされます。
- GitHub Actionsでは最新の通常版YMM4を取得し、`Release|x64`でビルドしてBOOTH配布用zipを生成します。
- リリースバージョンは`Directory.Build.targets`の`EffekseerForYMM4Version`で管理します。

## ライセンス

このソフトウェアはMITライセンスの下で公開されています。Effekseerを含む第三者コンポーネントの著作権表示とライセンス条項は`THIRD_PARTY_NOTICES.txt`を参照してください。

### 使用ライブラリ・関連ソフトウェア

- **Effekseer**（v1.7.3.0）- MIT License
- **YukkuriMovieMaker4**（v4.49.0.2）

### 謝辞

このプラグインは、以下のプロジェクト・ライブラリのおかげで実現できました。開発者の皆様に心より感謝いたします。

- [YukkuriMovieMaker4](https://manjubox.net/ymm4/) - 饅頭遣い様
- [Effekseer](https://effekseer.github.io/jp/) - Effekseer Project
