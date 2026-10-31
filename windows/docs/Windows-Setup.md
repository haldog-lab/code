# Windows 11で起動する手順

## 1. 開発用の道具を入れる

1. Microsoft公式の https://dotnet.microsoft.com/download/dotnet/8.0 を開きます。
2. 「.NET SDK」のWindows版をダウンロードしてインストールします。
   通常のIntel/AMDパソコンはx64、ARMパソコンはArm64を選びます。
   「Runtime」だけではビルドできません。Visual Studioは必須ではありません。
3. インストール後、新しいPowerShellを開いて次を入力します。

```powershell
dotnet --list-sdks
```

`8.0.xxx`が表示されることを確認します。初回の復元にはインターネット接続が必要です。

## 2. ソースを配置する

この変更を含むリポジトリをWindowsにコピーするか、GitHubから取得します。
ZIPで取得した場合は先に展開してください。GitHubに未反映の場合は、この作業環境の
`windows`フォルダー全体をWindowsにコピーしてください。

エクスプローラーで`windows`フォルダーを開き、アドレスバーに`powershell`と入力してEnterを押します。
以下のコマンドはすべて、この`windows`フォルダーで実行します。

## 3. ビルドして起動する

次のコマンドを1行ずつ入力します。失敗した場合は後続の実行を止め、エラーを確認します。

```powershell
dotnet restore .\Shelf.Windows.sln
dotnet build .\Shelf.Windows.sln -c Release --no-restore
dotnet run --project .\src\Shelf.Windows\Shelf.Windows.csproj -c Release --no-build
```

プライマリモニターの左端にShelfウィンドウが出ます。エクスプローラーから
テスト用ファイルをドロップすると、名前とパスが表示されます。
その行を別のフォルダーへドラッグするとコピーされます。棚の項目は残ります。
通常の閉じるボタンで終了します。管理者として実行する必要はありません。

## 4. 配布用の実行ファイルを作る

Intel/AMDのWindows 11用:

```powershell
dotnet publish .\src\Shelf.Windows\Shelf.Windows.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\Shelf-win-x64
```

`artifacts\Shelf-win-x64`の中に`Shelf.Windows.exe`ができます。
**フォルダー全体**を任意の場所へコピーし、その中のexeをダブルクリックします。
exeだけを取り出さないでください。配布先への.NETランタイムの追加導入は不要です。
ARM版が必要な場合は`win-x64`を`win-arm64`に置き換えて生成し、ARM実機で確認します。

初期版はフォルダー配布です。インストーラーや自動起動の登録はありません。
アンインストールは終了後に配置した配布フォルダーを削除します。
未署名のためSmartScreenなどが警告する可能性があります。保護機能を無効化せず、
入手元と内容を確認してください。組織端末の制限は管理者に確認してください。

## 困ったとき

- `dotnet`が見つからない: SDKを確認し、PowerShellを開き直します。
- 復元エラー: インターネット接続とNuGetへのアクセスを確認します。
- ドロップできない: Shelfを管理者権限で起動していないか確認します。
- 登録できない: 今回は実在する通常のファイルだけが対象です。フォルダーやブラウザー内の仮想ファイルは対象外です。
- 再起動で一覧が消える: 第1段階の仕様です。元ファイルは残ります。
