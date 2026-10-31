# Windows 11での起動・開発手順（v0.2）

## 配布ZIPから起動（通常はこちら）

1. v0.1のShelfを終了します。この版は別のフォルダーに展開してください。
2. GitHubの第2段階ブランチから `Shelf-Windows11-v0.2-Light.zip` をダウンロードします。
3. ZIPを右クリック →「すべて展開」→「展開」。ZIP内から直接実行しないでください。
4. 必要なら https://dotnet.microsoft.com/download/dotnet/8.0 から
   **.NET Desktop Runtime 8.0 / Windows x64**をインストールします。
   v0.1軽量版が動いたPCでは通常インストール済みです。
5. 展開先の`Shelf.Windows.exe`をダブルクリックします。DLLとJSONはすべて同じフォルダーに残します。
   管理者として起動しないでください。

最初はメイン画面左端、以後は保存したモニターの左端に表示します。
右上の`‹`で畳み、帯の左クリックで展開します。帯にもファイルをドロップできます。
モニター選択欄または帯の右クリックから、配置先を変更できます。
`DISPLAYn`はWindowsのデバイス識別子で、Windows設定画面の「識別」番号と一致するとは限りません。
解像度と左上座標を合わせて判断してください。座標は物理pxで、左・上の画面は負になります。
モニターを外すとメイン画面へ戻し、その配置先を保存します。再接続後は手動で選び直してください。
タスクバーを避けた作業領域の左端・上下中央に配置します。

終了は右上の`×`、帯の右クリック→終了、またはAlt+F4です。
未署名のためSmartScreenなどの警告が出る可能性があります。保護機能は無効化せず、入手元を確認してください。

## 保存先と復旧

Win+Rを押して `%LOCALAPPDATA%\Shelf` と入力すると保存フォルダーを開けます。

- `items.json`: 登録した元ファイルのパス。元ファイル本体は入っていません。
- `settings.json`: モニター識別子と折り畳み状態。
- `*.json.bak`: 直前の保存データ。2回目以降の保存で生成します。

保存は登録・除去・設定変更時です。ドラッグアウトでは一覧を消しません。
保存に失敗しても元ファイルは変わらず、棚に警告を表示します。帯では`!`で知らせます。
保存できない変更があると、終了時に確認します。

壊れた/未対応の保存データは自動上書きしません。有効なバックアップがあれば表示しますが、
そのファイルへの保存は停止します。復旧するには:

1. Shelfを終了します（未保存変更がある場合は先に内容を確認）。
2. 保存フォルダー全体を別の場所へコピーして保管します。
3. 問題のJSONを別名へ退避します。有効な`.bak`がある場合は**コピーして**元のJSON名に戻します。
   バックアップを使えない場合は退避したままにし、次の起動で空の一覧/既定設定から開始できます。
4. 起動して警告が解消するか確認します。判断できない場合は退避データを残して相談してください。

アプリの配布フォルダーを削除してもAppDataの保存データと元ファイルは残ります。

## ソースからビルド

Microsoft公式サイトから.NET 8 SDKを導入し、PowerShellを開き直します。
Visual Studioは必須ではありません。GitHubの `shelf/windows11-v0.2` ブランチのソースを取得し、
`windows`フォルダーのアドレスバーに`powershell`と入力してEnterを押します。
次を1行ずつ実行し、エラー時は後続を止めてください。

```powershell
dotnet --list-sdks
dotnet restore .\Shelf.Windows.sln
dotnet build .\Shelf.Windows.sln -c Release --no-restore
dotnet run --project .\tests\Shelf.Core.Tests -c Release --no-build
dotnet run --project .\src\Shelf.Windows -c Release --no-build
```

テストはコンソール形式で、合計と失敗件数を出力します。`dotnet test`では実行されません。
テストは専用の一時ディレクトリを使い、実際のAppDataは変更しません。

## 軽量ZIPの再生成

```powershell
.\Publish-Light.ps1
```

`artifacts\Shelf-Windows11-v0.2-Light.zip`ができます。
既存の同名ZIPは上書きせず停止します。前回分を別名に退避してから再実行してください。
スクリプト実行が組織のポリシーで制限される場合、制限を変更せず次の方法を使えます。

```powershell
dotnet publish .\src\Shelf.Windows -c Release -r win-x64 --self-contained false -p:DebugType=None -p:DebugSymbols=false -o .\artifacts\Shelf-v0.2-Manual
Copy-Item .\docs\Light-README.txt .\artifacts\Shelf-v0.2-Manual\README-JA.txt
Copy-Item .\docs\Windows-Test-Checklist.md .\artifacts\Shelf-v0.2-Manual\Windows-Test-Checklist.md
Compress-Archive -Path .\artifacts\Shelf-v0.2-Manual\* -DestinationPath .\artifacts\Shelf-Windows11-v0.2-Light.zip
```

手動の場合も新しい出力フォルダーを使ってください。このコマンドはx64専用です。
このクラウドでは同等のpublishとZIP生成を実行していますが、PowerShellスクリプトの実行はWindows実機で未検証です。

## よくある問題

- 起動時にランタイム要求: 通常の.NET Runtimeだけでなく **Desktop Runtime 8 x64** を導入します。
- ファイルを登録できない: 管理者として起動していないか、通常の実在ファイルか確認します。
- モニターが見つからない: 拡張表示に設定されているか確認します。複製表示では別々の配置先にならないことがあります。
- 第2段階の3画面/DPI/ドロップ/保存復元: Windows実機でチェックリストに沿って確認してください。
