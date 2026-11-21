# Mind8ディスパッチャ C#互換実装 基本サンプル完走までの修正案 v2

調査日: 2026-11-17

実施後追記（2026-11-20）: 531件解消作業の区切り3は継続中。最新の回帰テストは73/73成功し、hello/variables/binarysearch/bubblesort/insertsortのfixtureもすべて完走した。
ユーザー確認により元MCOと正規版のCRLF出力を維持したため、探索・整列3件は参照LFをCRLFに展開した期待値と一致する。
第1・4・5章は実施前の調査・計画の記録、第3・6章は実施後の再集計である。
区切りごとの結果、受入条件の変更、制限は[完了レポート](./Mind8ディスパッチャC＃互換実装基本サンプル完走までの修正案v2完了レポート.md)を参照。

## 1. 結論と今回の範囲

**実施前の調査では `binarysearch.mco`、`bubblesort.mco`、`insertsort.mco` は完走しなかった。**
現在のソースをビルドし、現在のfixtureを別プロセスで実行して確認した。以前の完了レポートの「5件完走」を、今回のMCOに適用することはできない。

本書は調査結果と修正案であり、ディスパッチャの実装修正は行っていない。既存のMCO、ソース、参照出力も変更していない。

### 1.1 前回と今回のMCOの違い

調査開始時点で、5つのfixture MCOはすべてGit上で変更状態だった。比較元のコミットは `22746be`。
特に探索・整列3件は、コミット内の小さいMCOから、consoleライブラリを含む大きいMCOに変わっている。
シリアル番号が同じでも、実行される中間コードの集合は同じとは限らない。

| サンプル | HEAD内のサイズ | 今回のサイズ | 今回のSHA-256 |
| --- | ---: | ---: | --- |
| binarysearch | 908 | 15,546 | `759438467CE8DA3E2B560BA6D4FF77EDB027044452CB413621BE5DD542D9015E` |
| bubblesort | 1,569 | 15,874 | `9856635817CC025CA87272B7802E16114888F6E0736FB1342BAA9CCE04CB5D59` |
| insertsort | 1,110 | 15,604 | `A49EB538B5CA73A2A36D748B8367370D99EA80B67D592C82D8CA48EF4B1D0730` |

今回の5件はすべてバージョン8・ランタイム10・シリアル42であり、ロード時の互換性検査は通過する。
コンパイル履歴では、探索・整列3件は正規版の `C:\pmind\file\obj\console` を使用している。
詳細な停止原因は、シリアル不一致ではなく、consoleライブラリから呼ばれる未実装命令である。

### 1.2 再現結果

標準出力は生バイトで対応する[参照出力フォルダ](../tests/fixtures/reference/)と比較した。空白・改行・文字コードの正規化は行っていない。

| MCO | 終了コード | 実出力バイト | 期待バイト | 完全一致 | 最初の停止命令 | MCO内バイトオフセット |
| --- | ---: | ---: | ---: | --- | --- | --- |
| [hello](../tests/fixtures/hello.mco) | 0 | 14 | 14 | 一致 | なし | — |
| [variables](../tests/fixtures/variables.mco) | 0 | 15 | 15 | 一致 | なし | — |
| [binarysearch](../tests/fixtures/binarysearch.mco) | -532462766 | 30 | 302 | 不一致 | `0x0171 chgDefaultStruct` | `0x0594` |
| [bubblesort](../tests/fixtures/bubblesort.mco) | -532462766 | 12 | 9,871 | 不一致 | `0x0161 zzCtoS` | `0x16BC` |
| [insertsort](../tests/fixtures/insertsort.mco) | -532462766 | 0 | 3,191 | 不一致 | `0x00F1 kaisu` | `0x2FEC` |

3件とも `NotSupportedException` による異常終了。終了コードの符号なし表現は `3762504530`、16進数は `0xE0434352`。
これらの命令を無視したり、スタック操作だけで代替したりしても、正しい完走にはならない。

検証コマンド:

```powershell
Set-Location C:\developments\vscode\mind8dispatch
dotnet build .\tests\McodeLoaderTests.csproj --no-restore --nologo -m:1
dotnet .\tests\bin\Debug\net8.0\McodeLoaderTests.dll
dotnet .\bin\Debug\net8.0\mind8dispatch.dll .\tests\fixtures\binarysearch.mco
dotnet .\bin\Debug\net8.0\mind8dispatch.dll .\tests\fixtures\bubblesort.mco
dotnet .\bin\Debug\net8.0\mind8dispatch.dll .\tests\fixtures\insertsort.mco
```

