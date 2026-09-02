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

- `create_scene`: 新しいシーンを作成し、指定されたパスに保存
  > **例:** "Scenesフォルダに'Level1'という新しいシーンを作成"

- `load_scene`: パスまたは名前でシーンをロード（オプションで追加ロード可能）
  > **例:** "MainMenuシーンをロード"

- `delete_scene`: パスまたは名前でシーンを削除し、ビルド設定から削除
  > **例:** "プロジェクトから古いTestSceneを削除"

- `get_gameobject`: すべてのコンポーネントを含む特定のGameObjectの詳細情報を取得
  > **例:** "Player GameObjectの詳細を取得"

- `get_console_logs`: ページネーションをサポートしてUnityコンソールからログを取得
  > **例:** "Unityコンソールから最新の20件のエラーログを表示"

- `save_scene`: 現在のアクティブなシーンを保存（オプションで別名保存可能）
  > **例:** "現在のシーンを保存" または "シーンを'Assets/Scenes/Level2.unity'として保存"

- `get_scene_info`: 名前、パス、ダーティ状態、ロードされたすべてのシーンを含むアクティブなシーンの情報を取得
  > **例:** "プロジェクトで現在ロードされているシーンは？"

- `unload_scene`: 階層からシーンをアンロード（シーンアセットは削除しない）
  > **例:** "階層からUIシーンをアンロード"

- `duplicate_gameobject`: シーン内のGameObjectを複製（オプションで名前変更や親の再設定が可能）
  > **例:** "Enemyプレハブを5回複製し、Enemy_1からEnemy_5に名前を変更"

- `delete_gameobject`: シーンからGameObjectを削除
  > **例:** "シーンから古いPlayerオブジェクトを削除"

- `reparent_gameobject`: 階層内のGameObjectの親を変更
  > **例:** "HealthBarオブジェクトをUI Canvasの子に移動"

- `move_gameobject`: GameObjectを新しい位置に移動（ローカルまたはワールド空間）
  > **例:** "Playerオブジェクトをワールド空間の位置(10, 0, 5)に移動"

- `rotate_gameobject`: GameObjectを新しい回転に変更（ローカルまたはワールド空間、オイラー角またはクォータニオン）
  > **例:** "CameraをY軸に45度回転"

- `scale_gameobject`: GameObjectを新しいローカルスケールに変更
  > **例:** "Enemyオブジェクトを2倍のサイズに拡大"

- `set_transform`: 単一の操作でGameObjectの位置、回転、スケールを設定
  > **例:** "Cubeの位置を(0, 5, 0)、回転を(0, 90, 0)、スケールを(2, 2, 2)に設定"

- `create_material`: 指定されたシェーダーで新しいマテリアルを作成し、プロジェクトに保存
  > **例:** "URP Litシェーダーを使用して'EnemyMaterial'という赤いマテリアルを作成"

- `assign_material`: GameObjectのRendererコンポーネントにマテリアルを割り当て
  > **例:** "'EnemyMaterial'をEnemy GameObjectに割り当て"

- `modify_material`: 既存のマテリアルのプロパティ（色、浮動小数点数、テクスチャ）を変更
  > **例:** "'EnemyMaterial'の色を青に変更し、メタリックを0.8に設定"

- `get_material_info`: シェーダーとすべてのプロパティを含むマテリアルの詳細情報を取得
  > **例:** "'PlayerMaterial'のすべてのプロパティを表示"

- `batch_execute`: 単一のバッチリクエストで複数のツール操作を実行し、ラウンドトリップを削減しアトミック操作を可能にする（失敗時のロールバックオプション付き）
  > **例:** "単一のバッチ操作でEnemy_1からEnemy_10という名前の10個の空のGameObjectを作成"

### MCP App ツール

- `show_unity_dashboard`: VS Code で Unity ダッシュボード MCP App を開く（VS Code 1.109 以降が必要）
  > **例:** "Unity ダッシュボードアプリを開いて"

