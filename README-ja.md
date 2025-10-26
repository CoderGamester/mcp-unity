# MCP Unity Editor（ゲームエンジン）

[![](https://badge.mcpx.dev?status=on 'MCP Enabled')](https://modelcontextprotocol.io/introduction)
[![](https://img.shields.io/badge/Unity-000000?style=flat&logo=unity&logoColor=white 'Unity')](https://unity.com/releases/editor/archive)
[![](https://img.shields.io/badge/Node.js-339933?style=flat&logo=nodedotjs&logoColor=white 'Node.js')](https://nodejs.org/en/download/)
[![](https://img.shields.io/github/stars/CoderGamester/mcp-unity 'Stars')](https://github.com/CoderGamester/mcp-unity/stargazers)
[![](https://img.shields.io/github/last-commit/CoderGamester/mcp-unity 'Last Commit')](https://github.com/CoderGamester/mcp-unity/commits/main)
[![](https://img.shields.io/badge/License-MIT-red.svg 'MIT License')](https://opensource.org/licenses/MIT)

| [🇺🇸英語](README.md) | [🇨🇳簡体中文](README_zh-CN.md) | [🇯🇵日本語](README-ja.md) |
|----------------------|---------------------------------|----------------------|

```                                                                        
                              ,/(/.   *(/,                                  
                          */(((((/.   *((((((*.                             
                     .*((((((((((/.   *((((((((((/.                         
                 ./((((((((((((((/    *((((((((((((((/,                     
             ,/(((((((((((((/*.           */(((((((((((((/*.                
            ,%%#((/((((((*                    ,/(((((/(#&@@(                
            ,%%##%%##((((((/*.             ,/((((/(#&@@@@@@(                
            ,%%######%%##((/(((/*.    .*/(((//(%@@@@@@@@@@@(                
            ,%%####%#(%%#%%##((/((((((((//#&@@@@@@&@@@@@@@@(                
            ,%%####%(    /#%#%%%##(//(#@@@@@@@%,   #@@@@@@@(                
            ,%%####%(        *#%###%@@@@@@(        #@@@@@@@(                
            ,%%####%(           #%#%@@@@,          #@@@@@@@(                
            ,%%##%%%(           #%#%@@@@,          #@@@@@@@(                
            ,%%%#*              #%#%@@@@,             *%@@@(                
            .,      ,/##*.      #%#%@@@@,     ./&@#*      *`                
                ,/#%#####%%#/,  #%#%@@@@, ,/&@@@@@@@@@&\.                    
                 `*#########%%%%###%@@@@@@@@@@@@@@@@@@&*´                   
                    `*%%###########%@@@@@@@@@@@@@@&*´                        
                        `*%%%######%@@@@@@@@@@&*´                            
                            `*#%%##%@@@@@&*´                                 
                               `*%#%@&*´                                     
                                                        
     ███╗   ███╗ ██████╗██████╗         ██╗   ██╗███╗   ██╗██╗████████╗██╗   ██╗
     ████╗ ████║██╔════╝██╔══██╗        ██║   ██║████╗  ██║██║╚══██╔══╝╚██╗ ██╔╝
     ██╔████╔██║██║     ██████╔╝        ██║   ██║██╔██╗ ██║██║   ██║    ╚████╔╝ 
     ██║╚██╔╝██║██║     ██╔═══╝         ██║   ██║██║╚██╗██║██║   ██║     ╚██╔╝  
     ██║ ╚═╝ ██║╚██████╗██║             ╚██████╔╝██║ ╚████║██║   ██║      ██║   
     ╚═╝     ╚═╝ ╚═════╝╚═╝              ╚═════╝ ╚═╝  ╚═══╝╚═╝   ╚═╝      ╚═╝   
```       

MCP Unityは、Unityエディター向けのModel Context Protocolの実装であり、AIアシスタントがUnityプロジェクトと対話できるようにします。このパッケージは、UnityとMCPプロトコルを実装するNode.jsサーバー間のブリッジを提供し、Claude、Windsurf、CursorなどのAIエージェントがUnityエディター内で操作を実行できるようにします。

<a href="https://glama.ai/mcp/servers/@CoderGamester/mcp-unity">
  <img width="400" height="200" src="https://glama.ai/mcp/servers/@CoderGamester/mcp-unity/badge" alt="Unity MCPサーバー" />
</a>

## 機能

### IDE統合 - パッケージキャッシュアクセス

MCP Unityは、Unityの`Library/PackedCache`フォルダーをワークスペースに追加することで、VSCode系IDE（Visual Studio Code、Cursor、Windsurf）との自動統合を提供します。この機能により：

- Unityパッケージのコードインテリジェンスが向上
- Unityパッケージのより良いオートコンプリートと型情報が有効化
- AIコーディングアシスタントがプロジェクトの依存関係を理解するのに役立つ

### MCPサーバーツール

- `execute_menu_item`: Unityメニュー項目（MenuItem属性でタグ付けされた関数）を実行
  > **例:** "新しい空のGameObjectを作成するためにメニュー項目'GameObject/Create Empty'を実行"

- `select_gameobject`: パスまたはインスタンスIDでUnity階層内のゲームオブジェクトを選択
  > **例:** "シーン内のMain Cameraオブジェクトを選択"

- `update_gameobject`: GameObject のコアプロパティ（名前、タグ、レイヤー、アクティブ/静的状態）を更新、または存在しない場合は作成します
  > **例:** "Playerオブジェクトのタグを ‘Enemy’ に設定し、非アクティブにする"

- `update_component`: GameObject上のコンポーネントフィールドを更新、またはGameObjectに含まれていない場合は追加
  > **例:** "PlayerオブジェクトにRigidbodyコンポーネントを追加し、その質量を5に設定"

- `add_package`: Unityパッケージマネージャーに新しいパッケージをインストール
  > **例:** "プロジェクトにTextMeshProパッケージを追加"

- `run_tests`: Unityテストランナーを使用してテストを実行
  > **例:** "プロジェクト内のすべてのEditModeテストを実行"

- `send_console_log`: Unityにコンソールログを送信
  > **例:** "Unity Editorにコンソールログを送信"

- `add_asset_to_scene`: AssetDatabaseからアセットをUnityシーンに追加
  > **例:** "プロジェクトからPlayerプレハブを現在のシーンに追加"

- `create_prefab`: プレハブを作成し、オプションでMonoBehaviourスクリプトとシリアライズされたフィールド値を設定
  > **例:** "'PlayerController'スクリプトから'Player'という名前のプレハブを作成"

- `recompile_scripts`: Unityプロジェクト内のすべてのスクリプトを再コンパイル
  > **例:** "Unityプロジェクト内のすべてのスクリプトを再コンパイル"

### MCPサーバーリソース

- `unity://menu-items`: `execute_menu_item`ツールを容易にするために、Unityエディターで利用可能なすべてのメニュー項目のリストを取得
  > **例:** "GameObject作成に関連する利用可能なすべてのメニュー項目を表示"

- `unity://scenes-hierarchy`: 現在のUnityシーン階層内のすべてのゲームオブジェクトのリストを取得
  > **例:** "現在のシーン階層構造を表示"

- `unity://gameobject/{id}`: シーン階層内のインスタンスIDまたはオブジェクトパスで特定のGameObjectに関する詳細情報を取得
  > **例:** "Player GameObjectに関する詳細情報を取得"

- `unity://logs`: Unityコンソールからのすべてのログのリストを取得
  > **例:** "Unityコンソールからの最近のエラーメッセージを表示"

- `unity://packages`: Unityパッケージマネージャーからインストール済みおよび利用可能なパッケージ情報を取得
  > **例:** "プロジェクトに現在インストールされているすべてのパッケージをリスト"

- `unity://assets`: Unityアセットデータベース内のアセット情報を取得
  > **例:** "プロジェクト内のすべてのテクスチャアセットを検索"

- `unity://tests/{testMode}`: Unityテストランナー内のテスト情報を取得
  > **例:** "プロジェクトで利用可能なすべてのテストをリスト"

## 要件
- Unity 2022.3以降 - [サーバーをインストール](#install-server)するため
- Node.js 18以降 - [サーバーを起動](#start-server)するため
- npm 9以降 - [サーバーをデバッグ](#debug-server)するため

> [!IMPORTANT]
> **プロジェクトパスにスペースを含めることはできません**
>
> Unity プロジェクトのファイルパスに**スペースを含めない**ことが非常に重要です。
> プロジェクトパスにスペースが含まれている場合、MCP クライアント（例：Cursor、Claude、Windsurf）は MCP Unity サーバーに接続できません。
>
> **例：**
> -   ✅ **動作します:** `C:\Users\YourUser\Documents\UnityProjects\MyAwesomeGame`
> -   ❌ **失敗します:** `C:\Users\Your User\Documents\Unity Projects\My Awesome Game`
>
> インストールを進める前に、プロジェクトがスペースを含まないパスにあることを確認してください。

## <a name="install-server"></a>インストール

このMCP Unityサーバーのインストールは複数ステップのプロセスです：

### ステップ1: Node.jsをインストール
> MCP Unityサーバーを実行するには、コンピューターにNode.js 18以降がインストールされている必要があります：

![node](docs/node.jpg)

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Windows</span></summary>

1. [Node.jsダウンロードページ](https://nodejs.org/en/download/)にアクセス
2. LTSバージョンのWindowsインストーラー（.msi）をダウンロード（推奨）
3. インストーラーを実行し、インストールウィザードに従う
4. PowerShellを開いて以下を実行してインストールを確認：
   ```bash
   node --version
   ```
</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">macOS</span></summary>

1. [Node.jsダウンロードページ](https://nodejs.org/en/download/)にアクセス
2. LTSバージョンのmacOSインストーラー（.pkg）をダウンロード（推奨）
3. インストーラーを実行し、インストールウィザードに従う
4. または、Homebrewがインストールされている場合は以下を実行：
   ```bash
   brew install node@18
   ```
5. ターミナルを開いて以下を実行してインストールを確認：
   ```bash
   node --version
   ```
</details>

### ステップ2: Unityパッケージマネージャー経由でUnity MCPサーバーパッケージをインストール
1. Unityパッケージマネージャーを開く（Window > Package Manager）
2. 左上隅の"+"ボタンをクリック
3. "Add package from git URL..."を選択
4. 入力: `https://github.com/CoderGamester/mcp-unity.git`
5. "Add"をクリック

![package manager](https://github.com/user-attachments/assets/a72bfca4-ae52-48e7-a876-e99c701b0497)

### ステップ3: AI LLMクライアントを設定

<details open>
<summary><span style="font-size: 1.1em; font-weight: bold;">オプション1: Unityエディターを使用して設定</span></summary>

1. Unityエディターを開く
2. Tools > MCP Unity > Server Windowに移動
3. 以下の画像のようにAI LLMクライアントの"Configure"ボタンをクリック

![image](docs/configure.jpg)

4. 表示されるポップアップで設定インストールを確認

![image](https://github.com/user-attachments/assets/b1f05d33-3694-4256-a57b-8556005021ba)

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">オプション2: 手動設定</span></summary>

AIクライアントのMCP設定ファイル（例：Claude Desktopのclaude_desktop_config.json）を開き、以下のテキストをコピー：

> `ABSOLUTE/PATH/TO`をMCP Unityインストールの絶対パスに置き換えるか、UnityエディターMCPサーバーウィンドウ（Tools > MCP Unity > Server Window）からテキストをコピー

```json
{
  "mcpServers": {
    "mcp-unity": {
      "command": "node",
      "args": [
        "ABSOLUTE/PATH/TO/mcp-unity/Server~/build/index.js"
      ]
    }
  }
}
```

</details>

## <a name="start-server"></a>サーバーの起動

1. Unityエディターを開く
2. Tools > MCP Unity > Server Window に移動
3. "Start Server" をクリックして WebSocket サーバーを起動
4. Claude Desktop または AI コーディング IDE（例：Cursor IDE、Windsurf IDE など）を開き、Unity ツールの実行を開始
   
![connect](https://github.com/user-attachments/assets/2e266a8b-8ba3-4902-b585-b220b11ab9a2)

> AI クライアントが WebSocket サーバーに接続すると、ウィンドウの緑色のボックスに自動的に表示されます

## オプション：WebSocket ポートを設定
デフォルトでは、WebSocket サーバーは '8090' ポートで動作します。次の手順でポートを変更できます：

1. Unityエディターを開く
2. Tools > MCP Unity > Server Window に移動
3. "WebSocket Port" の値を希望のポート番号に変更
4. Unity はシステム環境変数 UNITY_PORT を新しいポート番号に設定
5. Node.js サーバーを再起動
6. 再度 "Start Server" をクリックして、Unity Editor の WebSocket を Node.js MCP サーバーに再接続

## オプション: タイムアウト設定

デフォルトでは、MCPサーバーとWebSocket間のタイムアウトは 10 秒です。
お使いの環境に応じて以下の手順で変更できます：

1. Unityエディターを開く  
2. **Tools > MCP Unity > Server Window** に移動  
3. **Request Timeout (seconds)** の値を希望のタイムアウト秒数に変更  
4. Unityが環境変数 `UNITY_REQUEST_TIMEOUT` に新しい値を設定  
5. Node.jsサーバーを再起動  
6. **Start Server** を再度クリックして再接続  

> [!TIP]  
> AIコーディングIDE（Claude Desktop、Cursor IDE、Windsurf IDEなど）とMCPサーバー間のタイムアウトは、使用するIDEによって異なる場合があります。

## オプション：リモート MCP ブリッジ接続を許可する

デフォルトでは、WebSocket サーバーは 'localhost' にバインドされています。他のマシンから MCP ブリッジ接続を許可するには、以下の手順に従ってください：

1. Unity エディターを開く  
2. メニューから「Tools > MCP Unity > Server Window」を選択  
3. 「Allow Remote Connections（リモート接続を許可）」チェックボックスを有効にする  
4. Unity は WebSocket サーバーを '0.0.0.0'（すべてのインターフェース）にバインドします  
5. Node.js サーバーを再起動して新しいホスト設定を適用する  
6. リモートで MCP ブリッジを実行する場合は、環境変数 UNITY_HOST を Unity 実行マシンの IP アドレスに設定して起動：  
   `UNITY_HOST=192.168.1.100 node server.js`

## <a name="debug-server"></a>サーバーのデバッグ

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Node.js サーバーのビルド</span></summary>

MCP Unity サーバーは Node.js で構築されています。TypeScript コードを `build` ディレクトリにコンパイルする必要があります。問題がある場合は、以下の手順で強制的にインストールできます：

1. Unityエディターを開く
2. Tools > MCP Unity > Server Window に移動
3. 「Force Install Server」ボタンをクリック

![install](docs/install.jpg)

手動でビルドする場合は、以下の手順に従ってください：

1. ターミナル/PowerShell/コマンドプロンプトを開く
2. Server ディレクトリに移動：
   ```bash
   cd ABSOLUTE/PATH/TO/mcp-unity/Server~
   ```
3. 依存関係をインストール：
   ```bash
   npm install
   ```
4. サーバーをビルド：
   ```bash
   npm run build
   ```
5. サーバーを実行：
   ```bash
   node build/index.js
   ```

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">MCP Inspector でデバッグ</span></summary>

[@modelcontextprotocol/inspector](https://github.com/modelcontextprotocol/inspector) を使用してサーバーをデバッグします：
   - Powershell
   ```powershell
   npx @modelcontextprotocol/inspector node Server~/build/index.js
   ```
   - コマンドプロンプト/ターミナル
   ```cmd
   npx @modelcontextprotocol/inspector node Server~/build/index.js
   ```

ターミナルを閉じる前、または [MCP Inspector](https://github.com/modelcontextprotocol/inspector) でデバッグする前に、必ず `Ctrl + C` でサーバーを終了してください。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">コンソールログを有効化</span></summary>

1. ターミナルまたは log.txt ファイルにログ出力を有効化：
   - Powershell
   ```powershell
   $env:LOGGING = "true"
   $env:LOGGING_FILE = "true"
   ```
   - コマンドプロンプト/ターミナル
   ```cmd
   set LOGGING=true
   set LOGGING_FILE=true
   ```

</details>

## よくある質問

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">MCP Unityとは何ですか？</span></summary>

MCP Unityは、Model Context Protocol（MCP）を使用して、Unityエディター環境をAIアシスタントLLMツールに接続する強力なブリッジです。

本質的に、MCP Unityは次のことを行います。
-   Unityエディターの機能（オブジェクトの作成、コンポーネントの変更、テストの実行など）を、AIが理解して使用できる「ツール」および「リソース」として公開します。
-   Unity内にWebSocketサーバーを、そしてMCPを実装するNode.jsサーバー（UnityへのWebSocketクライアントとして機能）を実行します。これにより、AIアシスタントはUnityにコマンドを送信し、情報を受け取ることができます。
-   AIアシスタントとの自然言語プロンプトを使用して、Unityプロジェクト内で複雑なタスクを実行できるようにし、開発ワークフローを大幅に加速します。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">MCP Unityを使用する理由は何ですか？</span></summary>

MCP Unityは、開発者、アーティスト、プロジェクトマネージャーにいくつかの魅力的な利点を提供します。

-   **開発の加速:** AIプロンプトを使用して、反復的なタスクを自動化し、ボイラープレートコードを生成し、アセットを管理します。これにより、創造的で複雑な問題解決に集中する時間を確保できます。
-   **生産性の向上:** メニューを手動でクリックしたり、簡単な操作のためにスクリプトを作成したりすることなく、Unityエディターの機能と対話できます。AIアシスタントは、Unity内でのあなたの能力を直接拡張します。
-   **アクセシビリティの向上:** UnityエディターやC#スクリプトの深い複雑さにあまり慣れていないユーザーでも、AIのガイダンスを通じてプロジェクトに意味のある貢献や変更を行うことができます。
-   **シームレスな統合:** MCPをサポートするさまざまなAIアシスタントやIDEと連携するように設計されており、開発ツールキット全体でAIを活用する一貫した方法を提供します。
-   **拡張性:** プロトコルとツールセットは拡張可能です。プロジェクト固有またはUnityの機能をAIに公開するための新しいツールとリソースを定義できます。
-   **コラボレーションの可能性:** AIがチームメンバーが従来行っていたタスクを支援したり、プロジェクトの構造や操作をガイドすることで新しい開発者のオンボーディングを支援したりする新しいコラボレーション方法を促進します。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">MCP Unityは、今後登場するUnity 6.2のAI機能と比較してどうですか？</span></summary>

Unity 6.2では、以前のUnity Muse（テクスチャやアニメーション生成などの生成AI機能用）やUnity Sentis（Unityランタイムでニューラルネットワークを実行するため）を含む、新しい組み込みAIツールが導入される予定です。Unity 6.2はまだ完全にリリースされていないため、この比較は公開されている情報と予想される機能に基づいています。

-   **焦点:**
    -   **MCP Unity:** 主に**エディターの自動化と対話**に焦点を当てています。これにより、外部AI（LLMベースのコーディングアシスタントなど）がUnityエディター自体を*制御およびクエリ*して、シーン、アセット、プロジェクト設定を操作できます。これは、エディター内での*開発者のワークフロー*を強化することです。
    -   **Unity 6.2 AI:**
        -   エディター内でのコンテンツ作成（テクスチャ、スプライト、アニメーション、動作、スクリプトの生成）と、Unityエディターインターフェースに直接統合された一般的なタスクのAI支援を目的としています。
        -   UnityのドキュメントとAPI構造に関するあらゆる質問に、Unity環境により正確なカスタマイズされた例で答えるための微調整されたモデル。
        -   AIモデルの推論を実行する機能を追加し、NPCの動作、画像認識などの機能のために、事前にトレーニングされたニューラルネットワークを*ゲームまたはアプリケーション内*にデプロイして実行できるようにします。

-   **ユースケース:**
    -   **MCP Unity:** 「新しい3Dオブジェクトを作成し、名前を「Player」にし、Rigidbodyを追加して、質量を10に設定してください。」「すべてのプレイモードテストを実行してください。」「コンソールログのエラーを修正するよう依頼してください。」「カスタムメニュー項目「iOS用のビルドを準備」を実行し、発生する可能性のあるエラーを修正してください。」
    -   **Unity 6.2 AI:** 「このマテリアルにSFテクスチャを生成してください。」「シーン内のすべてのツリーの位置を「forest」とタグ付けされたテレインゾーン内に配置するように更新してください。」「このキャラクターのアニメーションを作成してください。」「キャラクターを完成させるために2Dスプライトを生成してください。」「コンソールログのエラーの詳細を尋ねてください。」

-   **補完的であり、相互排他的ではない:**
    MCP UnityとUnityのネイティブAIツールは補完的であると見なすことができます。AIコーディングアシスタントでMCP Unityを使用してシーンを設定したり、アセットを一括変更したりしてから、Unity AIツールを使用して特定のテクスチャを生成したり、アニメーションや2Dスライトをそれらのアセットの1つに作成したりすることができます。MCP Unityは、より広範な外部AIサービスと統合したり、カスタム自動化ワークフローを構築したりしたい開発者にとって強力な、プロトコルベースのエディターとの対話方法を提供します。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">現在、どのMCPホストとIDEがMCP Unityをサポートしていますか？</span></summary>

MCP Unityは、MCPクライアントとして機能できるAIアシスタントまたは開発環境と連携するように設計されています。エコシステムは成長していますが、現在の既知の統合または互換性のあるプラットフォームには以下が含まれます。
-  Windsurf
-  Cursor
-  GitHub Copilot
-  Claude Desktop

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">プロジェクトにカスタムツールでMCP Unityを拡張できますか？</span></summary>

はい、もちろんです！MCP Unityアーキテクチャの重要な利点の1つは、その拡張性です。
-   **Unity内（C#）:** `McpToolBase`（またはリソースの同様のベース）を継承する新しいC#クラスを作成して、カスタムUnityエディター機能を公開できます。これらのツールは、`McpUnityServer.cs`に登録されます。たとえば、プロジェクト固有のアセットインポートパイプラインを自動化するツールを作成できます。
-   **Node.jsサーバー内（TypeScript）:** 次に、`Server/src/tools/`ディレクトリに対応するTypeScriptツールハンドラーを定義し、入力/出力のZodスキーマを含め、`Server/src/index.ts`に登録します。このNode.js部分は、新しいC#ツールへのリクエストをUnityに転送します。

これにより、AIの機能をゲームやアプリケーションの特定のニーズとワークフローに合わせて調整できます。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">MCP Unityは無料で利用できますか？</span></summary>

はい、MCP UnityはMITライセンスの下で配布されているオープンソースプロジェクトです。ライセンス条項に従って、自由に利用、変更、配布できます。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">MCP Unityに接続できないのはなぜですか？</span></summary>

- WebSocket サーバーが起動していることを確認（Unity の Server Window を確認）
- MCP クライアントからコンソールログを 1 件送信して、MCP クライアントと Unity サーバー間の再接続をトリガー
- Unity Editor の MCP Server ウィンドウ（Tools > MCP Unity > Server Window）でポート番号を変更

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">MCP Unity サーバーが起動しないのはなぜですか？</span></summary>

- Unity コンソールにエラーメッセージがないか確認
- Node.js が正しくインストールされ、PATH から実行できることを確認
- Server ディレクトリで依存関係がインストールされていることを確認

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Play Mode テストを実行すると接続失敗エラーが発生するのはなぜですか？</span></summary>

`run_tests` ツールは次のレスポンスを返すことがあります：
```
Error:
Connection failed: Unknown error
```

これは、Play Mode に切り替える際のドメインリロードでブリッジ接続が失われるために発生します。回避策として、**Edit > Project Settings > Editor > "Enter Play Mode Settings"** で **Reload Domain** をオフにしてください。

</details>

## トラブルシューティング：WSL2（Windows 11）のネットワーク

WSL2 内で MCP（Node.js）サーバーを実行し、Unity が Windows 11 上で動作している場合、`ws://localhost:8090/McpUnity` への接続が `ECONNREFUSED` で失敗することがあります。

原因：WSL2 と Windows は別々のネットワーク名前空間を持ち、WSL2 内の `localhost` は Windows ホストを指しません。既定では Unity は `localhost:8090` で待ち受けます。

### 解決策 1 — WSL2 のミラー化ネットワークを有効化（推奨）
- Windows 11: 設定 → システム → 開発者向け → WSL → 「ミラー化モードのネットワーク」を有効化。
- または `.wslconfig` で設定（適用後に `wsl --shutdown` を実行して WSL を再起動）:

```ini
[wsl2]
networkingMode=mirrored
```

有効化後は Windows と WSL2 で `localhost` が共有され、既定設定（`localhost:8090`）で動作します。

### 解決策 2 — Node クライアントを Windows ホストに向ける
MCP クライアントを起動する前に、WSL シェルで以下を設定します：

```bash
# resolv.conf から検出した Windows ホスト IP を使用
export UNITY_HOST=$(grep -m1 nameserver /etc/resolv.conf | awk '{print $2}')
```

これにより、`Server~/src/unity/mcpUnity.ts` は `localhost` の代わりに `ws://$UNITY_HOST:8090/McpUnity` に接続します（`UNITY_HOST` を参照し、必要に応じて `ProjectSettings/McpUnitySettings.json` の `Host` も使用されます）。

### 解決策 3 — Unity からのリモート接続を許可
- Unity: Tools → MCP Unity → Server Window → 「Allow Remote Connections」を有効化（`0.0.0.0` にバインド）。
- Windows ファイアウォールで、設定したポート（既定 8090）への受信 TCP を許可します。
- WSL2 からは、Windows ホストの IP（解決策 2 を参照）またはミラー化有効時は `localhost` へ接続します。

> [!NOTE]
> 既定のポートは `8090` です。Unity の Server Window（Tools → MCP Unity → Server Window）で変更できます。値は `McpUnitySettings` に対応し、`ProjectSettings/McpUnitySettings.json` に保存されます。

#### 接続確認

```bash
npm i -g wscat
# ミラー化ネットワーク有効化後
wscat -c ws://localhost:8090/McpUnity
# または Windows ホスト IP を使用
wscat -c ws://$UNITY_HOST:8090/McpUnity
```

## サポート & フィードバック

ご質問やサポートが必要な場合は、このリポジトリの[Issue](https://github.com/CoderGamester/mcp-unity/issues)を開くか、以下までご連絡ください：
- LinkedIn: [![](https://img.shields.io/badge/LinkedIn-0077B5?style=flat&logo=linkedin&logoColor=white 'LinkedIn')](https://www.linkedin.com/in/miguel-tomas/)
- Discord: gamester7178
- Email: game.gamester@gmail.com

## 貢献

貢献は大歓迎です！プルリクエストを送信するか、Issueを開いてください。

**コミットメッセージ**は[Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/)形式に従ってください。

## ライセンス

本プロジェクトは[MIT License](LICENSE.md)の下で提供されます。

## 謝辞

- [Model Context Protocol](https://modelcontextprotocol.io)
- [Unity Technologies](https://unity.com)
- [Node.js](https://nodejs.org)
- [WebSocket-Sharp](https://github.com/sta/websocket-sharp)
