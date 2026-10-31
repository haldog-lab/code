# 第1段階の検証記録

検証場所: Linuxクラウド環境。SDK: .NET 8.0.427。

## 確認済み

- `dotnet build Shelf.Windows.sln -c Release`: 成功、警告0・エラー0。
- `dotnet publish src/Shelf.Windows/Shelf.Windows.csproj -c Release -r win-x64 --self-contained true -o artifacts/Shelf-win-x64`: 成功。
- 生成した`Shelf.Windows.exe`はWindows x64のPE GUI実行ファイル。
- ソース確認: 元ファイルへの書き込み・移動・削除APIを使用していない。
- ソース確認: ドラッグアウトで許可する操作はCopyのみ。完了後の一覧除去なし。
- ソース確認: 一覧からの除去は明示的なボタン操作だけ。
- Git作業ツリー: 既存の追跡ファイルに変更なし。追加はwindows配下のみ。

## 未検証

Windows 11でのビルド、起動、画面左端配置、最前面表示、DPI、実際のドラッグ＆ドロップ、
元ファイルの前後比較、SDK未導入端末での配布版起動は未検証。
LinuxでWPFは実行できないため、Windows-Test-Checklist.mdで実機確認が必要。
自動UIテスト・単体テストは今回追加・実行していない。ビルド成功は実動作の保証ではない。

生成物は`windows/artifacts/Shelf-win-x64/`にあり、bin/obj/artifactsはGit対象外。
Windowsへ渡すときは公開フォルダー全体をコピーする。ARM版は生成・検証していない。
環境設定の下書き更新や公開、Gitへのコミット・プッシュは行っていない。