- `get_play_mode_status`: Unity のプレイモード状態（isPlaying、isPaused）を取得
  > **例:** "Unity はプレイモードですか？"

- `set_play_mode_status`: `play`（開始または再開）、`pause`（一時停止の切替）、`stop`（プレイモードの終了）、`step`（1 フレーム進める）で Unity のプレイモードを操作
  > **例:** "Unity のプレイモードを開始"、"ゲームを一時停止"、または "1 フレーム進めて"

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

- `ui://unity-dashboard`: Unity ダッシュボード MCP App UI
  > **例:** "Unity ダッシュボードアプリを開いて"

### MCP サーバープロンプト

プロンプトは、一般的な Unity タスクのガイド付きワークフローを提供する事前設定済みテンプレートです。特定のシナリオにおける適切な操作順序と利用可能なツールを AI アシスタントが理解するのに役立ちます。

- `unity_dashboard`: 機能に関するコンテキスト情報とともに Unity ダッシュボード MCP App を開く
  > **使い方:** AI アシスタントで `unity_dashboard` プロンプトを使用すると、Unity ダッシュボードへのガイド付きアクセスを得られます

- `gameobject_handling_strategy`: 使用するツールとリソースを含む、GameObject 操作の構造化されたワークフローを提供
  > **使い方:** AI アシスタントで GameObject の ID、名前、またはパスとともに `gameobject_handling_strategy` プロンプトを使用すると、段階的なガイダンスを得られます