- ビルド: 警告0、エラー0。
- 回帰テスト: **45/48件成功**。失敗3件は上記の探索・整列fixtureの完全一致テスト。
- 各fixtureには30秒、テスト全体には90秒の実行上限を設けて調査した。今回はタイムアウトではない。
- [既存のfixtureテスト](../tests/Program.cs#L491)は、終了コード・生バイト出力・標準エラーを検査している。このテストの期待値を緩めずに修正する。

## 2. 全console命令の母集団と分類基準

### 2.1 母集団

- 正規版の [c_words.tbl](C:/pmind/kernel/c_words.tbl) にある **`0x0010`〜`0x02F6`、743命令番号**を対象とする。
- console版は[kerbody.c](C:/pmind/kernel/kerbody.c#L324)からこの表を選択する。GUI用・CGI用・コンパイラ専用の別表は混ぜない。
- 「console」は通常コンソール用ランタイムの意味であり、標準入出力だけでなく、同表にあるファイル、ソケット、プロセス、DLL/API、同期、レジストリ等も含む。
- 同じC関数を指す別名も、命令番号ごとに1件と数える。末尾の予約ダミー7件も除外しない。
- `0x0000`〜`0x000F` は表の前の予約領域であり、743件には含めない。`0x8000`以上のMind単語呼出しはMCO固有のLOC番号で、固定のconsoleプリミティブ表とは別である。
- [C#の登録表](../CsFunctions.cs)と実際の関数本体を照合した。コメントアウトされた登録や、登録だけ存在するスタブを実装済みに数えない。

### 2.2 3分類の定義

| 分類 | 本書での判定 |
| --- | --- |
| 未実装 | C#登録なし、`UnDefined`を投げるだけ、または主要な処理本体が欠落している。 |
| 実装済未検証 | 実行する処理はあるが、今回成功した命令単位の期待値照合テストで確認していない。既知の実装不整合も、この区分で注記する。 |
| 実装済検証済 | 今回成功した命令単位のテストで、結果・スタック・メモリ・終了コード等の期待値が確認できる。 |

**「実装済検証済」は、そのテストケースに限る。全入力・全分岐・全OSでの完全互換を意味しない。**
正常終了したhello/variablesで命令を通過しただけの場合、単独の期待値検証とは区別する。
失敗した3件の停止前トレースへの出現や、MCO内の静的な命令出現も、検証済みへ昇格させる理由にしない。

## 3. 集計（実施後）

| 分類 | 件数 | 全743件に対する割合 |
| --- | ---: | ---: |
| 未実装 | **453** | 60.97% |
| 実装済未検証 | **137** | 18.44% |
| 実装済検証済 | **153** | 20.59% |
| 合計 | **743** | 100.00% |

登録状況の別集計: **登録なし453件 + 登録あり290件 = 743件**。
登録あり290件の内訳: **実装済未検証137件 + 実装済検証済153件**。15件あった登録済み例外スタブはすべて実処理へ置き換えた。
未実装は **登録なし453件**。登録数だけを実装数として扱わず、実処理の有無で分類する。

検証済み153件は成功した命令単位の期待値照合で重複排除した件数であり、テスト件数73件とは異なる。最新の回帰テストでは全73件と5つのfixtureが成功した。
5サンプルの完走だけを理由に、通過命令すべてを検証済みへ昇格させていない。
割合の表示は小数第2位に丸めている。

| 命令番号帯 | 未実装 | 実装済未検証 | 実装済検証済 | 合計 |
| --- | ---: | ---: | ---: | ---: |
| 0x0010〜0x00FF | 12 | 112 | 116 | 240 |
| 0x0100〜0x01FF | 204 | 18 | 34 | 256 |
| 0x0200〜0x02F6 | 237 | 7 | 3 | 247 |

### 3.1 区切り1で実装した既存スタブ15件

`004B`, `0059`, `0060`, `0061`, `0062`, `0073`, `0083`, `0084`, `0089`, `008E`, `0090`, `0091`, `0092`, `0093`, `0094`。

15件すべての実処理を追加した。うち`004B`, `0059`, `0060`〜`0062`, `0073`, `0089`, `008E`〜`0094`の13件は命令単位の期待値照合に成功した。`0083`と`0084`は64ビット値のスタック往復を確認したが、正規C版の診断出力との完全照合が未完了のため実装済未検証に留める。

実施前に主要処理欠落だった`008F`と`0164`〜`0167`の5件は、この区切りより前に本体を実装し、個別の期待値照合も成功している。

### 3.2 区切り3の数値・表示系で今回実装した63件

`00A6`〜`00BD`の24件、ならびに `00C0`、`00C2`、`00C3`、`00C5`〜`00C8`、`00CA`〜`00CD`、`00CE`〜`00EB`を実装した。浮動小数点定数の2ワード格納、`c_atof` の数値prefix/end pointer、整数・小数部の移動とpack/unpack、タグ付き領域のstack/data情報、Shift-JIS文字・文字列およびC形式の数値表示、数値から文字列への変換、memory/stack dump、変数・局所・定数配列と構造体参照を対象にした。`00D7`と`00DD`はそれぞれC表上の`zzPointQarray`、`zzPointQarrayLocal`別名として既存opcodeと同じ処理へ登録した。

| opcode群 | 処理と個別確認 |
| --- | --- |
| `00A6` | `c_atof`: prefixを解析しdoubleを返す。変換後ポインターを書き込み。 |
| `00A7`〜`00B5` | 定数15件。IEEE double値と2ワード配置を確認。 |
| `00B6`〜`00B8` | データstack／return stack／data areaのアドレス・サイズを確認。 |
| `00B9`〜`00BD` | integer/fraction wordの消去、移動、pack/unpackを確認。 |
| `00C0`、`00C2`、`00C3`、`00C5`〜`00C8` | `wordStore11`、引用符、Shift-JIS文字、16進表示を確認。 |
| `00CB`、`00CC`、`00CE` | 固定小数、指数、quad表示の書式を確認。 |
| `00CF`、`00D0`、`00D2` | byte/word dumpとreturn stack monitorの出力を確認。 |
| `00D3`、`00D4`、`00D7` | 1始まりのbyte/word配列添字、要素幅、quad配列別名を確認。 |
| `00CA`、`00CD` | 符号付きquadと固定／指数形式の数値文字列、幅・精度・バッファ上限を確認。 |
| `00D1` | quad単位のdata stack表示、異常なstack幅の診断、stack resetを確認。 |
| `00D8`〜`00DE` | 可変長配列と局所byte/word/double/quad配列の添字・要素幅を確認。 |
| `00DF`〜`00E3` | 定数byte/word/double/quad/string配列の参照を確認。 |
| `00E4`〜`00E7` | global/local構造体の多次元添字とレコード情報を確認。 |
| `00E8`〜`00EB` | 一時構造体の直接／多次元再参照とレコード長を確認。 |

これら63件は命令単位の期待値照合に成功したため、すべて実装済検証済へ移動した。区切り3は継続中で、残る数値／表示系と、`0x00F2`〜`0x00FC`・`0x00FF`を含む後続opcodeを調査する。回帰テストは73/73成功し、5つのfixtureもすべて完走した。

## 4. 3サンプルに対する不足命令の洗い出し

### 4.1 実行で停止を確認した3件

| 命令 | 正規C版の参照 | 必要な修正 |
| --- | --- | --- |
| `0x0171 chgDefaultStruct` | [2ker0.c](C:/pmind/kernel/2ker0.c#L737) | 旧実体情報と新実体情報を受け取り、旧アドレスがNULLの場合だけ新情報に置換する。常に上書きする実装にしない。 |
| `0x0161 zzCtoS` | [2ker0.c](C:/pmind/kernel/2ker0.c#L165) | 元の文字コード値を残し、そのスタック上の実体を指す1/2バイトの文字列情報を積む。Shift-JISのバイト順と参照寿命を保つ。 |
| `0x00F1 kaisu` | [bunki.c](C:/pmind/kernel/bunki.c#L225) | ループの現在回数をリターンスタックから読み、整数部と0の小数部を積む。局所変数や戻りアドレスを壊さない。 |

### 4.2 後続の静的到達候補

MCOのLOCテーブル、単語呼出し、即値長、文字列リテラル長、相対分岐の両側を追跡した。
数値や文字列データを2バイトごとに走査して「命令」と誤認する方法は用いていない。
起点は実行開始位置 `0x70`。MCO内の `0x64` / `0x68` にある共通ジャンプ入口も追跡した。

| MCO | 静的に訪問した命令位置数（Mind呼出し込み） | プリミティブ種類数 | 未登録種類数 | 登録済み本体欠落 |
| --- | ---: | ---: | ---: | --- |
| binarysearch | 514 | 99 | 24 | `008F`, `0165` |
| bubblesort | 582 | 100 | 24 | `008F`, `0165` |
| insertsort | 532 | 99 | 24 | `008F`, `0165` |

これは条件を評価しない**保守的な到達候補**であり、通常入力で全件が実行されるという意味ではない。
特に共通終了・引数処理の分岐も含む。今回停止した3命令以外を「次に必ず停止する命令」とは断定しない。
動的な呼出しや実行時状態による経路を含む完全な必要十分集合の証明でもないため、実装後の再実行を必須とする。

3件共通の未登録候補24件:

| 命令 | 正規C関数 | 対応方針 |
| --- | --- | --- |
| `0x00D5` | `zzPointDarray` | 4バイト配列。要素数と1始まり添字を検査し、4バイト刻みでアクセスする。 |
| `0x00EF` | `zzLoop` | 回数更新、終了時の2ワード解放、相対ジャンプ。 |
| `0x00F0` | `zzBreakLoop` | 回数指定ループの状態を解放してループ外へ分岐。 |
| `0x00F1` | `kaisu` | 現在回数の参照。 |
| `0x00FE` | `zzMataha` | 真偽OR。両入力と結果の2ワード構成を維持。 |
| `0x010D` | `amariToSyou` | 符号付きの余りと商をC版と同じ順序で積む。ゼロ除算も確認。 |
| `0x0111` | `ijyou` | 符号付きの以上比較。 |
| `0x011A` | `fusu` | 整数の符号反転。 |
| `0x011C` | `fukusya` | 数値/文字列情報の2ワードを複写。 |
| `0x0121` | `isFu` | 負数判定。 |
| `0x0138` | `zzAndWimm` | 16ビット即値ANDとMコードポインタの進行。 |
| `0x0160` | `isNotKuretu` | 非空列判定。 |
| `0x0161` | `zzCtoS` | 文字コードからスタック参照の文字列情報を構成。 |
| `0x0169` | `zzClearJvar` | 実体情報経由で長さ・左余白をクリア。 |
| `0x016A` | `mojiretuJittaiWoSansyo` | ヘッダーの長さ・左余白を反映して文字列情報を生成。 |
| `0x0171` | `chgDefaultStruct` | 省略時だけ既定実体へ置換。 |
| `0x0173` | `tuikaYoyu` | 容量・現在長・左余白から追加可能長を計算。4バイト即値を消費。 |
| `0x0177` | `itiMojiTuika` | 実体への1文字追加。4バイト容量即値、文字境界、ヘッダー更新。 |
| `0x017A` | `itiMojiHigaTuikaForJinfo` | 実体情報経由の1文字左側追加。 |
| `0x017B` | `tuika` | 文字列追加。4バイト容量即値と格納上限を処理。 |
| `0x0182` | `migiYoseForJinfo` | 実体情報経由の右寄せ。長さと左余白を一貫させる。 |
| `0x01A5` | `itiMojiMigiKiridasi` | 情報変数から末尾1文字を数値で返し、元の長さを縮める。 |
| `0x01E9` | `konoBaitoSu` | 元の文字列情報を残したままバイト数を積む。 |
| `0x0263` | `get_Arg_n` | 実引数取得。引数個数、範囲、エンコーディング、寿命を確認。 |

登録済みの追加修正対象は、上表とは別に `0x008F` と `0x0165`。したがって、この静的経路内の未実装候補は計26種類である。
全console命令に対する未実装件数とは区別する。

## 5. 完走に向けた修正手順

### 区切りA: 検証対象の固定

1. 本書のSHA-256と一致するMCOを入力として固定する。MCOを差し替えたらサイズ・ハッシュ・コンパイル条件を記録し直す。
2. シリアル42・ランタイム10を維持する。ヘッダー書換え、簡略スタートアップへの差替え、参照テキストの変更で成功扱いにしない。
3. 不足命令の診断には命令番号とMCOオフセットを残す。トレースは標準エラー、アプリ出力は標準出力に分離する。

### 区切りB: メモリ参照と文字列の共通基盤

今回の本質的な追加要件は、Mコード領域とデータ領域だけでなく、**スタック上の文字列実体を指す情報**である。
[ReadStringBytes](../Dispatcher.cs)は現在Mコード/データの2領域しか扱わず、[AccessPointer](../AccessPointer.cs)の局所変数アクセス機能だけでは、データスタック上の文字への文字列参照を表現できない。

1. 既存のポインタ・スタック補助機能を再利用し、Mコード/データ/データスタック/リターンスタックを区別して読み書きできる論理アドレス解決を設ける。
2. アドレス表現の変更時は、既存の `DataAddressBit` と衝突しないこと、長さ加算が領域識別を壊さないこと、スタック移動後も対象位置を保持することを確認する。
3. `0x0161` と `0x0043/0x0044`、`0x008F`、文字列表示・検索・格納を同じ解決規約に揃える。局所オフセットをMコードの絶対位置として扱わない。
4. `0x0164`〜`0x0167` の共通処理を完成させる。長さ・左余白ヘッダーと本体を更新し、格納上限、重複領域、Shift-JIS文字境界を扱う。
5. `0x0169/0x016A/0x0171` と文字列実体操作を追加する。

最低限のテスト: ASCII/Shift-JISの1文字化、元数値の保持、スタック移動・入れ子呼出し後の参照、既定実体の採用/非採用、空文字列、容量境界、重複コピー、局所実体への書込み。

### 区切りC: 回数指定ループと配列

1. [ZzDo](../CsFunctionsBunki.cs)は初回回数に0を積んでいるが、正規版[zzDO](C:/pmind/kernel/bunki.c#L148)は1を積む。`0x00F1`追加と同時に修正する。
2. `0x00EF/0x00F0/0x00F1` を一体で実装し、回数0・1・複数、終了時のスタック復旧、内側からの脱出を確認する。単語呼出しによる戻りアドレスとの配置もC版に合わせる。
3. `0x00D5` を追加する。[既存の配列テスト](../tests/Program.cs#L292)は `0x00D6` の8バイト配列であり、今回の4バイト配列の検証の代わりにはならない。
4. 添字0・1・末尾・末尾+1で、読出し、書込み、隣接要素の保持を確認する。

### 区切りD: 数値から文字列への変換と桁揃え

正規console経由の数値表示は、`0x00C9`を直接呼ぶだけではない。
商と余り、文字の追加、右寄せ、文字列実体、文字表示を経由するため、数値表示命令の登録だけでは不足する。

1. `0x010D/0x0111/0x011A/0x011C/0x0121/0x0138/0x00FE` を正規C版のスタック効果に合わせて追加する。
2. `0x017A/0x0182/0x01A5` を含む変換用文字列操作を完成させる。
3. 「二桁で数値表示」は通常の `ToString()` で置き換えない。桁幅、埋め文字、全角空白、改行位置を参照出力と一致させる。
4. 0、1桁、2桁、負数、空の出力先、容量上限を確認し、数値/文字列の両スタック状態を検査する。

### 区切りE: 共通処理の不整合と環境経路

| 対象 | 現状の注意点 | 対応 |
| --- | --- | --- |
| `0x0027`と条件付きEXIT | 汎用EXITが戻り先を`PopUs()`で16ビット化している。 | 32ビットのMCO戻り先とフレーム解放をC版に合わせてテストする。 |
| `0x0043/0x0044` | 局所位置へのアドレスではなく、生のオフセットを積んでいる。 | 区切りBの共通参照方式に合わせる。 |
| `0x0108` | 即値減算が整数部でなく先頭ワードへ書き込んでいる。 | 算術の整数部と小数部を別々に検証する。今回の静的候補外だが全命令一覧では未検証として残す。 |
| `0x0263/0x0264` | 引数取得が未登録、引数個数は固定値2。 | 通常実行と引数ありを正規版と照合し、必要な呼出し元も変更する。 |
| `0x026D` | ファイルハンドルを読み捨て、常に`Console.Write`する。 | 今回のstdout経路と一般ハンドル対応を区別する。ファイル互換まで検証済みとしない。 |
| `0x02CD/0x02CE` | 状態切替えのみ。関連するOS操作は多くが未実装。 | 状態切替えとリソース操作の実装・検証を混同しない。 |

引数・終了処理等にだけ属する静的候補は、まず通常実行時に必要かを再トレースする。
必要な命令は実装し、未対応経路は明確に失敗させる。既定値を返して隠蔽しない。
全console743命令の完成と、この3サンプルの完走は別の到達点として管理する。

### 区切りF: 最終受入れ

- 変更のたびに停止命令・停止位置・出力差分を記録し、次の不足を調べる。最初の3命令を追加しただけで完了としない。
- 既存の48テストと、今回追加する命令・境界条件のテストを実行する。fixture比較の失敗をスキップしない。
- 当初は5件ともタイムアウトなし、終了コード0、参照出力と生バイト完全一致を条件とした。実施中のユーザー確認により正規版の改行を優先し、探索・整列3件のみ参照LFをCRLFに展開した期待値と比較することへ変更した。MCO・参照ファイルは変更しない。
- 不正な標準エラーがないことを確認する。命令トレースを通常出力へ混ぜない。
- 正規版との命令単位比較には入力スタック、出力スタック、リターンスタック、変更メモリ、次のMCO位置を用いる。
- 成功時のMCOハッシュ、ビルド条件、テスト件数を完了レポートへ記録し、今回の全件表を更新する。

## 6. 全console中間コード一覧

以下は命令番号順の全743件。C#のリンクは実装本体、検証のリンクは今回成功した期待値照合テストを指す。
未登録の命令の正式な番号対応は[正規版の表](C:/pmind/kernel/c_words.tbl)を参照する。
別名や予約ダミーにも1行ずつ割り当て、範囲の省略は行わない。

### 0x0010〜0x00FF

| 命令番号 | 正規C関数名 | C#実装 | 分類 | 根拠・注意点 |
| --- | --- | --- | --- | --- |
| `0x0010` | `zzEndExecute` | [ZzEndExecute](../CsFunctionsCompword.cs#L11) | 実装済未検証 | 戻り先を16ビットへ切捨て |
| `0x0011` | `jikkou` | [Jikkou](../CsFunctionsCompword.cs#L20) | 実装済未検証 | 動的Mコードとスタック領域の接続が未検証 |
| `0x0012` | `musyori` | [Musyori](../CsFunctionsCompword.cs#L36) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0013` | `emgcyExit` | [EmgcyExit](../CsFunctionsCompword.cs#L40) | 実装済検証済 | [ディスパッチ終了](../tests/Program.cs#L67) |
| `0x0014` | `processOwari` | [ProcessOwari](../CsFunctionsCompword.cs#L45) | 実装済検証済 | [終了コード](../tests/Program.cs#L68) |
| `0x0015` | `stackKensa0` | [StackKensa0](../CsFunctionsCompword.cs#L58) | 実装済検証済 | [リセット後の再開](../tests/Program.cs#L63) |
| `0x0016` | `stackReset` | [StackReset](../CsFunctionsCompword.cs#L81) | 実装済検証済 | [リセット後の再開](../tests/Program.cs#L63) |
| `0x0017` | `finish_dispatcher` | [FinishDispatcher](../CsFunctionsCompword.cs#L90) | 実装済検証済 | [ディスパッチ終了](../tests/Program.cs#L67) |
| `0x0018` | `c_stack_data_no` | [C_stack_data_no](../CsFunctionsCompword.cs#L95) | 実装済検証済 | [ディスパッチ終了](../tests/Program.cs#L67) |
| `0x0019` | `zzReserve1L` | [ZzReserve1L](../CsFunctionsCompword.cs#L102) | 実装済未検証 | 命令単位の期待値照合なし。variables通過のみ |
| `0x001A` | `zzReserve2L` | [ZzReserve2L](../CsFunctionsCompword.cs#L107) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x001B` | `zzReserve3L` | [ZzReserve3L](../CsFunctionsCompword.cs#L112) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x001C` | `zzReserve4L` | [ZzReserve4L](../CsFunctionsCompword.cs#L117) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x001D` | `zzReserve5L` | [ZzReserve5L](../CsFunctionsCompword.cs#L122) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x001E` | `zzReserve6L` | [ZzReserve6L](../CsFunctionsCompword.cs#L127) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x001F` | `zzReserve` | [ZzReserve](../CsFunctionsCompword.cs#L132) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0020` | `zzExit1L` | [ZzExit1L](../CsFunctionsCompword.cs#L139) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0021` | `zzExit2L` | [ZzExit2L](../CsFunctionsCompword.cs#L146) | 実装済未検証 | 命令単位の期待値照合なし。variables通過のみ |
| `0x0022` | `zzExit3L` | [ZzExit3L](../CsFunctionsCompword.cs#L155) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0023` | `zzExit4L` | [ZzExit4L](../CsFunctionsCompword.cs#L164) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0024` | `zzExit5L` | [ZzExit5L](../CsFunctionsCompword.cs#L173) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0025` | `zzExit6L` | [ZzExit6L](../CsFunctionsCompword.cs#L182) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0026` | `zzExit7L` | [ZzExit7L](../CsFunctionsCompword.cs#L191) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0027` | `zzExit` | [ZzExit](../CsFunctionsCompword.cs#L200) | 実装済検証済 | [32ビットEXIT・フレーム解放](../tests/Program.cs#L29) |
| `0x0028` | `zzIfExit` | [ZzIfExit](../CsFunctionsCompword.cs#L210) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0029` | `zzIf0Exit` | [ZzIf0Exit](../CsFunctionsCompword.cs#L228) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x002A` | `zzIf1Exit` | [ZzIf1Exit](../CsFunctionsCompword.cs#L248) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x002B` | `zzNotIfExit` | [ZzNotIfExit](../CsFunctionsCompword.cs#L271) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x002C` | `zzNotIf0Exit` | [ZzNotIf0Exit](../CsFunctionsCompword.cs#L288) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x002D` | `zzNotIf1Exit` | [ZzNotIf1Exit](../CsFunctionsCompword.cs#L308) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x002E` | `zzJmpRelative` | [ZzJmpRelative](../CsFunctionsCompword.cs#L330) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x002F` | `zzJmpIndexed` | [ZzJmpIndexed](../CsFunctionsCompword.cs#L335) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0030` | `zzPointNull` | [ZzPointNull](../CsFunctionsCompword.cs#L342) | 実装済未検証 | NULLポインタ設定でなくメモリへ0を書込み |
| `0x0031` | `zzPushNull` | [ZzPushNull](../CsFunctionsCompword.cs#L347) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0032` | `zzPushNull` | [ZzPushNull](../CsFunctionsCompword.cs#L347) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0033` | `zzPushPointer` | [ZzPushPointer](../CsFunctionsCompword.cs#L357) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0034` | `zzPushPointerPlusSize` | [ZzPushPointerPlusSize](../CsFunctionsCompword.cs#L363) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0035` | `addressWoEru` | [AddressWoEru](../CsFunctionsCompword.cs#L374) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0036` | `zzPointVarAddr` | [ZzPointVarAddr](../CsFunctionsCompword.cs#L399) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0037` | `zzPointCodeAddr` | [ZzPointCodeAddr](../CsFunctionsCompword.cs#L405) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0038` | `zzPushVarAddrPlusSize` | [ZzPushVarAddrPlusSize](../CsFunctionsCompword.cs#L412) | 実装済検証済 | [データ範囲クリア](../tests/Program.cs#L53) |
| `0x0039` | `zzPushMcodeAddrPlusSize` | [ZzPushMcodeAddrPlusSize](../CsFunctionsCompword.cs#L423) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x003A` | `zzPointLocalAddr0W` | [ZzPointLocalAddr0W](../CsFunctionsCompword.cs#L432) | 実装済検証済 | [局所文字列切出し](../tests/Program.cs#L40) |
| `0x003B` | `zzPointLocalAddr1W` | [ZzPointLocalAddr1W](../CsFunctionsCompword.cs#L437) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x003C` | `zzPointLocalAddr2W` | [ZzPointLocalAddr2W](../CsFunctionsCompword.cs#L442) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x003D` | `zzPointLocalAddr3W` | [ZzPointLocalAddr3W](../CsFunctionsCompword.cs#L447) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x003E` | `zzPointLocalAddr4W` | [ZzPointLocalAddr4W](../CsFunctionsCompword.cs#L452) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x003F` | `zzPointLocalAddr5W` | [ZzPointLocalAddr5W](../CsFunctionsCompword.cs#L457) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0040` | `zzPointLocalAddr6W` | [ZzPointLocalAddr6W](../CsFunctionsCompword.cs#L462) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0041` | `zzPointLocalAddr7W` | [ZzPointLocalAddr7W](../CsFunctionsCompword.cs#L467) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0042` | `zzPointLocalAddr` | [ZzPointLocalAddr](../CsFunctionsCompword.cs#L472) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0043` | `zzPushLocalAddrPlusSize` | [ZzPushLocalAddrPlusSize](../CsFunctionsCompword.cs#L478) | 実装済検証済 | [局所文字列の絶対アドレス](../tests/Program.cs#L43) |
| `0x0044` | `zzPushLocalAddrPlusSize` | [ZzPushLocalAddrPlusSize](../CsFunctionsCompword.cs#L478) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0045` | `zzPushBvar` | [ZzPushBvar](../CsFunctionsCompword.cs#L489) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0046` | `zzPushWvar` | [ZzPushWvar](../CsFunctionsCompword.cs#L496) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0047` | `zzPushDvar` | [ZzPushDvar](../CsFunctionsCompword.cs#L503) | 実装済検証済 | [4バイト配列・境界・読書き](../tests/Program.cs#L47) |
| `0x0048` | `zzPushLvar` | [ZzPushLvar](../CsFunctionsCompword.cs#L510) | 実装済検証済 | [配列境界・読出し](../tests/Program.cs#L60) |
| `0x0049` | `zzPushQvar` | [ZzPushQvar](../CsFunctionsCompword.cs#L518) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x004A` | `zzPushSvar` | [ZzPushSvar](../CsFunctionsCompword.cs#L526) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x004B` | `zzPushJvar` | [ZzPushJvar](../CsFunctionsCompword.cs#L536) | 実装済検証済 | [Jvar参照](../tests/Program.cs#L955) |
| `0x004C` | `zzReadBvar` | [ZzReadBvar](../CsFunctionsCompword.cs#L546) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x004D` | `zzReadWvar` | [ZzReadWvar](../CsFunctionsCompword.cs#L556) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x004E` | `zzReadDvar` | [ZzReadDvar](../CsFunctionsCompword.cs#L566) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x004F` | `zzReadLvar` | [ZzReadLvar](../CsFunctionsCompword.cs#L576) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0050` | `zzReadQvar` | [ZzReadQvar](../CsFunctionsCompword.cs#L587) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0051` | `zzReadSvar` | [ZzReadSvar](../CsFunctionsCompword.cs#L598) | 実装済検証済 | [文字列情報の保存](../tests/Program.cs#L37) |
| `0x0052` | `zzReadJvar` | [ZzReadJvar](../CsFunctionsCompword.cs#L609) | 実装済検証済 | [実体参照・文字境界](../tests/Program.cs#L38) |
| `0x0053` | `zzReadBvarLocal` | [ZzReadBvarLocal](../CsFunctionsCompword.cs#L631) | 実装済検証済 | [局所スカラー](../tests/Program.cs#L25) |
| `0x0054` | `zzReadWvarLocal` | [ZzReadWvarLocal](../CsFunctionsCompword.cs#L642) | 実装済検証済 | [局所スカラー](../tests/Program.cs#L25) |
| `0x0055` | `zzReadDvarLocal` | [ZzReadDvarLocal](../CsFunctionsCompword.cs#L654) | 実装済検証済 | [局所スカラー](../tests/Program.cs#L25) |
| `0x0056` | `zzReadLvarLocal` | [ZzReadLvarLocal](../CsFunctionsCompword.cs#L667) | 実装済検証済 | [局所2ワード](../tests/Program.cs#L26) |
| `0x0057` | `zzReadQvarLocal` | [ZzReadQvarLocal](../CsFunctionsCompword.cs#L677) | 実装済検証済 | [局所2ワード](../tests/Program.cs#L26) |
| `0x0058` | `zzReadSvarLocal` | [ZzReadSvarLocal](../CsFunctionsCompword.cs#L687) | 実装済検証済 | [局所2ワード](../tests/Program.cs#L26) |
| `0x0059` | `zzReadJvarLocal` | [ZzReadJvarLocal](../CsFunctionsCompword.cs#L697) | 実装済検証済 | [Jvar参照](../tests/Program.cs#L955) |
| `0x005A` | `zzPopBvar` | [ZzPopBvar](../CsFunctionsCompword.cs#L711) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x005B` | `zzPopWvar` | [ZzPopWvar](../CsFunctionsCompword.cs#L717) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x005C` | `zzPopDvar` | [ZzPopDvar](../CsFunctionsCompword.cs#L723) | 実装済検証済 | [4バイト配列・境界・読書き](../tests/Program.cs#L47) |
| `0x005D` | `zzPopLvar` | [ZzPopLvar](../CsFunctionsCompword.cs#L729) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x005E` | `zzPopQvar` | [ZzPopQvar](../CsFunctionsCompword.cs#L736) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x005F` | `zzPopSvar` | [ZzPopSvar](../CsFunctionsCompword.cs#L742) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0060` | `zzPopMemory` | [ZzPopMemory](../CsFunctionsCompword.cs#L752) | 実装済検証済 | [重複領域・切詰め](../tests/Program.cs#L986) |
| `0x0061` | `zzPopMemory` | [ZzPopMemory](../CsFunctionsCompword.cs#L752) | 実装済検証済 | [重複領域・切詰め](../tests/Program.cs#L986) |
| `0x0062` | `zzPopMemoryWithResult` | [ZzPopMemoryWithResult](../CsFunctionsCompword.cs#L762) | 実装済検証済 | [残り文字列情報](../tests/Program.cs#L986) |
| `0x0063` | `zzWriteBvar` | [ZzWriteBvar](../CsFunctionsCompword.cs#L759) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0064` | `zzWriteWvar` | [ZzWriteWvar](../CsFunctionsCompword.cs#L774) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0065` | `zzWriteDvar` | [ZzWriteDvar](../CsFunctionsCompword.cs#L789) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0066` | `zzWriteLvar` | [ZzWriteLvar](../CsFunctionsCompword.cs#L800) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0067` | `zzWriteQvar` | [ZzWriteQvar](../CsFunctionsCompword.cs#L812) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0068` | `zzWriteSvar` | [ZzWriteSvar](../CsFunctionsCompword.cs#L823) | 実装済検証済 | [文字列情報の保存](../tests/Program.cs#L37) |
| `0x0069` | `zzWriteBvarLocal` | [ZzWriteBvarLocal](../CsFunctionsCompword.cs#L839) | 実装済検証済 | [局所スカラー](../tests/Program.cs#L25) |
| `0x006A` | `zzWriteWvarLocal` | [ZzWriteWvarLocal](../CsFunctionsCompword.cs#L855) | 実装済検証済 | [局所スカラー](../tests/Program.cs#L25) |
| `0x006B` | `zzWriteDvarLocal` | [ZzWriteDvarLocal](../CsFunctionsCompword.cs#L871) | 実装済検証済 | [局所スカラー](../tests/Program.cs#L25) |
| `0x006C` | `zzWriteLvarLocal` | [ZzWriteLvarLocal](../CsFunctionsCompword.cs#L888) | 実装済検証済 | [局所2ワード](../tests/Program.cs#L26) |
| `0x006D` | `zzWriteQvarLocal` | [ZzWriteQvarLocal](../CsFunctionsCompword.cs#L899) | 実装済検証済 | [局所2ワード](../tests/Program.cs#L26) |
| `0x006E` | `zzWriteSvarLocal` | [ZzWriteSvarLocal](../CsFunctionsCompword.cs#L909) | 実装済検証済 | [局所2ワード](../tests/Program.cs#L26) |
| `0x006F` | `zzClearBvar` | [ZzClearBvar](../CsFunctionsCompword.cs#L924) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0070` | `zzClearWvar` | [ZzClearWvar](../CsFunctionsCompword.cs#L929) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0071` | `zzClearDvar` | [ZzClearDvar](../CsFunctionsCompword.cs#L934) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0072` | `zzClearLvar` | [ZzClearLvar](../CsFunctionsCompword.cs#L939) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0073` | `zzClearFvar` | [ZzClearFvar](../CsFunctionsCompword.cs#L964) | 実装済検証済 | [浮動小数点変数](../tests/Program.cs#L1059) |
| `0x0074` | `zzClearSvar` | [ZzClearSvar](../CsFunctionsCompword.cs#L949) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0075` | `zzClearMemory` | [ZzClearMemory](../CsFunctionsCompword.cs#L955) | 実装済検証済 | [データ範囲クリア](../tests/Program.cs#L53) |
| `0x0076` | `zzClearMemory` | [ZzClearMemory](../CsFunctionsCompword.cs#L955) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0077` | `zzIncBvar` | [ZzIncBvar](../CsFunctionsCompword.cs#L970) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0078` | `zzIncWvar` | [ZzIncWvar](../CsFunctionsCompword.cs#L975) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0079` | `zzIncDvar` | [ZzIncDvar](../CsFunctionsCompword.cs#L980) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x007A` | `zzIncLvar` | [ZzIncLvar](../CsFunctionsCompword.cs#L985) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x007B` | `zzDecBvar` | [ZzDecBvar](../CsFunctionsCompword.cs#L992) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x007C` | `zzDecWvar` | [ZzDecWvar](../CsFunctionsCompword.cs#L997) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x007D` | `zzDecDvar` | [ZzDecDvar](../CsFunctionsCompword.cs#L1002) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x007E` | `zzDecLvar` | [ZzDecLvar](../CsFunctionsCompword.cs#L1007) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x007F` | `zzSetBvar` | [ZzSetBvar](../CsFunctionsCompword.cs#L1014) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0080` | `zzSetWvar` | [ZzSetWvar](../CsFunctionsCompword.cs#L1019) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0081` | `zzSetDvar` | [ZzSetDvar](../CsFunctionsCompword.cs#L1025) | 実装済未検証 | 命令単位の期待値照合なし。variables通過のみ |
| `0x0082` | `zzSetLvar` | [ZzSetLvar](../CsFunctionsCompword.cs#L1030) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0083` | `c_test_push_quad` | [c_test_push_quad](../CsFunctionsCompword.cs#L1070) | 実装済未検証 | スタック往復を実装。C版診断出力は未照合 |
| `0x0084` | `c_test_push_float` | [c_test_push_float](../CsFunctionsCompword.cs#L1079) | 実装済未検証 | スタック往復を実装。C版診断出力は未照合 |
| `0x0085` | `zzAddBvar` | [ZzAddBvar](../CsFunctionsCompword.cs#L1044) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0086` | `zzAddWvar` | [ZzAddWvar](../CsFunctionsCompword.cs#L1051) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0087` | `zzAddDvar` | [ZzAddDvar](../CsFunctionsCompword.cs#L1058) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0088` | `zzAddLvar` | [ZzAddLvar](../CsFunctionsCompword.cs#L1065) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0089` | `zzAddFvar` | [ZzAddFvar](../CsFunctionsCompword.cs#L1126) | 実装済検証済 | [浮動小数点変数](../tests/Program.cs#L1059) |
| `0x008A` | `zzSubBvar` | [ZzSubBvar](../CsFunctionsCompword.cs#L1089) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x008B` | `zzSubWvar` | [ZzSubWvar](../CsFunctionsCompword.cs#L1096) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x008C` | `zzSubDvar` | [ZzSubDvar](../CsFunctionsCompword.cs#L1103) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x008D` | `zzSubLvar` | [ZzSubLvar](../CsFunctionsCompword.cs#L1110) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x008E` | `zzSubFvar` | [ZzSubFvar](../CsFunctionsCompword.cs#L1171) | 実装済検証済 | [浮動小数点変数](../tests/Program.cs#L1059) |
| `0x008F` | `byteLoad` | [ByteLoad](../CsFunctionsCompword.cs#L1133) | 実装済検証済 | [文字コード・スタック実体参照](../tests/Program.cs#L42) |
| `0x0090` | `wordLoad` | [WordLoad](../CsFunctionsCompword.cs#L1189) | 実装済検証済 | [間接ロード・ストア](../tests/Program.cs#L1024) |
| `0x0091` | `doubleLoad` | [DoubleLoad](../CsFunctionsCompword.cs#L1197) | 実装済検証済 | [間接ロード・ストア](../tests/Program.cs#L1024) |
| `0x0092` | `byteStore` | [ByteStore](../CsFunctionsCompword.cs#L1206) | 実装済検証済 | [間接ロード・ストア](../tests/Program.cs#L1024) |
| `0x0093` | `wordStore` | [WordStore](../CsFunctionsCompword.cs#L1215) | 実装済検証済 | [間接ロード・ストア](../tests/Program.cs#L1024) |
| `0x0094` | `doubleStore` | [DoubleStore](../CsFunctionsCompword.cs#L1226) | 実装済検証済 | [間接ロード・ストア](../tests/Program.cs#L1024) |
| `0x0095` | `zz0` | [Zz0](../CsFunctionsCompword.cs#L1178) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0096` | `zz1` | [Zz1](../CsFunctionsCompword.cs#L1187) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0097` | `zz2` | [Zz2](../CsFunctionsCompword.cs#L1193) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0098` | `zz3` | [Zz3](../CsFunctionsCompword.cs#L1199) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0099` | `zz4` | [Zz4](../CsFunctionsCompword.cs#L1205) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x009A` | `zz5` | [Zz5](../CsFunctionsCompword.cs#L1211) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x009B` | `zz6` | [Zz6](../CsFunctionsCompword.cs#L1217) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x009C` | `zz7` | [Zz7](../CsFunctionsCompword.cs#L1223) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x009D` | `zz8` | [Zz8](../CsFunctionsCompword.cs#L1229) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x009E` | `zz9` | [Zz9](../CsFunctionsCompword.cs#L1235) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x009F` | `zz10` | [Zz10](../CsFunctionsCompword.cs#L1241) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x00A0` | `zzWord` | [ZzWord](../CsFunctionsCompword.cs#L1247) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x00A1` | `zzMword` | [ZzMword](../CsFunctionsCompword.cs#L1254) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x00A2` | `zzDouble` | [ZzDouble](../CsFunctionsCompword.cs#L1261) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x00A3` | `zzQuad` | [ZzQuad](../CsFunctionsCompword.cs#L1268) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x00A4` | `zzSliteralOdd` | [ZzSliteralOdd](../CsFunctionsCompword.cs#L1281) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x00A5` | `zzSliteralEven` | [ZzSliteralEven](../CsFunctionsCompword.cs#L1292) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x00A6` | `c_atof` | [C_atof](../CsFunctionsNumeric.cs) | 実装済検証済 | [prefix/end pointer test](../tests/Program.cs#L62) |
| `0x00A7` | `c_M_ZERO` | [CMZero](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00A8` | `c_M_ONE` | [CMOne](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00A9` | `c_M_E` | [CME](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00AA` | `c_M_LOG2E` | [CMLog2E](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00AB` | `c_M_LOG10E` | [CMLog10E](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00AC` | `c_M_LN2` | [CMLn2](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00AD` | `c_M_LN10` | [CMLn10](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00AE` | `c_M_PI` | [CMPi](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00AF` | `c_M_PI_2` | [CMPi2](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00B0` | `c_M_PI_4` | [CMPi4](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00B1` | `c_M_1_PI` | [CM1Pi](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00B2` | `c_M_2_PI` | [CM2Pi](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00B3` | `c_M_2_SQRTPI` | [CM2SqrtPi](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00B4` | `c_M_SQRT2` | [CMSqrt2](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00B5` | `c_M_SQRT1_2` | [CM_SQRT1_2](../CsFunctionsNumeric.cs) | 実装済検証済 | [constant layout test](../tests/Program.cs#L60) |
| `0x00B6` | `stackPointer` | [StackPointer](../CsFunctionsNumeric.cs) | 実装済検証済 | [stack address test](../tests/Program.cs#L60) |
| `0x00B7` | `returnStackPointer` | [ReturnStackPointer](../CsFunctionsNumeric.cs) | 実装済検証済 | [stack address test](../tests/Program.cs#L60) |
| `0x00B8` | `zzDataArear` | [zzDataArear](../CsFunctionsNumeric.cs) | 実装済検証済 | [data area test](../tests/Program.cs#L60) |
| `0x00B9` | `seisubuWoEru` | [seisubuWoEru](../CsFunctionsNumeric.cs) | 実装済検証済 | [fraction word test](../tests/Program.cs#L60) |
| `0x00BA` | `syosubuWoEru` | [syosubuWoEru](../CsFunctionsNumeric.cs) | 実装済検証済 | [fraction word test](../tests/Program.cs#L60) |
| `0x00BB` | `seisubuWoClear` | [seisubuWoClear](../CsFunctionsNumeric.cs) | 実装済検証済 | [fraction word test](../tests/Program.cs#L60) |
| `0x00BC` | `seisuToSyosuWoPack` | [seisuToSyosuWoPack](../CsFunctionsNumeric.cs) | 実装済検証済 | [pack/unpack test](../tests/Program.cs#L62) |
| `0x00BD` | `seisuToSyosuWoUnpack` | [seisuToSyosuWoUnpack](../CsFunctionsNumeric.cs) | 実装済検証済 | [pack/unpack test](../tests/Program.cs#L62) |
| `0x00BE` | `mojiretuJyouhouBunkai` | [MojiretuJyouhouBunkai](../CsFunctions0ker.cs#L6) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x00BF` | `mojiretuJyouhouGousei` | [MojiretuJyouhouGousei](../CsFunctions0ker.cs#L20) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x00C0` | `wordStore11` | [WordStore11](../CsFunctionsNumeric.cs) | 実装済検証済 | [display/memory test](../tests/Program.cs#L61) |
| `0x00C1` | `zzHyouji` | [ZzHyouji](../CsFunctionsDispword.cs#L9) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x00C2` | `zzDoubleQuateHyouji` | [ZzDoubleQuateHyouji](../CsFunctionsDispword.cs) | 実装済検証済 | [display test](../tests/Program.cs#L61) |
| `0x00C3` | `zzIchimojiHyouji` | [ZzIchimojiHyouji](../CsFunctionsDispword.cs) | 実装済検証済 | [Shift-JIS display test](../tests/Program.cs#L61) |
| `0x00C4` | `zzKaigyou` | [ZzKaigyou](../CsFunctionsDispword.cs#L28) | 実装済検証済 | [数値・改行出力](../tests/Program.cs#L33) |
| `0x00C5` | `zzNibbleHyouji` | [ZzNibbleHyouji](../CsFunctionsDispword.cs) | 実装済検証済 | [hex display test](../tests/Program.cs#L61) |
| `0x00C6` | `zzByteHyouji` | [ZzByteHyouji](../CsFunctionsDispword.cs) | 実装済検証済 | [hex display test](../tests/Program.cs#L61) |
| `0x00C7` | `zzWordHyouji` | [ZzWordHyouji](../CsFunctionsDispword.cs) | 実装済検証済 | [hex display test](../tests/Program.cs#L61) |
| `0x00C8` | `zzDoubleHyouji` | [ZzDoubleHyouji](../CsFunctionsDispword.cs) | 実装済検証済 | [hex display test](../tests/Program.cs#L61) |
| `0x00C9` | `zzBaiseidoHyouji` | [ZzBaiseidoHyouji](../CsFunctionsDispword.cs#L30) | 実装済検証済 | [数値・改行出力](../tests/Program.cs#L33) |
| `0x00CA` | `c_conv_to_string_quad` | [C_conv_to_string_quad](../CsFunctionsNumeric.cs#L35) | 実装済検証済 | [数値文字列変換](../tests/Program.cs#L1567) |
| `0x00CB` | `zzSyosuHyouji` | [zzSyosuHyouji](../CsFunctionsDispword.cs) | 実装済検証済 | [float display test](../tests/Program.cs#L61) |
| `0x00CC` | `zzSisuHyouji` | [zzSisuHyouji](../CsFunctionsDispword.cs) | 実装済検証済 | [float display test](../tests/Program.cs#L61) |
| `0x00CD` | `c_conv_to_string_fraction` | [C_conv_to_string_fraction](../CsFunctionsNumeric.cs#L45) | 実装済検証済 | [数値文字列変換](../tests/Program.cs#L1567) |
| `0x00CE` | `zzQuadHyouji` | [zzQuadHyouji](../CsFunctionsDispword.cs) | 実装済検証済 | [quad display test](../tests/Program.cs#L61) |
| `0x00CF` | `zzDump` | [ZzDump](../CsFunctionsDispword.cs) | 実装済検証済 | [dump・return stack monitor](../tests/Program.cs#L1332) |
| `0x00D0` | `zzDumpWord` | [ZzDumpWord](../CsFunctionsDispword.cs) | 実装済検証済 | [dump・return stack monitor](../tests/Program.cs#L1332) |
| `0x00D1` | `zzStackDump` | [zzStackDump](../CsFunctionsDispword.cs#L119) | 実装済検証済 | [data stack dump](../tests/Program.cs#L1618) |
| `0x00D2` | `zzReturnStackMonitor` | [ZzReturnStackMonitor](../CsFunctionsDispword.cs) | 実装済検証済 | [dump・return stack monitor](../tests/Program.cs#L1332) |
| `0x00D3` | `zzPointBarray` | [ZzPointBarray](../CsFunctionsCompword.cs#L385) | 実装済検証済 | [byte/word配列参照](../tests/Program.cs#L477) |
| `0x00D4` | `zzPointWarray` | [ZzPointWarray](../CsFunctionsCompword.cs#L386) | 実装済検証済 | [byte/word配列参照](../tests/Program.cs#L477) |
| `0x00D5` | `zzPointDarray` | [ZzPointDarray](../CsFunctionsCompword.cs#L384) | 実装済検証済 | [4バイト配列・境界・読書き](../tests/Program.cs#L47) |
| `0x00D6` | `zzPointQarray` | [ZzPointQarray](../CsFunctionsCompword.cs#L383) | 実装済検証済 | [配列境界・読出し](../tests/Program.cs#L60) |
| `0x00D7` | `zzPointQarray` | [ZzPointQarray](../CsFunctionsCompword.cs#L387) | 実装済検証済 | [quad配列境界・読出し](../tests/Program.cs#L440) |
| `0x00D8` | `zzPointJXarray` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00D9` | `zzPointBarrayLocal` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00DA` | `zzPointWarrayLocal` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00DB` | `zzPointDarrayLocal` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00DC` | `zzPointQarrayLocal` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00DD` | `zzPointQarrayLocal` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00DE` | `zzPointJXarrayLocal` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00DF` | `zzPointConsBarray` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00E0` | `zzPointConsWarray` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00E1` | `zzPointConsDarray` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00E2` | `zzPointConsQarray` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00E3` | `zzPointConsJarray` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00E4` | `zzPointStrucVariable` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00E5` | `zzPushStrucRecord` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00E6` | `zzPointStrucVariableLocal` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00E7` | `zzPushStrucRecordLocal` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00E8` | `zzRepointTempVariableShort` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00E9` | `zzRepointTempRecordShort` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00EA` | `zzRepointTempVariable` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00EB` | `zzRepointTempRecord` | [array handlers](../CsFunctionsCompword.cs) | 実装済検証済 | [配列・構造体参照](../tests/Program.cs#L511) |
| `0x00EC` | `zzIf` | [Zzif](../CsFunctionsBunki.cs#L7) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x00ED` | `zzNotif` | [ZzNotif](../CsFunctionsBunki.cs#L31) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x00EE` | `zzDO` | [ZzDo](../CsFunctionsBunki.cs#L57) | 実装済検証済 | [回数指定・脱出・フレーム](../tests/Program.cs#L46) |
| `0x00EF` | `zzLoop` | [ZzLoop](../CsFunctionsBunki.cs#L90) | 実装済検証済 | [回数指定・脱出・フレーム](../tests/Program.cs#L46) |
| `0x00F0` | `zzBreakLoop` | [ZzBreakLoop](../CsFunctionsBunki.cs#L105) | 実装済検証済 | [回数指定・脱出・フレーム](../tests/Program.cs#L46) |
| `0x00F1` | `kaisu` | [Kaisu](../CsFunctionsBunki.cs#L111) | 実装済検証済 | [回数指定・脱出・フレーム](../tests/Program.cs#L46) |
| `0x00F2` | `kaisuM1` | [KaisuM1](../CsFunctionsBunki.cs#L117) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00F3` | `kaisuP1` | [KaisuP1](../CsFunctionsBunki.cs#L118) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00F4` | `zenKausu` | [ZenKausu](../CsFunctionsBunki.cs#L119) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00F5` | `sotogawaKaisu` | [SotogawaKaisu](../CsFunctionsBunki.cs#L120) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00F6` | `sotogawaKaisuM1` | [SotogawaKaisuM1](../CsFunctionsBunki.cs#L121) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00F7` | `sotogawaKaisuP1` | [SotogawaKaisuP1](../CsFunctionsBunki.cs#L122) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00F8` | `sotogawaZenKaisu` | [SotogawaZenKaisu](../CsFunctionsBunki.cs#L123) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00F9` | `zzSelect` | [ZzSelect](../CsFunctionsBunki.cs#L134) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00FA` | `zzDcase` | [ZzDcase](../CsFunctionsBunki.cs#L143) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00FB` | `zzDrange` | [ZzDrange](../CsFunctionsBunki.cs#L144) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00FC` | `zzScase` | [ZzScase](../CsFunctionsBunki.cs#L165) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |
| `0x00FD` | `zzKatu` | [ZzKatu](../CsFunctions1ker.cs#L5) | 実装済検証済 | [論理演算](../tests/Program.cs#L35) |
| `0x00FE` | `zzMataha` | [ZzMataha](../CsFunctions1ker.cs#L17) | 実装済検証済 | [符号付き変換・スタック順序](../tests/Program.cs#L48) |
| `0x00FF` | `isTrue` | [IsTrue](../CsFunctions1ker.cs#L16) | 実装済検証済 | [回数・case opcodeテスト](../tests/Program.cs#L1742) |

### 0x0100〜0x01FF

| 命令番号 | 正規C関数名 | C#実装 | 分類 | 根拠・注意点 |
| --- | --- | --- | --- | --- |
| `0x0100` | `isFalse` | [IsFalse](../CsFunctions1ker.cs#L15) | 実装済検証済 | [論理演算](../tests/Program.cs#L35) |
| `0x0101` | `hitotuKuwae` | [HitotuKuwae](../CsFunctions1ker.cs#L112) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0102` | `futatuKuwae` | [FutatuKuwae](../CsFunctions1ker.cs#L121) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0103` | `hitotuHiki` | [HitotuHiki](../CsFunctions1ker.cs#L128) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0104` | `futatuHiki` | [FutatuHiki](../CsFunctions1ker.cs#L135) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0105` | `zzAddWimm` | [ZzAddWimm](../CsFunctions1ker.cs#L145) | 実装済検証済 | [整数演算](../tests/Program.cs#L36) |
| `0x0106` | `zzAddDimm` | [ZzAddDimm](../CsFunctions1ker.cs#L153) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0107` | `kuwae` | [Kuwae](../CsFunctions1ker.cs#L161) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0108` | `zzSubWimm` | [ZzSubWimm](../CsFunctions1ker.cs#L177) | 実装済検証済 | [整数部の即値減算](../tests/Program.cs#L36) |
| `0x0109` | `zzSubDimm` | [ZzSubDimm](../CsFunctions1ker.cs#L184) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x010A` | `hiku` | [Hiku](../CsFunctions1ker.cs#L192) | 実装済検証済 | [整数演算](../tests/Program.cs#L36) |
| `0x010B` | `kake` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x010C` | `waru` | [Waru](../CsFunctions1ker.cs#L203) | 実装済検証済 | [整数演算](../tests/Program.cs#L36) |
| `0x010D` | `amariToSyou` | [AmariToSyou](../CsFunctions1ker.cs#L27) | 実装済検証済 | [符号付き変換・スタック順序](../tests/Program.cs#L48) |
| `0x010E` | `syouToAmari` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x010F` | `amari` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0110` | `tiisai` | [Tiisai](../CsFunctions1ker.cs#L69) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0111` | `ijyou` | [Ijyou](../CsFunctions1ker.cs#L40) | 実装済検証済 | [符号付き変換・スタック順序](../tests/Program.cs#L48) |
| `0x0112` | `ookii` | [Ookii](../CsFunctions1ker.cs#L79) | 実装済検証済 | [符号付き比較](../tests/Program.cs#L34) |
| `0x0113` | `ika` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0114` | `hitosii` | [Hitosii](../CsFunctions1ker.cs#L89) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0115` | `kotonaru` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0116` | `tisaiHou` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0117` | `ookiiHou` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0118` | `isHanni` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0119` | `dotirakaniHitosii` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x011A` | `fusu` | [Fusu](../CsFunctions1ker.cs#L50) | 実装済検証済 | [符号付き変換・スタック順序](../tests/Program.cs#L48) |
| `0x011B` | `zettaiti` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x011C` | `fukusya` | [Fukusya](../CsFunctions1ker.cs#L58) | 実装済検証済 | [符号付き変換・スタック順序](../tests/Program.cs#L48) |
| `0x011D` | `pairDeFukusya` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x011E` | `koukan` | [Koukan](../CsFunctions1ker.cs#L99) | 実装済検証済 | [2ワード交換](../tests/Program.cs#L52) |
| `0x011F` | `isSei` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0120` | `isSeiNoKazu` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0121` | `isFu` | [IsFu](../CsFunctions1ker.cs#L66) | 実装済検証済 | [符号付き変換・スタック順序](../tests/Program.cs#L48) |
| `0x0122` | `sute` | [Sute](../CsFunctions1ker.cs#L212) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0123` | `nibannmeWoSute` | [NibannmeWoSute](../CsFunctions1ker.cs#L220) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0124` | `futatuSute` | [FutatuSute](../CsFunctions1ker.cs#L239) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0125` | `jyogeNibbleKoukan` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0126` | `jyogeByteKoukan` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0127` | `jyogeByteKoukan` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0128` | `jyogeWordKoukan` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0129` | `doubleSwap` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x012A` | `watasihaBigEndian` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x012B` | `watasihaLittleEndian` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x012C` | `jyouiNibble` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x012D` | `kaiNibble` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x012E` | `jyouiByte` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x012F` | `kaiByte` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0130` | `jyouiWord` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0131` | `kaiWord` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0132` | `hidariShift` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0133` | `migiShift` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0134` | `zzAnd` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0135` | `zzOr` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0136` | `zzXor` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0137` | `zzNot` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0138` | `zzAndWimm` | [ZzAndWimm](../CsFunctions1ker.cs#L67) | 実装済検証済 | [符号付き変換・スタック順序](../tests/Program.cs#L48) |
| `0x0139` | `zzAndDimm` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x013A` | `zzOrWimm` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x013B` | `zzOrDimm` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x013C` | `pack` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x013D` | `unpack` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x013E` | `mitasu` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x013F` | `tenso` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0140` | `tenso` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0141` | `syousuumadewaru` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0142` | `syousuka` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0143` | `seisuka` | [integer utility handlers](../CsFunctionsNumeric.cs) | 実装済検証済 | [integer utility opcode test](../tests/Program.cs#L1507) |
| `0x0144` | `fkuwae` | [Fkuwae](../CsFunctionsNumeric.cs#L177) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0145` | `fhiku` | [Fhiku](../CsFunctionsNumeric.cs#L178) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0146` | `fkake` | [Fkake](../CsFunctionsNumeric.cs#L179) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0147` | `fwaru` | [Fwaru](../CsFunctionsNumeric.cs#L180) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0148` | `fhitosii` | [Fhitosii](../CsFunctionsNumeric.cs#L181) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0149` | `fookii` | [Fookii](../CsFunctionsNumeric.cs#L182) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x014A` | `ftiisai` | [Ftiisai](../CsFunctionsNumeric.cs#L183) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x014B` | `fijyou` | [Fijyou](../CsFunctionsNumeric.cs#L184) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x014C` | `fika` | [Fika](../CsFunctionsNumeric.cs#L185) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x014D` | `fkotonaru` | [Fkotonaru](../CsFunctionsNumeric.cs#L186) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x014E` | `c_acos` | [C_acos](../CsFunctionsNumeric.cs#L188) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x014F` | `c_asin` | [C_asin](../CsFunctionsNumeric.cs#L189) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0150` | `c_atan` | [C_atan](../CsFunctionsNumeric.cs#L190) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0151` | `c_cos` | [C_cos](../CsFunctionsNumeric.cs#L191) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0152` | `c_sin` | [C_sin](../CsFunctionsNumeric.cs#L192) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0153` | `c_tan` | [C_tan](../CsFunctionsNumeric.cs#L193) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0154` | `c_cosh` | [C_cosh](../CsFunctionsNumeric.cs#L194) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0155` | `c_sinh` | [C_sinh](../CsFunctionsNumeric.cs#L195) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0156` | `c_tanh` | [C_tanh](../CsFunctionsNumeric.cs#L196) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0157` | `c_exp` | [C_exp](../CsFunctionsNumeric.cs#L197) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0158` | `c_log` | [C_log](../CsFunctionsNumeric.cs#L198) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x0159` | `c_log10` | [C_log10](../CsFunctionsNumeric.cs#L199) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x015A` | `c_pow` | [C_pow](../CsFunctionsNumeric.cs#L200) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x015B` | `c_sqrt` | [C_sqrt](../CsFunctionsNumeric.cs#L201) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x015C` | `c_fabs` | [C_fabs](../CsFunctionsNumeric.cs#L202) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x015D` | `c_radian` | [C_radian](../CsFunctionsNumeric.cs#L203) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x015E` | `c_degree` | [C_degree](../CsFunctionsNumeric.cs#L204) | 実装済検証済 | [浮動小数点演算・関数テスト](../tests/Program.cs#L1428) |
| `0x015F` | `isKuretu` | [IsKuretu](../CsFunctions2ker0.cs#L5) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0160` | `isNotKuretu` | [IsNotKuretu](../CsFunctions2ker0.cs#L13) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0161` | `zzCtoS` | [ZzCtoS](../CsFunctions2ker0.cs#L22) | 実装済検証済 | [文字コード・スタック実体参照](../tests/Program.cs#L42) |
| `0x0162` | `sentoKetasuSeigen` | [SentoKetasuSeigen](../CsFunctions2ker0.cs#L19) | 実装済検証済 | [文字列境界・Xvarテスト](../tests/Program.cs#L950) |
| `0x0163` | `matubiKetasuSeigen` | [MatubiKetasuSeigen](../CsFunctions2ker0.cs#L20) | 実装済検証済 | [文字列境界・Xvarテスト](../tests/Program.cs#L950) |
| `0x0164` | `zzPopJvar` | [ZzPopJvar](../CsFunctions2ker0.cs#L52) | 実装済検証済 | [実体格納・容量・文字境界](../tests/Program.cs#L44) |
| `0x0165` | `zzWriteJvar` | [ZzWriteJvar](../CsFunctions2ker0.cs#L54) | 実装済検証済 | [実体格納・容量・文字境界](../tests/Program.cs#L44) |
| `0x0166` | `zzWriteJvarLocal` | [ZzWriteJvarLocal](../CsFunctions2ker0.cs#L60) | 実装済検証済 | [実体格納・容量・文字境界](../tests/Program.cs#L44) |
| `0x0167` | `mojiretuJittaiWoIre` | [MojiretuJittaiWoIre](../CsFunctions2ker0.cs#L66) | 実装済検証済 | [実体格納・容量・文字境界](../tests/Program.cs#L44) |
| `0x0168` | `zzClearJvar` | [ZzClearJvar](../CsFunctions2ker0.cs#L73) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0169` | `zzClearJvar` | [ZzClearJvar](../CsFunctions2ker0.cs#L73) | 実装済検証済 | [既定実体・ヘッダー](../tests/Program.cs#L45) |
| `0x016A` | `mojiretuJittaiWoSansyo` | [MojiretuJittaiWoSansyo](../CsFunctions2ker0.cs#L89) | 実装済検証済 | [既定実体・ヘッダー](../tests/Program.cs#L45) |
| `0x016B` | `zzPopXvar` | [ZzPopXvar](../CsFunctions2ker0.cs#L110) | 実装済検証済 | [文字列境界・Xvarテスト](../tests/Program.cs#L950) |
| `0x016C` | `zzWriteXvar` | [ZzWriteXvar](../CsFunctions2ker0.cs#L112) | 実装済検証済 | [文字列境界・Xvarテスト](../tests/Program.cs#L950) |
| `0x016D` | `zzWriteXvarLocal` | [ZzWriteXvarLocal](../CsFunctions2ker0.cs#L118) | 実装済検証済 | [文字列境界・Xvarテスト](../tests/Program.cs#L950) |
| `0x016E` | `zzClearXvar` | [ZzClearXvar](../CsFunctionsCompword.cs#L1304) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x016F` | `tanjyunMojiretuJittaiWoClear` | [TanjyunMojiretuJittaiWoClear](../CsFunctions2ker0.cs#L124) | 実装済検証済 | [文字列境界・Xvarテスト](../tests/Program.cs#L950) |
| `0x0170` | `chgDefaultStruct` | [ChgDefaultStruct](../CsFunctions2ker0.cs#L95) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0171` | `chgDefaultStruct` | [ChgDefaultStruct](../CsFunctions2ker0.cs#L95) | 実装済検証済 | [既定実体・ヘッダー](../tests/Program.cs#L45) |
| `0x0172` | `mojiretuJittaiJohoWoEru` | [MojiretuJittaiJohoWoEru](../CsFunctions2ker0.cs#L131) | 実装済検証済 | [文字列境界・Xvarテスト](../tests/Program.cs#L950) |
| `0x0173` | `tuikaYoyu` | [TuikaYoyu](../CsFunctions2ker1.cs#L14) | 実装済検証済 | [追加余裕・容量・重複領域](../tests/Program.cs#L50) |
| `0x0174` | `tuikaYoyuForJinfo` | [TuikaYoyuForJinfo](../CsFunctions2ker1.cs#L21) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x0175` | `hidariTuikaYoyu` | [HidariTuikaYoyu](../CsFunctions2ker1.cs#L28) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x0176` | `hidariTuikaYoyuForJinfo` | [HidariTuikaYoyuForJinfo](../CsFunctions2ker1.cs#L35) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x0177` | `itiMojiTuika` | [ItiMojiTuika](../CsFunctions2ker1.cs#L21) | 実装済検証済 | [追加余裕・容量・重複領域](../tests/Program.cs#L50) |
| `0x0178` | `itiMojiTuikaForJinfo` | [ItiMojiTuikaForJinfo](../CsFunctions2ker1.cs#L49) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x0179` | `itiMojiHigaTuika` | [ItiMojiHigaTuika](../CsFunctions2ker1.cs#L69) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x017A` | `itiMojiHigaTuikaForJinfo` | [ItiMojiHigaTuikaForJinfo](../CsFunctions2ker1.cs#L37) | 実装済検証済 | [余白・切出し・文字境界](../tests/Program.cs#L49) |
| `0x017B` | `tuika` | [Tuika](../CsFunctions2ker1.cs#L53) | 実装済検証済 | [追加余裕・容量・重複領域](../tests/Program.cs#L50) |
| `0x017C` | `tuikaForJinfo` | [TuikaForJinfo](../CsFunctions2ker1.cs#L103) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x017D` | `higaTuika` | [HigaTuika](../CsFunctions2ker1.cs#L121) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x017E` | `higaTuikaForJinfo` | [HigaTuikaForJinfo](../CsFunctions2ker1.cs#L127) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x017F` | `fillSpace` | [FillSpace](../CsFunctions2ker1.cs#L148) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x0180` | `fillSpaceForJinfo` | [FillSpaceForJinfo](../CsFunctions2ker1.cs#L153) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x0181` | `migiYose` | [MigiYose](../CsFunctions2ker1.cs#L181) | 実装済検証済 | [文字列余白・Jinfoテスト](../tests/Program.cs#L1009) |
| `0x0182` | `migiYoseForJinfo` | [MigiYoseForJinfo](../CsFunctions2ker1.cs#L67) | 実装済検証済 | [余白・切出し・文字境界](../tests/Program.cs#L49) |
| `0x0183` | `sukosiMigiYose` | [SukosiMigiYose](../CsFunctions2ker1.cs#L192) | 実装済検証済 | [文字列寄せ・情報設定テスト](../tests/Program.cs#L1141) |
| `0x0184` | `sukosiMigiYoseForJinfo` | [SukosiMigiYoseForJinfo](../CsFunctions2ker1.cs#L200) | 実装済検証済 | [文字列寄せ・情報設定テスト](../tests/Program.cs#L1141) |
| `0x0185` | `hidariYose` | [HidariYose](../CsFunctions2ker1.cs#L219) | 実装済検証済 | [文字列寄せ・情報設定テスト](../tests/Program.cs#L1141) |
| `0x0186` | `hidariYoseForJinfo` | [HidariYoseForJinfo](../CsFunctions2ker1.cs#L225) | 実装済検証済 | [文字列寄せ・情報設定テスト](../tests/Program.cs#L1141) |
| `0x0187` | `sukosiHidariYose` | [SukosiHidariYose](../CsFunctions2ker1.cs#L231) | 実装済検証済 | [文字列寄せ・情報設定テスト](../tests/Program.cs#L1141) |
| `0x0188` | `sukosiHidariYoseForJinfo` | [SukosiHidariYoseForJinfo](../CsFunctions2ker1.cs#L239) | 実装済検証済 | [文字列寄せ・情報設定テスト](../tests/Program.cs#L1141) |
| `0x0189` | `jittaiCountSettei` | [JittaiCountSettei](../CsFunctions2ker1.cs#L260) | 実装済検証済 | [文字列寄せ・情報設定テスト](../tests/Program.cs#L1141) |
| `0x018A` | `jittaiCountSetteiForJinfo` | [JittaiCountSetteiForJinfo](../CsFunctions2ker1.cs#L266) | 実装済検証済 | [文字列寄せ・情報設定テスト](../tests/Program.cs#L1141) |
| `0x018B` | `jittaiOffsetSettei` | [JittaiOffsetSettei](../CsFunctions2ker1.cs#L272) | 実装済検証済 | [文字列寄せ・情報設定テスト](../tests/Program.cs#L1141) |
| `0x018C` | `jittaiOffsetSetteiForJinfo` | [JittaiOffsetSetteiForJinfo](../CsFunctions2ker1.cs#L278) | 実装済検証済 | [文字列寄せ・情報設定テスト](../tests/Program.cs#L1141) |
| `0x018D` | `ooMojiHenkan` | [OoMojiHenkan](../CsFunctions2ker1.cs#L284) | 実装済検証済 | [大文字変換テスト](../tests/Program.cs#L1234) |
| `0x018E` | `ooMojiHenkanForJinfo` | [OoMojiHenkanForJinfo](../CsFunctions2ker1.cs#L290) | 実装済検証済 | [大文字変換テスト](../tests/Program.cs#L1234) |
| `0x018F` | `itiBaitoSakujyoForJvar` | [ItiBaitoSakujyoForJvar](../CsFunctions2ker2.cs#L25) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x0190` | `itiBaitoSakujyo` | [ItiBaitoSakujyo](../CsFunctions2ker2.cs#L26) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x0191` | `itiBaitoSakujyoForJinfo` | [ItiBaitoSakujyoForJinfo](../CsFunctions2ker2.cs#L31) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x0192` | `itiMojiSakujyoForJvar` | [ItiMojiSakujyoForJvar](../CsFunctions2ker2.cs#L32) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x0193` | `itiMojiSakujyo` | [ItiMojiSakujyo](../CsFunctions2ker2.cs#L33) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x0194` | `itiMojiSakujyoForJinfo` | [ItiMojiSakujyoForJinfo](../CsFunctions2ker2.cs#L38) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x0195` | `itiBaitoMigiSakujyoForJvar` | [ItiBaitoMigiSakujyoForJvar](../CsFunctions2ker2.cs#L39) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x0196` | `itiBaitoMigiSakujyo` | [ItiBaitoMigiSakujyo](../CsFunctions2ker2.cs#L40) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x0197` | `itiBaitoMigiSakujyoForJinfo` | [ItiBaitoMigiSakujyoForJinfo](../CsFunctions2ker2.cs#L45) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x0198` | `itiMojiMigiSakujyoForJvar` | [ItiMojiMigiSakujyoForJvar](../CsFunctions2ker2.cs#L46) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x0199` | `itiMojiMigiSakujyo` | [ItiMojiMigiSakujyo](../CsFunctions2ker2.cs#L47) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x019A` | `itiMojiMigiSakujyoForJinfo` | [ItiMojiMigiSakujyoForJinfo](../CsFunctions2ker2.cs#L52) | 実装済検証済 | [文字列削除テスト](../tests/Program.cs#L1271) |
| `0x019B` | `itiBaitoKiridasiForJvar` | [ItiBaitoKiridasiForJvar](../CsFunctions2ker2.cs#L83) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x019C` | `itiBaitoKiridasi` | [ItiBaitoKiridasi](../CsFunctions2ker2.cs#L84) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x019D` | `itiBaitoKiridasiForJinfo` | [ItiBaitoKiridasiForJinfo](../CsFunctions2ker2.cs#L85) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x019E` | `itiMojiKiridasiForJvar` | [ItiMojiKiridasiForJvar](../CsFunctions2ker2.cs#L86) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x019F` | `itiMojiKiridasi` | [ItiMojiKiridasi](../CsFunctions2ker2.cs#L87) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x01A0` | `itiMojiKiridasiForJinfo` | [ItiMojiKiridasiForJinfo](../CsFunctions2ker2.cs#L88) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x01A1` | `itiBaitoMigiKiridasiForJvar` | [ItiBaitoMigiKiridasiForJvar](../CsFunctions2ker2.cs#L89) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x01A2` | `itiBaitoMigiKiridasi` | [ItiBaitoMigiKiridasi](../CsFunctions2ker2.cs#L90) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x01A3` | `itiBaitoMigiKiridasiForJinfo` | [ItiBaitoMigiKiridasiForJinfo](../CsFunctions2ker2.cs#L91) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x01A4` | `itiMojiMigiKiridasiForJvar` | [ItiMojiMigiKiridasiForJvar](../CsFunctions2ker2.cs#L92) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x01A5` | `itiMojiMigiKiridasi` | [ItiMojiMigiKiridasi](../CsFunctions2ker2.cs#L58) | 実装済検証済 | [余白・切出し・文字境界](../tests/Program.cs#L49) |
| `0x01A6` | `itiMojiMigiKiridasiForJinfo` | [ItiMojiMigiKiridasiForJinfo](../CsFunctions2ker2.cs#L93) | 実装済検証済 | [文字列切出しテスト](../tests/Program.cs#L1356) |
| `0x01A7` | `baitoRetuKiridasiForJvar` | [BaitoRetuKiridasiForJvar](../CsFunctions2ker2.cs#L159) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01A8` | `baitoRetuKiridasi` | [BaitoRetuKiridasi](../CsFunctions2ker2.cs#L160) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01A9` | `baitoRetuKiridasiForJinfo` | [BaitoRetuKiridasiForJinfo](../CsFunctions2ker2.cs#L161) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01AA` | `baitoRetuMigiKiridasiForJvar` | [BaitoRetuMigiKiridasiForJvar](../CsFunctions2ker2.cs#L162) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01AB` | `baitoRetuMigiKiridasi` | [BaitoRetuMigiKiridasi](../CsFunctions2ker2.cs#L163) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01AC` | `baitoRetuMigiKiridasiForJinfo` | [BaitoRetuMigiKiridasiForJinfo](../CsFunctions2ker2.cs#L164) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01AD` | `baitoRetuSakujyoForJvar` | [BaitoRetuSakujyoForJvar](../CsFunctions2ker2.cs#L165) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01AE` | `baitoRetuSakujyo` | [BaitoRetuSakujyo](../CsFunctions2ker2.cs#L166) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01AF` | `baitoRetuSakujyoForJinfo` | [BaitoRetuSakujyoForJinfo](../CsFunctions2ker2.cs#L167) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01B0` | `baitoRetuMigiSakujyoForJvar` | [BaitoRetuMigiSakujyoForJvar](../CsFunctions2ker2.cs#L168) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01B1` | `baitoRetuMigiSakujyo` | [BaitoRetuMigiSakujyo](../CsFunctions2ker2.cs#L169) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01B2` | `baitoRetuMigiSakujyoForJinfo` | [BaitoRetuMigiSakujyoForJinfo](../CsFunctions2ker2.cs#L170) | 実装済検証済 | [バイト範囲テスト](../tests/Program.cs#L1417) |
| `0x01B3` | `kiridasiForJvar` | [KiridasiForJvar](../CsFunctions2ker2.cs#L252) | 実装済検証済 | [文字数切出しテスト](../tests/Program.cs#L1495) |
| `0x01B4` | `kiridasi` | [Kiridasi](../CsFunctions2ker2.cs#L253) | 実装済検証済 | [文字数切出しテスト](../tests/Program.cs#L1495) |
| `0x01B5` | `kiridasiForJinfo` | [KiridasiForJinfo](../CsFunctions2ker2.cs#L254) | 実装済検証済 | [文字数切出しテスト](../tests/Program.cs#L1495) |
| `0x01B6` | `migiKiridasiForJvar` | [MigiKiridasiForJvar](../CsFunctions2ker2.cs#L255) | 実装済検証済 | [文字数切出しテスト](../tests/Program.cs#L1495) |
| `0x01B7` | `migiKiridasi` | [MigiKiridasi](../CsFunctions2ker2.cs#L257) | 実装済検証済 | [文字数切出しテスト](../tests/Program.cs#L1495) |
| `0x01B8` | `migiKiridasiForJinfo` | [MigiKiridasiForJinfo](../CsFunctions2ker2.cs#L256) | 実装済検証済 | [文字数切出しテスト](../tests/Program.cs#L1495) |
| `0x01B9` | `bundanForJvar` | [BundanForJvar](../CsFunctions2ker2.cs#L261) | 実装済検証済 | [文字・バイト区切りテスト](../tests/Program.cs#L1543) |
| `0x01BA` | `bundan` | [Bundan](../CsFunctions2ker2.cs#L262) | 実装済検証済 | [文字・バイト区切りテスト](../tests/Program.cs#L1543) |
| `0x01BB` | `bundanForJinfo` | [BundanForJinfo](../CsFunctions2ker2.cs#L263) | 実装済検証済 | [文字・バイト区切りテスト](../tests/Program.cs#L1543) |
| `0x01BC` | `byte_bundanForJvar` | [ByteBundanForJvar](../CsFunctions2ker2.cs#L264) | 実装済検証済 | [文字・バイト区切りテスト](../tests/Program.cs#L1543) |
| `0x01BD` | `byte_bundan` | [ByteBundan](../CsFunctions2ker2.cs#L265) | 実装済検証済 | [文字・バイト区切りテスト](../tests/Program.cs#L1543) |
| `0x01BE` | `byte_bundanForJinfo` | [ByteBundanForJinfo](../CsFunctions2ker2.cs#L266) | 実装済検証済 | [文字・バイト区切りテスト](../tests/Program.cs#L1543) |
| `0x01BF` | `sakujyoForJvar` | [SakujyoForJvar](../CsFunctions2ker2.cs#L267) | 実装済検証済 | [文字数削除テスト](../tests/Program.cs#L1589) |
| `0x01C0` | `sakujyo` | [Sakujyo](../CsFunctions2ker2.cs#L268) | 実装済検証済 | [文字数削除テスト](../tests/Program.cs#L1589) |
| `0x01C1` | `sakujyoForJinfo` | [SakujyoForJinfo](../CsFunctions2ker2.cs#L269) | 実装済検証済 | [文字数削除テスト](../tests/Program.cs#L1589) |
| `0x01C2` | `migiSakujyoForJvar` | [MigiSakujyoForJvar](../CsFunctions2ker2.cs#L270) | 実装済検証済 | [文字数削除テスト](../tests/Program.cs#L1589) |
| `0x01C3` | `migiSakujyo` | [MigiSakujyo](../CsFunctions2ker2.cs#L271) | 実装済検証済 | [文字数削除テスト](../tests/Program.cs#L1589) |
| `0x01C4` | `migiSakujyoForJinfo` | [MigiSakujyoForJinfo](../CsFunctions2ker2.cs#L272) | 実装済検証済 | [文字数削除テスト](../tests/Program.cs#L1589) |
| `0x01C5` | `kuhakuWoNozokuForJvar` | [KuhakuWoNozokuForJvar](../CsFunctions2ker2.cs#L273) | 実装済検証済 | [空白除去テスト](../tests/Program.cs#L1636) |
| `0x01C6` | `kuhakuWoNozoku` | [KuhakuWoNozoku](../CsFunctions2ker2.cs#L274) | 実装済検証済 | [空白除去テスト](../tests/Program.cs#L1636) |
| `0x01C7` | `kuhakuWoNozokuForJinfo` | [KuhakuWoNozokuForJinfo](../CsFunctions2ker2.cs#L275) | 実装済検証済 | [空白除去テスト](../tests/Program.cs#L1636) |
| `0x01C8` | `migiKuhakuWoNozokuForJvar` | [MigiKuhakuWoNozokuForJvar](../CsFunctions2ker2.cs#L276) | 実装済検証済 | [空白除去テスト](../tests/Program.cs#L1636) |
| `0x01C9` | `migiKuhakuWoNozoku` | [MigiKuhakuWoNozoku](../CsFunctions2ker2.cs#L277) | 実装済検証済 | [空白除去テスト](../tests/Program.cs#L1636) |
| `0x01CA` | `migiKuhakuWoNozokuForJinfo` | [MigiKuhakuWoNozokuForJinfo](../CsFunctions2ker2.cs#L278) | 実装済検証済 | [空白除去テスト](../tests/Program.cs#L1636) |
| `0x01CB` | `tangoKiridasiForJvar` | [TangoKiridasiForJvar](../CsFunctions2ker2.cs#L279) | 実装済検証済 | [単語切出しテスト](../tests/Program.cs#L1673) |
| `0x01CC` | `tangoKiridasi` | [TangoKiridasi](../CsFunctions2ker2.cs#L280) | 実装済検証済 | [単語切出しテスト](../tests/Program.cs#L1673) |
| `0x01CD` | `tangoKiridasiForJinfo` | [TangoKiridasiForJinfo](../CsFunctions2ker2.cs#L281) | 実装済検証済 | [単語切出しテスト](../tests/Program.cs#L1673) |
| `0x01CE` | `mojisu` | [Mojisu](../CsFunctions2ker2.cs#L282) | 実装済検証済 | [文字数・選択テスト](../tests/Program.cs#L1719) |
| `0x01CF` | `satanBaito` | [SatanBaito](../CsFunctions2ker2.cs#L283) | 実装済検証済 | [文字数・選択テスト](../tests/Program.cs#L1719) |
| `0x01D0` | `satanMoji` | [SatanMoji](../CsFunctions2ker2.cs#L284) | 実装済検証済 | [文字数・選択テスト](../tests/Program.cs#L1719) |
| `0x01D1` | `utanBaito` | [UtanBaito](../CsFunctions2ker2.cs#L285) | 実装済検証済 | [文字数・選択テスト](../tests/Program.cs#L1719) |
| `0x01D2` | `utanMoji` | [UtanMoji](../CsFunctions2ker2.cs#L42) | 実装済検証済 | [空列・末尾文字](../tests/Program.cs#L51) |
| `0x01D3` | `siteiItiNoMoji` | [SiteiItiNoMoji](../CsFunctions2ker2.cs#L286) | 実装済検証済 | [文字数・選択テスト](../tests/Program.cs#L1719) |
| `0x01D4` | `migiSiteiItiNoMoji` | [MigiSiteiItiNoMoji](../CsFunctions2ker2.cs#L287) | 実装済検証済 | [文字数・選択テスト](../tests/Program.cs#L1719) |
| `0x01D5` | `toridasi` | [Toridasi](../CsFunctions2ker2.cs#L83) | 実装済検証済 | [実体参照・文字境界](../tests/Program.cs#L38) |
| `0x01D6` | `migiToridasi` | [MigiToridasi](../CsFunctions2ker2.cs#L705) | 実装済検証済 | [文字数・選択テスト](../tests/Program.cs#L1719) |
| `0x01D7` | `itiBaitoKensaku` | [ItiBaitoKensaku](../CsFunctions2ker2.cs#L114) | 実装済検証済 | [データ領域検索](../tests/Program.cs#L54) |
| `0x01D8` | `gyakuItiBaitoKensaku` | [GyakuItiBaitoKensaku](../CsFunctions2ker2.cs#L142) | 実装済検証済 | [データ領域検索](../tests/Program.cs#L54) |
| `0x01D9` | `itiMojiKensaku` | [ItiMojiKensaku](../CsFunctions2ker2.cs#L291) | 実装済検証済 | [検索・比較テスト](../tests/Program.cs#L1796) |
| `0x01DA` | `kensaku` | [Kensaku](../CsFunctions2ker2.cs#L292) | 実装済検証済 | [検索・比較テスト](../tests/Program.cs#L1796) |
| `0x01DB` | `zan_kensakumojiretu` | [ZanKensakumojiretu](../CsFunctions2ker2.cs#L293) | 実装済検証済 | [検索・比較テスト](../tests/Program.cs#L1796) |
| `0x01DC` | `baito_retu_kensaku` | [BaitoRetuKensaku](../CsFunctions2ker2.cs#L298) | 実装済検証済 | [検索・比較テスト](../tests/Program.cs#L1796) |
| `0x01DD` | `hitosiiMojiretu` | [HitosiiMojiretu](../CsFunctions2ker2.cs#L101) | 実装済検証済 | [領域間文字列比較](../tests/Program.cs#L39) |
| `0x01DE` | `mojiretuHikaku_0` | [MojiretuHikaku0](../CsFunctions2ker2.cs#L299) | 実装済検証済 | [検索・比較テスト](../tests/Program.cs#L1796) |
| `0x01DF` | `mojiretuHikaku` | [MojiretuHikaku](../CsFunctions2ker2.cs#L300) | 実装済検証済 | [検索・比較テスト](../tests/Program.cs#L1796) |
| `0x01E0` | `filenameHikaku` | [FilenameHikaku](../CsFunctions2ker2.cs#L301) | 実装済検証済 | [検索・比較テスト](../tests/Program.cs#L1796) |
| `0x01E1` | `isKanjiDaiitiBaito_SJIS` | [IsKanjiDaiitiBaitoSjis](../CsFunctions2ker2.cs#L302) | 実装済検証済 | [文字種判定テスト](../tests/Program.cs#L1875) |
| `0x01E2` | `isKanjiDaiitiBaito_EUC` | [IsKanjiDaiitiBaitoEuc](../CsFunctions2ker2.cs#L304) | 実装済検証済 | [文字種判定テスト](../tests/Program.cs#L1875) |
| `0x01E3` | `isKanjiDaiitiBaito` | [IsKanjiDaiitiBaito](../CsFunctions2ker2.cs#L306) | 実装済検証済 | [文字種判定テスト](../tests/Program.cs#L1875) |
| `0x01E4` | `isKanjiCode` | [IsKanjiCode](../CsFunctions2ker2.cs#L308) | 実装済検証済 | [文字種判定テスト](../tests/Program.cs#L1875) |
| `0x01E5` | `isHankakuKana` | [IsHankakuKana](../CsFunctions2ker2.cs#L309) | 実装済検証済 | [文字種判定テスト](../tests/Program.cs#L1875) |
| `0x01E6` | `mojiHaba` | [MojiHaba](../CsFunctions2ker2.cs#L311) | 実装済検証済 | [文字種判定テスト](../tests/Program.cs#L1875) |
| `0x01E7` | `hyojiHaba` | [HyojiHaba](../CsFunctions2ker2.cs#L312) | 実装済検証済 | [文字種判定テスト](../tests/Program.cs#L1875) |
| `0x01E8` | `isKuhaku` | [IsKuhaku](../CsFunctions2ker2.cs#L313) | 実装済検証済 | [文字種判定テスト](../tests/Program.cs#L1875) |
| `0x01E9` | `konoBaitoSu` | [KonoBaitoSu](../CsFunctions2ker2.cs#L76) | 実装済検証済 | [余白・切出し・文字境界](../tests/Program.cs#L49) |
| `0x01EA` | `baitoSuWoEru` | [BaitoSuWoEru](../CsFunctions2ker2.cs#L17) | 実装済検証済 | [サイズ操作テスト](../tests/Program.cs#L1911) |
| `0x01EB` | `baitoSuWoEru` | [BaitoSuWoEru](../CsFunctions2ker2.cs#L17) | 実装済検証済 | [サイズ操作テスト](../tests/Program.cs#L1911) |
| `0x01EC` | `sizeHenko` | [SizeHenko](../CsFunctions2ker2.cs#L7) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x01ED` | `sizeGensyo` | [SizeGensyo](../CsFunctions2ker2.cs#L24) | 実装済検証済 | [サイズ操作テスト](../tests/Program.cs#L1911) |
| `0x01EE` | `c_sjis_to_jis` | [CSjisToJis](../CsFunctions2ker2.cs#L365) | 実装済検証済 | [文字コード変換テスト](../tests/Program.cs#L1948) |
| `0x01EF` | `c_jis_to_sjis` | [CJisToSjis](../CsFunctions2ker2.cs#L366) | 実装済検証済 | [文字コード変換テスト](../tests/Program.cs#L1948) |
| `0x01F0` | `itiMojiOhmojiHenkan` | [ItiMojiOhmojiHenkan](../CsFunctions2ker2.cs#L367) | 実装済検証済 | [文字コード変換テスト](../tests/Program.cs#L1948) |
| `0x01F1` | `itiMojiSutiHenkan` | [ItiMojiSutiHenkan](../CsFunctions2ker2.cs#L368) | 実装済検証済 | [文字コード変換テスト](../tests/Program.cs#L1948) |
| `0x01F2` | `itiMojiSutiHenkanHex` | [ItiMojiSutiHenkanHex](../CsFunctions2ker2.cs#L369) | 実装済検証済 | [文字コード変換テスト](../tests/Program.cs#L1948) |
| `0x01F3` | `itiMojiZenkakuHenkan` | [ItiMojiZenkakuHenkan](../CsFunctions2ker2.cs#L370) | 実装済検証済 | [文字コード変換テスト](../tests/Program.cs#L1948) |
| `0x01F4` | `isKuhakuMatahaSeparator` | [IsKuhakuMatahaSeparator](../CsFunctions2ker2.cs#L371) | 実装済検証済 | [文字コード変換テスト](../tests/Program.cs#L1948) |
| `0x01F5` | `is_S_Kuretu` | [IsSvarEmpty](../CsFunctions2ker2.cs#L57) | 実装済検証済 | [Svar状態・削除テスト](../tests/Program.cs#L1999) |
| `0x01F6` | `is_S_NotKuretu` | [IsSvarNotEmpty](../CsFunctions2ker2.cs#L58) | 実装済検証済 | [Svar状態・削除テスト](../tests/Program.cs#L1999) |
| `0x01F7` | `S_itiBaitoSakujyo` | [SvarDeleteFirstByte](../CsFunctions2ker2.cs#L59) | 実装済検証済 | [Svar状態・削除テスト](../tests/Program.cs#L1999) |
| `0x01F8` | `S_itiBaitoMigiSakujyo` | [SvarDeleteLastByte](../CsFunctions2ker2.cs#L60) | 実装済検証済 | [Svar状態・削除テスト](../tests/Program.cs#L1999) |
| `0x01F9` | `S_itiMojiSakujyo` | [SvarDeleteFirstCharacter](../CsFunctions2ker2.cs#L61) | 実装済検証済 | [Svar状態・削除テスト](../tests/Program.cs#L1999) |
| `0x01FA` | `S_itiMojiMigiSakujyo` | [SvarDeleteLastCharacter](../CsFunctions2ker2.cs#L62) | 実装済検証済 | [Svar状態・削除テスト](../tests/Program.cs#L1999) |
| `0x01FB` | `S_itiBaitoKiridasi` | — | 未実装 | 未登録 |
| `0x01FC` | `S_itiBaitoMigiKiridasi` | — | 未実装 | 未登録 |
| `0x01FD` | `S_itiMojiKiridasi` | — | 未実装 | 未登録 |
| `0x01FE` | `S_itiMojiMigiKiridasi` | — | 未実装 | 未登録 |
| `0x01FF` | `S_baitoRetuKiridasi` | — | 未実装 | 未登録 |

### 0x0200〜0x02F6

| 命令番号 | 正規C関数名 | C#実装 | 分類 | 根拠・注意点 |
| --- | --- | --- | --- | --- |
| `0x0200` | `S_baitoRetuMigiKiridasi` | — | 未実装 | 未登録 |
| `0x0201` | `S_baitoRetuSakujyo` | — | 未実装 | 未登録 |
| `0x0202` | `S_baitoRetuMigiSakujyo` | — | 未実装 | 未登録 |
| `0x0203` | `S_kiridasi` | — | 未実装 | 未登録 |
| `0x0204` | `S_migiKiridasi` | — | 未実装 | 未登録 |
| `0x0205` | `S_bundan` | — | 未実装 | 未登録 |
| `0x0206` | `S_baitoRetuBundan` | — | 未実装 | 未登録 |
| `0x0207` | `S_sakujyo` | — | 未実装 | 未登録 |
| `0x0208` | `S_migiSakujyo` | — | 未実装 | 未登録 |
| `0x0209` | `S_kuhakuWoNozku` | — | 未実装 | 未登録 |
| `0x020A` | `S_migiKuhakuWoNozku` | — | 未実装 | 未登録 |
| `0x020B` | `S_tangoKiridasi` | — | 未実装 | 未登録 |
| `0x020C` | `Mind_tangoKiridasi` | — | 未実装 | 未登録 |
| `0x020D` | `S_satanBaito` | — | 未実装 | 未登録 |
| `0x020E` | `S_utanBaito` | — | 未実装 | 未登録 |
| `0x020F` | `S_satanMoji` | — | 未実装 | 未登録 |
| `0x0210` | `S_utanMoji` | — | 未実装 | 未登録 |
| `0x0211` | `S_siteiItiNoMoji` | — | 未実装 | 未登録 |
| `0x0212` | `S_migiSiteiItiNoMoji` | — | 未実装 | 未登録 |
| `0x0213` | `S_baitoRetuWoEru` | — | 未実装 | 未登録 |
| `0x0214` | `S_baitoRetuMigiWoEru` | — | 未実装 | 未登録 |
| `0x0215` | `S_toridasi` | — | 未実装 | 未登録 |
| `0x0216` | `S_migiToridasi` | — | 未実装 | 未登録 |
| `0x0217` | `S_itiMojiKensaku` | — | 未実装 | 未登録 |
| `0x0218` | `S_kensaku` | — | 未実装 | 未登録 |
| `0x0219` | `S_sakujyo_mojiretuwo_eru` | — | 未実装 | 未登録 |
| `0x021A` | `S_sakujyo_baitosuwo_eru` | — | 未実装 | 未登録 |
| `0x021B` | `S_migi_sakujyo_mojiretuwo_eru` | — | 未実装 | 未登録 |
| `0x021C` | `S_migi_sakujyo_baitosuwo_eru` | — | 未実装 | 未登録 |
| `0x021D` | `S_satan_itide_migi_sakujyo_baitosuwo_eru` | — | 未実装 | 未登録 |
| `0x021E` | `baitoRetuToridasi` | — | 未実装 | 未登録 |
| `0x021F` | `baitoRetuMigiToridasi` | — | 未実装 | 未登録 |
| `0x0220` | `zzUCtoS` | — | 未実装 | 未登録 |
| `0x0221` | `u_sentoKetasuSeigen` | — | 未実装 | 未登録 |
| `0x0222` | `u_matubiKetasuSeigen` | — | 未実装 | 未登録 |
| `0x0223` | `u_mojiretuJittaiWoIre` | — | 未実装 | 未登録 |
| `0x0224` | `u_itiMojiTuika` | — | 未実装 | 未登録 |
| `0x0225` | `u_itiMojiTuikaForJinfo` | — | 未実装 | 未登録 |
| `0x0226` | `u_itiMojiHigaTuika` | — | 未実装 | 未登録 |
| `0x0227` | `u_itiMojiHigaTuikaForJinfo` | — | 未実装 | 未登録 |
| `0x0228` | `u_tuika` | — | 未実装 | 未登録 |
| `0x0229` | `u_tuikaForJinfo` | — | 未実装 | 未登録 |
| `0x022A` | `u_higaTuika` | — | 未実装 | 未登録 |
| `0x022B` | `u_higaTuikaForJinfo` | — | 未実装 | 未登録 |
| `0x022C` | `u_itiMojiSakujyoForJvar` | — | 未実装 | 未登録 |
| `0x022D` | `u_itiMojiSakujyo` | — | 未実装 | 未登録 |
| `0x022E` | `u_itiMojiSakujyoForJinfo` | — | 未実装 | 未登録 |
| `0x022F` | `u_itiMojiMigiSakujyoForJvar` | — | 未実装 | 未登録 |
| `0x0230` | `u_itiMojiMigiSakujyo` | — | 未実装 | 未登録 |
| `0x0231` | `u_itiMojiMigiSakujyoForJinfo` | — | 未実装 | 未登録 |
| `0x0232` | `u_itiMojiKiridasiForJvar` | — | 未実装 | 未登録 |
| `0x0233` | `u_itiMojiKiridasi` | — | 未実装 | 未登録 |
| `0x0234` | `u_itiMojiKiridasiForJinfo` | — | 未実装 | 未登録 |
| `0x0235` | `u_itiMojiMigiKiridasiForJvar` | — | 未実装 | 未登録 |
| `0x0236` | `u_itiMojiMigiKiridasi` | — | 未実装 | 未登録 |
| `0x0237` | `u_itiMojiMigiKiridasiForJinfo` | — | 未実装 | 未登録 |
| `0x0238` | `u_kiridasiForJvar` | — | 未実装 | 未登録 |
| `0x0239` | `u_kiridasi` | — | 未実装 | 未登録 |
| `0x023A` | `u_kiridasiForJinfo` | — | 未実装 | 未登録 |
| `0x023B` | `u_migiKiridasiForJvar` | — | 未実装 | 未登録 |
| `0x023C` | `u_migiKiridasi` | — | 未実装 | 未登録 |
| `0x023D` | `u_migiKiridasiForJinfo` | — | 未実装 | 未登録 |
| `0x023E` | `u_bundanForJvar` | — | 未実装 | 未登録 |
| `0x023F` | `u_bundan` | — | 未実装 | 未登録 |
| `0x0240` | `u_bundanForJinfo` | — | 未実装 | 未登録 |
| `0x0241` | `u_sakujyoForJvar` | — | 未実装 | 未登録 |
| `0x0242` | `u_sakujyo` | — | 未実装 | 未登録 |
| `0x0243` | `u_sakujyoForJinfo` | — | 未実装 | 未登録 |
| `0x0244` | `u_migiSakujyoForJvar` | — | 未実装 | 未登録 |
| `0x0245` | `u_migiSakujyo` | — | 未実装 | 未登録 |
| `0x0246` | `u_migiSakujyoForJinfo` | — | 未実装 | 未登録 |
| `0x0247` | `u_mojisu` | — | 未実装 | 未登録 |
| `0x0248` | `u_satanMoji` | — | 未実装 | 未登録 |
| `0x0249` | `u_utanMoji` | — | 未実装 | 未登録 |
| `0x024A` | `u_siteiItiNoMoji` | — | 未実装 | 未登録 |
| `0x024B` | `u_migiSiteiItiNoMoji` | — | 未実装 | 未登録 |
| `0x024C` | `u_toridasi` | — | 未実装 | 未登録 |
| `0x024D` | `u_migiToridasi` | — | 未実装 | 未登録 |
| `0x024E` | `u_itiMojiKensaku` | — | 未実装 | 未登録 |
| `0x024F` | `u_kensaku` | — | 未実装 | 未登録 |
| `0x0250` | `u_S_itiMojiSakujyo` | — | 未実装 | 未登録 |
| `0x0251` | `u_S_itiMojiMigiSakujyo` | — | 未実装 | 未登録 |
| `0x0252` | `u_S_itiMojiKiridasi` | — | 未実装 | 未登録 |
| `0x0253` | `u_S_itiMojiMigiKiridasi` | — | 未実装 | 未登録 |
| `0x0254` | `u_S_kiridasi` | — | 未実装 | 未登録 |
| `0x0255` | `u_S_migiKiridasi` | — | 未実装 | 未登録 |
| `0x0256` | `u_S_bundan` | — | 未実装 | 未登録 |
| `0x0257` | `u_S_sakujyo` | — | 未実装 | 未登録 |
| `0x0258` | `u_S_migiSakujyo` | — | 未実装 | 未登録 |
| `0x0259` | `u_S_satanMoji` | — | 未実装 | 未登録 |
| `0x025A` | `u_S_utanMoji` | — | 未実装 | 未登録 |
| `0x025B` | `u_S_siteiItiNoMoji` | — | 未実装 | 未登録 |
| `0x025C` | `u_S_migiSiteiItiNoMoji` | — | 未実装 | 未登録 |
| `0x025D` | `u_S_toridasi` | — | 未実装 | 未登録 |
| `0x025E` | `u_S_migiToridasi` | — | 未実装 | 未登録 |
| `0x025F` | `u_S_itiMojiKensaku` | — | 未実装 | 未登録 |
| `0x0260` | `u_S_kensaku` | — | 未実装 | 未登録 |
| `0x0261` | `c_mcodeFullFilename` | [CmcodeFullFilename](../CsFunctions3ker.cs#L41) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0262` | `kankyoHensuNoKensaku_C` | — | 未実装 | 未登録 |
| `0x0263` | `get_Arg_n` | [GetArgN](../CsFunctions3ker.cs#L70) | 実装済検証済 | [実引数・NULL終端・範囲外](../tests/Program.cs#L30) |
| `0x0264` | `get_Arg_c` | [GetArgC](../CsFunctions3ker.cs#L53) | 実装済検証済 | [実引数・NULL終端・範囲外](../tests/Program.cs#L30) |
| `0x0265` | `is_breaked` | — | 未実装 | 未登録 |
| `0x0266` | `is_breaked_BREAK` | — | 未実装 | 未登録 |
| `0x0267` | `suppress_ctrlC_signal` | — | 未実装 | 未登録 |
| `0x0268` | `enable_ctrlC_signal` | [EnableCtrlCSignal](../CsFunctions3ker.cs#L12) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0269` | `suppress_ctrlBREAK_signal` | — | 未実装 | 未登録 |
| `0x026A` | `c_time_or_ftime` | — | 未実装 | 未登録 |
| `0x026B` | `c_localtime` | — | 未実装 | 未登録 |
| `0x026C` | `c_ctime` | — | 未実装 | 未登録 |
| `0x026D` | `outputDevice` | [OutputDevice](../CsFunctions3ker.cs#L80) | 実装済検証済 | [stdout/stderr・CRLF・未対応ハンドル拒否](../tests/Program.cs#L31)。stdout/stderrのみ。一般ファイルハンドルは未対応 |
| `0x026E` | `itigyo_nyuryoku_0` | — | 未実装 | 未登録 |
| `0x026F` | `c_malloc` | — | 未実装 | 未登録 |
| `0x0270` | `c_free` | — | 未実装 | 未登録 |
| `0x0271` | `c_getcwd` | — | 未実装 | 未登録 |
| `0x0272` | `c_chdir` | — | 未実装 | 未登録 |
| `0x0273` | `c_errno` | — | 未実装 | 未登録 |
| `0x0274` | `c_strerror` | — | 未実装 | 未登録 |
| `0x0275` | `c_ENOENT` | — | 未実装 | 未登録 |
| `0x0276` | `c_ENOSPC` | — | 未実装 | 未登録 |
| `0x0277` | `c_ENOMEM` | — | 未実装 | 未登録 |
| `0x0278` | `c_EINTR` | — | 未実装 | 未登録 |
| `0x0279` | `c_ENAMETOOLONG` | — | 未実装 | 未登録 |
| `0x027A` | `c_create` | — | 未実装 | 未登録 |
| `0x027B` | `c_create_new` | — | 未実装 | 未登録 |
| `0x027C` | `c_open` | — | 未実装 | 未登録 |
| `0x027D` | `c_open_for_write` | — | 未実装 | 未登録 |
| `0x027E` | `c_close` | — | 未実装 | 未登録 |
| `0x027F` | `c_seek` | — | 未実装 | 未登録 |
| `0x0280` | `c_SEEK_SET` | — | 未実装 | 未登録 |
| `0x0281` | `c_SEEK_CUR` | — | 未実装 | 未登録 |
| `0x0282` | `c_SEEK_END` | — | 未実装 | 未登録 |
| `0x0283` | `c_read` | — | 未実装 | 未登録 |
| `0x0284` | `c_write` | — | 未実装 | 未登録 |
| `0x0285` | `c_IFMT` | — | 未実装 | 未登録 |
| `0x0286` | `c_IFREG` | — | 未実装 | 未登録 |
| `0x0287` | `c_IFDIR` | — | 未実装 | 未登録 |
| `0x0288` | `c_fstat` | — | 未実装 | 未登録 |
| `0x0289` | `c_stat` | — | 未実装 | 未登録 |
| `0x028A` | `c_utime` | — | 未実装 | 未登録 |
| `0x028B` | `c_unlink` | — | 未実装 | 未登録 |
| `0x028C` | `c_rename` | — | 未実装 | 未登録 |
| `0x028D` | `c_mkdir` | — | 未実装 | 未登録 |
| `0x028E` | `c_rmdir` | — | 未実装 | 未登録 |
| `0x028F` | `c_spawn` | — | 未実装 | 未登録 |
| `0x0290` | `c_gmtime` | — | 未実装 | 未登録 |
| `0x0291` | `c_mktime` | — | 未実装 | 未登録 |
| `0x0292` | `c_asctime` | — | 未実装 | 未登録 |
| `0x0293` | `c_findinfo_buffSize` | — | 未実装 | 未登録 |
| `0x0294` | `c_finddir` | — | 未実装 | 未登録 |
| `0x0295` | `c_closedir` | — | 未実装 | 未登録 |
| `0x0296` | `c_O_BINARY` | [BinaryMode](../CsFunctions3ker.cs#L17) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0297` | `c_O_TEXT` | [TextMode](../CsFunctions3ker.cs#L21) | 実装済未検証 | 命令単位の期待値照合なし |
| `0x0298` | `c_setmode` | [SetFileMode](../CsFunctions3ker.cs#L28) | 実装済未検証 | 命令単位の期待値照合なし。hello/variables通過のみ |
| `0x0299` | `c_chmod` | — | 未実装 | 未登録 |
| `0x029A` | `c_CLOCKS_PER_SEC` | — | 未実装 | 未登録 |
| `0x029B` | `c_clock` | — | 未実装 | 未登録 |
| `0x029C` | `c_sleep` | — | 未実装 | 未登録 |
| `0x029D` | `openPipes` | — | 未実装 | 未登録 |
| `0x029E` | `dup_fd` | — | 未実装 | 未登録 |
| `0x029F` | `dup_fd2` | — | 未実装 | 未登録 |
| `0x02A0` | `get_process_id` | — | 未実装 | 未登録 |
| `0x02A1` | `c_wait` | — | 未実装 | 未登録 |
| `0x02A2` | `c_STILL_ACTIVE` | — | 未実装 | 未登録 |
| `0x02A3` | `c_procstatus_2` | — | 未実装 | 未登録 |
| `0x02A4` | `c_WSAStartup` | — | 未実装 | 未登録 |
| `0x02A5` | `c_WSACleanup` | — | 未実装 | 未登録 |
| `0x02A6` | `c_gethostbyname` | — | 未実装 | 未登録 |
| `0x02A7` | `listen_or_connect_plus` | — | 未実装 | 未登録 |
| `0x02A8` | `c_accept` | — | 未実装 | 未登録 |
| `0x02A9` | `c_accept_ex` | — | 未実装 | 未登録 |
| `0x02AA` | `c_closesocket` | — | 未実装 | 未登録 |
| `0x02AB` | `c_shutdown` | — | 未実装 | 未登録 |
| `0x02AC` | `c_fd_set_size` | — | 未実装 | 未登録 |
| `0x02AD` | `c_FD_ZERO` | — | 未実装 | 未登録 |
| `0x02AE` | `c_FD_SET` | — | 未実装 | 未登録 |
| `0x02AF` | `c_FD_ISSET` | — | 未実装 | 未登録 |
| `0x02B0` | `c_select` | — | 未実装 | 未登録 |
| `0x02B1` | `c_getsockerror` | — | 未実装 | 未登録 |
| `0x02B2` | `c_read_socket` | — | 未実装 | 未登録 |
| `0x02B3` | `c_write_socket` | — | 未実装 | 未登録 |
| `0x02B4` | `c_recvfrom` | — | 未実装 | 未登録 |
| `0x02B5` | `c_sendto` | — | 未実装 | 未登録 |
| `0x02B6` | `c_load_module` | — | 未実装 | 未登録 |
| `0x02B7` | `c_load_module_only` | — | 未実装 | 未登録 |
| `0x02B8` | `c_get_funcaddr_only` | — | 未実装 | 未登録 |
| `0x02B9` | `c_free_library` | — | 未実装 | 未登録 |
| `0x02BA` | `c_API0` | — | 未実装 | 未登録 |
| `0x02BB` | `c_API1` | — | 未実装 | 未登録 |
| `0x02BC` | `c_API2` | — | 未実装 | 未登録 |
| `0x02BD` | `c_API3` | — | 未実装 | 未登録 |
| `0x02BE` | `c_API4` | — | 未実装 | 未登録 |
| `0x02BF` | `c_API5` | — | 未実装 | 未登録 |
| `0x02C0` | `c_API6` | — | 未実装 | 未登録 |
| `0x02C1` | `c_API7` | — | 未実装 | 未登録 |
| `0x02C2` | `c_API8` | — | 未実装 | 未登録 |
| `0x02C3` | `c_API9` | — | 未実装 | 未登録 |
| `0x02C4` | `c_isCRTyped` | — | 未実装 | 未登録 |
| `0x02C5` | `c_getErrorStringEx` | — | 未実装 | 未登録 |
| `0x02C6` | `c_freeErrorStringEx` | — | 未実装 | 未登録 |
| `0x02C7` | `c_readFileEx` | — | 未実装 | 未登録 |
| `0x02C8` | `c_readFixedSizeEx` | — | 未実装 | 未登録 |
| `0x02C9` | `c_readBlockEx` | — | 未実装 | 未登録 |
| `0x02CA` | `c_writeEx` | — | 未実装 | 未登録 |
| `0x02CB` | `c_writeBlockEx` | — | 未実装 | 未登録 |
| `0x02CC` | `c_closeHandle` | — | 未実装 | 未登録 |
| `0x02CD` | `lowSecurityMode` | [LowSecurityMode](../CsFunctions3ker.cs#L9) | 実装済未検証 | 状態切替えのみ。OS操作の検証ではない |
| `0x02CE` | `hiSecurityMode` | [HighSecurityMode](../CsFunctions3ker.cs#L10) | 実装済未検証 | 状態切替えのみ。OS操作の検証ではない |
| `0x02CF` | `c_createAnonymousPipe` | — | 未実装 | 未登録 |
| `0x02D0` | `c_CREATE_NEW_CONSOLE` | — | 未実装 | 未登録 |
| `0x02D1` | `c_DETACHED_PROCESS` | — | 未実装 | 未登録 |
| `0x02D2` | `c_HIGH_PRIORITY_CLASS` | — | 未実装 | 未登録 |
| `0x02D3` | `c_IDLE_PRIORITY_CLASS` | — | 未実装 | 未登録 |
| `0x02D4` | `c_NORMAL_PRIORITY_CLASS` | — | 未実装 | 未登録 |
| `0x02D5` | `c_REALTIME_PRIORITY_CLASS` | — | 未実装 | 未登録 |
| `0x02D6` | `c_CreateProcess` | — | 未実装 | 未登録 |
| `0x02D7` | `c_CreateFile` | — | 未実装 | 未登録 |
| `0x02D8` | `c_CreateFileMapping` | — | 未実装 | 未登録 |
| `0x02D9` | `c_OpenFileMapping` | — | 未実装 | 未登録 |
| `0x02DA` | `c_MapViewOfFile` | — | 未実装 | 未登録 |
| `0x02DB` | `c_UnmapViewOfFile` | — | 未実装 | 未登録 |
| `0x02DC` | `c_CreateEvent` | — | 未実装 | 未登録 |
| `0x02DD` | `c_CreateAnonymousEvent` | — | 未実装 | 未登録 |
| `0x02DE` | `c_OpenEvent` | — | 未実装 | 未登録 |
| `0x02DF` | `c_SetEvent` | — | 未実装 | 未登録 |
| `0x02E0` | `c_CreateMutex` | — | 未実装 | 未登録 |
| `0x02E1` | `c_CreateMutexForce` | — | 未実装 | 未登録 |
| `0x02E2` | `c_OpenMutex` | — | 未実装 | 未登録 |
| `0x02E3` | `c_ReleaseMutex` | — | 未実装 | 未登録 |
| `0x02E4` | `c_WaitForSingleObject` | — | 未実装 | 未登録 |
| `0x02E5` | `c_WaitForMultipleObject` | — | 未実装 | 未登録 |
| `0x02E6` | `c_INFINITE` | — | 未実装 | 未登録 |
| `0x02E7` | `c_WAIT_OBJECT_0` | — | 未実装 | 未登録 |
| `0x02E8` | `c_WAIT_ABANDONED_0` | — | 未実装 | 未登録 |
| `0x02E9` | `c_WAIT_TIMEOUT` | — | 未実装 | 未登録 |
| `0x02EA` | `c_HKEY_CLASSES_ROOT` | — | 未実装 | 未登録 |
| `0x02EB` | `c_HKEY_CURRENT_USER` | — | 未実装 | 未登録 |
| `0x02EC` | `c_HKEY_LOCAL_MACHINE` | — | 未実装 | 未登録 |
| `0x02ED` | `c_HKEY_USERS` | — | 未実装 | 未登録 |
| `0x02EE` | `c_get_registry_value` | — | 未実装 | 未登録 |
| `0x02EF` | `famari` | — | 未実装 | 未登録 |
| `0x02F0` | `other_dummy_02` | — | 未実装 | 未登録（正規表の予約ダミー） |
| `0x02F1` | `other_dummy_03` | — | 未実装 | 未登録（正規表の予約ダミー） |
| `0x02F2` | `other_dummy_04` | — | 未実装 | 未登録（正規表の予約ダミー） |
| `0x02F3` | `other_dummy_05` | — | 未実装 | 未登録（正規表の予約ダミー） |
| `0x02F4` | `other_dummy_06` | — | 未実装 | 未登録（正規表の予約ダミー） |
| `0x02F5` | `other_dummy_07` | — | 未実装 | 未登録（正規表の予約ダミー） |
| `0x02F6` | `other_dummy_08` | — | 未実装 | 未登録（正規表の予約ダミー） |
