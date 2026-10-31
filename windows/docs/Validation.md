# 第2段階 v0.2 の検証記録

## 第1段階から引き継いだ証拠

v0.1について利用者からWindows 11実機で次の成功報告があります。
起動、ファイル登録、複数ファイル表示、別フォルダーへのドラッグアウト、取り出し後の棚への保持。
これはv0.2の実機確認結果ではありません。第2段階で再確認が必要です。

## 今回確認したこと

検証場所: Linuxクラウド環境、.NET SDK 8.0.427。

```sh
dotnet build windows/Shelf.Windows.sln -c Release -m:1 -nr:false
dotnet run --project windows/tests/Shelf.Core.Tests -c Release --no-build
dotnet publish windows/src/Shelf.Windows/Shelf.Windows.csproj -c Release -r win-x64 --self-contained false -p:DebugType=None -p:DebugSymbols=false -m:1 -nr:false -o /workspace/shelf-distribution/v0.2-Light
```

- Releaseクロスビルド成功: 警告0、エラー0。
- コンソールテスト: **23件成功、0件失敗**。ゼロ件実行ではない。
- テスト範囲: 複数パスの保存復元、元ファイルの内容/場所/更新日時の保持、バックアップ更新、
  破損・未対応・不完全データの保護、参照先消失、保存不能、設定復元、負の座標、125/150/200%の配置計算。
- Windows x64向け軽量publish成功。アプリとCore DLL、deps/runtimeconfig JSON、Windows exeを生成。
- ドラッグデータ生成クラスとインターフェースはv0.1から変更なし。
  ドラッグアウトの操作は引き続きCopyのみで、自動除去処理はない。
- 書き込み対象はAppDataのアプリ用JSON・バックアップ・固有の一時ファイル。
  保存処理には元ファイルのパスを保存データとして渡すだけで、元ファイルへの書き込みには使用しない。
- ZIPはランタイム非同梱。Windowsの.NET 8 Desktop Runtime x64を使用する。
- v0.2軽量ZIP: 92,834バイト、7ファイル。ZIP整合性と各バイナリのpublish出力との一致を確認。
  秘密情報の既知パターンは検出されず、アプリ・JSON・説明書・チェックリストのみを収録。
  SHA256: `509314f910f8dc85f64f0bf69fe3208f815e86b0ed8343ae2ac5b6d67d52e94c`。
- v0.1軽量ZIPは元のSHA256と一致し、変更していない。

クラウドの通常並列ビルドはエラー詳細なしで終了したため、単一MSBuildノードとノード再利用なしで実行した。
コンパイル失敗ではなく、上記コマンドで全プロジェクトのコンパイルとテストを完了している。

## Windows実機で未検証のこと

- v0.2の起動、折り畳み、帯のクリックとドロップ、既存ドラッグ＆ドロップの回帰。
- Windows APIによる実際の3画面列挙・選択・配置・切断復帰。
- 混在DPI下でのWPFレイアウト、Windowsメッセージ処理、解像度・倍率変更、スリープ復帰。
- 実際のAppDataでの保存・復元・Windows上のファイル置換、二重起動防止。
- PowerShell配布スクリプトの実行、SDK未導入端末での軽量版起動。

保存と座標計算のテスト成功は、Win32/WPFの実際の表示・操作の成功を意味しない。
Windows-Test-Checklist.mdで確認してください。

## 変更範囲

第1段階を起点とする `shelf/windows11-v0.2` ブランチで開発。
変更対象はwindows配下と、新しい `releases/Shelf-Windows11-v0.2-Light.zip`。
第1段階のZIPはそのまま残し、mainへの変更・マージは行わない。