## 要件
- Unity 6以降 - [サーバーをインストール](#install-server)するため
- Node.js 18以降 - [サーバーを起動](#start-server)するため
- npm 9以降 - [サーバーをデバッグ](#debug-server)するため

> [!NOTE]
> **スペースを含むプロジェクトパス**
>
> MCP Unity はスペースを含むプロジェクトパスに対応しています。ただし接続の問題が発生した場合は、トラブルシューティングとしてスペースを含まないパスへプロジェクトを移動してみてください。
>
> **例：**
> -   ✅ **推奨:** `C:\Users\YourUser\Documents\UnityProjects\MyAwesomeGame`
> -   ✅ **対応済み:** `C:\Users\Your User\Documents\Unity Projects\My Awesome Game`

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

> **グローバル設定とプロジェクト設定:**
> - **Configure \[Client\]** — グローバルユーザー設定ファイル（例: `~/.claude.json`）へ書き込みます。絶対パスを使用し、マシン上のすべてのプロジェクトに適用されます。個人または単一開発者のセットアップに適しています。
> - **Configure \[Client\] (Project)** — Unity プロジェクトルートの `.mcp.json`（または相当するファイル）へ書き込みます。相対パスを使用するため、マシン間で動作します。Git にコミットしてチームと共有することを想定しており、共同プロジェクトや設定をプロジェクトと一緒に移動したい場合に適しています。
>
> 迷った場合は **(Project)** バリアントを選んでください。相対パスの方が可搬性が高く、プロジェクトフォルダを移動しても壊れません。

4. 表示されるポップアップで設定インストールを確認

![image](https://github.com/user-attachments/assets/b1f05d33-3694-4256-a57b-8556005021ba)

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">オプション2: 手動設定</span></summary>

AI クライアントの MCP 設定ファイルを開き、MCP Unity サーバー設定を追加します:

> `ABSOLUTE/PATH/TO` を MCP Unity インストールの絶対パスに置き換えるか、Unity Editor MCP Server ウィンドウ（Tools > MCP Unity > Server Window）からテキストをコピーしてください。
>
> Unity プロジェクトツリー内にあり Git にコミットする設定（例: `<project>/.vscode/mcp.json`、`<project>/opencode.json`、`<project>/.cursor/mcp.json`、`<project>/.mcp.json`、`<project>/.codex/config.toml`）には、同じファイルがマシン間で動作するプロジェクト相対パスを使用してください。Server Window の **"Use relative path"** を切り替えると、コピー用スニペットを絶対パスとプロジェクト相対パスの間で切り替えられます。**Configure GitHub Copilot**、**Configure OpenCode**、**Configure Cursor (Project)**、**Configure Claude Code (Project)**、**Configure Codex CLI (Project)** ボタンは、すでに相対パスを自動生成します。
>
> プロジェクトローカルボタン（Cursor / Claude Code / Codex CLI）は、グローバルユーザー設定ではなく Unity プロジェクトディレクトリに MCP サーバーエントリを書き込みます。これにより、ほかの（Unity ではない）プロジェクトに接続失敗警告が表示されません。特に **Codex CLI (Project)** では、プロジェクトルートで初めて `codex` を実行する際にプロジェクトの信頼確認を承認する必要があります。承認しない場合、Codex は `<project>/.codex/config.toml` を無視します。

**JSON ベースのクライアント用**（Cursor、Windsurf、Claude Code、GitHub Copilot など）:

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

ワークスペーススコープの VS Code / GitHub Copilot（`.vscode/mcp.json`）では、マシン間でパスを可搬にするため `${workspaceFolder}` を使用します:

```json
{
   "mcpServers": {
       "mcp-unity": {
          "command": "node",
          "args": [
             "${workspaceFolder}/Library/PackageCache/com.gamelovers.mcp-unity@<hash>/Server~/build/index.js"
          ]
       }
   }
}
```

**Codex CLI 用**（`~/.codex/config.toml`）:

```toml
[mcp_servers.mcp-unity]
command = "node"
args = ["ABSOLUTE/PATH/TO/mcp-unity/Server~/build/index.js"]
```

**Cursor — プロジェクトローカル用**（Unity プロジェクトルートの `.cursor/mcp.json`、プロジェクト相対パス）:

```json
{
   "mcpServers": {
       "mcp-unity": {
          "command": "node",
          "args": [
             "Library/PackageCache/com.gamelovers.mcp-unity@<hash>/Server~/build/index.js"
          ]
       }
   }
}
```

**Claude Code — プロジェクトローカル用**（Unity プロジェクトルートの `.mcp.json`、プロジェクト相対パス。Claude Code のチーム共有 MCP 設定）:

```json
{
   "mcpServers": {
       "mcp-unity": {
          "command": "node",
          "args": [
             "Library/PackageCache/com.gamelovers.mcp-unity@<hash>/Server~/build/index.js"
          ]
       }
   }
}
```

**Codex CLI — プロジェクトローカル用**（Unity プロジェクトルートの `.codex/config.toml`、プロジェクト相対パス）:

```toml
[mcp_servers.mcp-unity]
command = "node"
args = ["Library/PackageCache/com.gamelovers.mcp-unity@<hash>/Server~/build/index.js"]
```

> Codex はグローバルの `~/.codex/config.toml` にこのファイルを重ねますが、プロジェクトが信頼済みの場合に限られます。プロジェクトルートで初めて `codex` を実行する際は信頼確認を承認してください。承認しない場合、Codex は `.codex/config.toml` を無視します。

**OpenCode 用**（Unity プロジェクトルートの `opencode.json`）:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "mcp-unity": {
      "type": "local",
      "enabled": true,
      "command": ["node", "Library/PackageCache/com.gamelovers.mcp-unity@<hash>/Server~/build/index.js"],
      "environment": {}
    }
  }
}
```

> UPM パッケージキャッシュパスの `@<hash>` 部分はパッケージ更新時に変わります。MCP Unity を更新した場合は、**Configure** ボタンを再実行するか、スニペットのパスを手動で更新してください。

</details>

## <a name="start-server"></a>サーバーの起動

1. Unityエディターを開く
2. Tools > MCP Unity > Server Window に移動
3. "Start Server" をクリックして WebSocket サーバーを起動
4. AI コーディング IDE（例: Cursor、Windsurf、Claude Code、Codex CLI、GitHub Copilot、Google Antigravity、OpenCode など）を開き、Unity ツールの実行を開始
   
![connect](https://github.com/user-attachments/assets/2e266a8b-8ba3-4902-b585-b220b11ab9a2)

> AI クライアントが WebSocket サーバーに接続すると、ウィンドウの緑色のボックスに自動的に表示されます

## オプション：WebSocket ポートを設定
デフォルトでは、WebSocket サーバーは '8090' ポートで動作します。変更するには:

1. Unityエディターを開く
2. Tools > MCP Unity > Server Window に移動
3. "Connection Port" の値を希望のポート番号に変更
4. Unity は値を `ProjectSettings/McpUnitySettings.json` に保存
5. Node.js サーバーを再起動

Node ブリッジはインストール済みパッケージのパスからこの設定ファイルを検出するため、MCP クライアントの作業ディレクトリに依存しません。プロセスごとに上書きするには、たとえば `UNITY_PORT=9001 node build/index.js` のように `UNITY_PORT` を設定します。

## オプション: タイムアウト設定

デフォルトでは、MCPサーバーとWebSocket間のタイムアウトは 10 秒です。変更するには:

1. Unityエディターを開く  
2. **Tools > MCP Unity > Server Window** に移動  
3. **Request Timeout (seconds)** の値を希望のタイムアウト秒数に変更  
4. Unity は値を `ProjectSettings/McpUnitySettings.json` に保存
5. Node.jsサーバーを再起動  

プロセスごとに上書きするには、秒単位の `UNITY_REQUEST_TIMEOUT` を設定します（例：`UNITY_REQUEST_TIMEOUT=30 node build/index.js`）。

> [!TIP]  
> AIコーディングIDE（Claude Desktop、Cursor IDE、Windsurf IDEなど）とMCPサーバー間のタイムアウトは、使用するIDEによって異なる場合があります。

## オプション：永続的なヘッドレス MCP ホストを実行

MCP Unity は、CI とクラウドビルドでブリッジが起動したり npm が実行されたりしないよう、既定では `-batchmode` で無効です。長時間稼働するヘッドレス Editor を MCP ホストとして実行するには、Server Window で **Allow Batch Mode Server** を有効にするか、Unity の起動前に `MCP_UNITY_ALLOW_BATCH_MODE=true` を設定して明示的に有効化します:

```bash
MCP_UNITY_ALLOW_BATCH_MODE=true Unity -batchmode -nographics -projectPath /path/to/project -logFile /path/to/unity.log
```

有効化した場合、Unity 側の WebSocket サーバーは通常どおり起動しますが、バッチモードで `npm install` または `npm run build` を実行することはありません。Node ブリッジがすでにビルド済みで、MCP クライアントから利用可能であることを確認してください。

## ブリッジ設定の解決順序

Node ブリッジは各接続値を次の順で解決します: 環境変数（`UNITY_PORT`、`UNITY_HOST`、`UNITY_REQUEST_TIMEOUT`）、`MCP_UNITY_SETTINGS_PATH`、インストール済みパッケージの上位にある `ProjectSettings/McpUnitySettings.json`、作業ディレクトリの上位にある同ファイル、最後に既定値。生成される MCP 設定は `MCP_UNITY_SETTINGS_PATH` と `MCP_UNITY_AUTH_TOKEN_PATH` を明示的に設定します。ブリッジは値のソースをログ出力し、既定値へフォールバックする前に警告します。

## ブリッジのセキュリティとパッケージのインストール

MCP Unity はプロジェクトごとの 256 ビット認証トークンを `Library/McpUnity/bridge-token` に作成します。トークンの解決順序は厳密で、`MCP_UNITY_AUTH_TOKEN`、`MCP_UNITY_AUTH_TOKEN_PATH`、検出された Unity プロジェクト内のトークンの順です。明示的に指定した値が存在しない、または不正な場合はフォールバックせず失敗します。生成される設定はトークンファイルのパスを使用し、秘密値を `ProjectSettings` に保存しません。

Unity エンドポイントはユーザー名 `mcp-unity` の HTTP Basic 認証を要求し、`Origin` ヘッダーを含むすべての WebSocket ハンドシェイクを拒否します。WebSocket を直接利用する実装は認証を付け、`Origin` を送信しないでください。トークンのコピーまたは再生成は Server Window で行えます。再生成後は、すべての MCP クライアントプロセスを再起動してください。

Unity パッケージはコンパイル時に Editor コードを実行できるため、`add_package` ツールは既定で無効です。信頼できる MCP クライアントとパッケージソースに限り、Tools > MCP Unity > Server Window の **Allow Package Installation**、または `ProjectSettings/McpUnitySettings.json` の `AllowPackageInstallation=true` で有効にしてください。

バージョン 1.5.0 は意図的に 1.4.x の Node ブリッジを拒否します。Unity 側と Node 側の両方を更新し、パッケージキャッシュのパスが変わった場合は Unity の Configure 操作を再実行して、MCP クライアントを再起動してください。

## オプション：リモート MCP ブリッジ接続を許可する

デフォルトでは、WebSocket サーバーは 'localhost' にバインドされています。他のマシンから MCP ブリッジ接続を許可するには、以下の手順に従ってください：

1. Unity エディターを開く  
2. メニューから「Tools > MCP Unity > Server Window」を選択  
3. 「Allow Remote Connections（リモート接続を許可）」チェックボックスを有効にする  
4. Unity は WebSocket サーバーを '0.0.0.0'（すべてのインターフェース）にバインドします  
5. Node.js サーバーを再起動して新しいホスト設定を適用する  
6. リモートで MCP ブリッジを実行する場合は、環境変数 UNITY_HOST を Unity 実行マシンの IP アドレスに設定して起動：  
   `UNITY_HOST=192.168.1.100 node server.js`

リモート転送は暗号化されていない `ws://` のままです。認証は不正アクセスを防ぎますが通信内容を秘匿しないため、信頼できるネットワーク、VPN、または SSH トンネルに限定してください。Unity プロジェクトのトークンファイルを読めないリモートブリッジには、安全なシークレット管理を通して `MCP_UNITY_AUTH_TOKEN` を渡してください。

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
-  Cursor
-  Windsurf
-  Claude Desktop
-  Claude Code
-  Codex CLI
-  GitHub Copilot
-  Google Antigravity
-  OpenCode

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

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">ツール初期化中に <code>KeyError: 'position'</code> で失敗するクライアントがあるのはなぜですか？</span></summary>

一部の MCP クライアントは、`#/properties/position` のようなローカル JSON ポインター参照を含むツールスキーマの解析に失敗することがあります。

MCP Unity は、transform ツール入力（`set_transform`、`move_gameobject`、`rotate_gameobject`、`scale_gameobject`）を各フィールドごとに新しいネスト済みベクタースキーマで登録することで、生成されるスキーマがローカルの `#/properties/...` 参照に依存しないようにしています。

引き続きこのエラーが発生する場合:
- MCP クライアントを最新版へ更新する
- Node サーバーを再ビルドする（`cd Server~ && npm run build`）
- パッケージバージョンにこの互換性修正が含まれていることを確認する

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

## テストの実行

### C# テスト（Unity）

Unity の Test Runner でテストを実行します:

1. Unity Editor を開く
2. Window > General > Test Runner に移動
3. "EditMode" タブを選択
4. "Run All" をクリックしてすべてのテストを実行

### TypeScript テスト（サーバー）

Jest でテストを実行します:

```bash
cd Server~
npm test
```

ウォッチモードでテストを実行するには:

```bash
npm run test:watch
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
