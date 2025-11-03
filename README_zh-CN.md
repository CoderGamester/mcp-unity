# MCP Unity Editor（游戏引擎）

[![](https://badge.mcpx.dev?status=on 'MCP Enabled')](https://modelcontextprotocol.io/introduction)
[![](https://img.shields.io/badge/Unity-000000?style=flat&logo=unity&logoColor=white 'Unity')](https://unity.com/releases/editor/archive)
[![](https://img.shields.io/badge/Node.js-339933?style=flat&logo=nodedotjs&logoColor=white 'Node.js')](https://nodejs.org/en/download/)
[![](https://img.shields.io/github/stars/CoderGamester/mcp-unity 'Stars')](https://github.com/CoderGamester/mcp-unity/stargazers)
[![](https://img.shields.io/github/last-commit/CoderGamester/mcp-unity 'Last Commit')](https://github.com/CoderGamester/mcp-unity/commits/main)
[![](https://img.shields.io/badge/License-MIT-red.svg 'MIT License')](https://opensource.org/licenses/MIT)

| [🇺🇸英文](README.md) | [🇨🇳简体中文](README_zh-CN.md) | [🇯🇵日本語](README-ja.md) |
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

MCP Unity 是 Model Context Protocol 在 Unity 编辑器中的实现，允许 AI 助手与您的 Unity 项目交互。这个包提供了 Unity 和实现 MCP 协议的 Node.js 服务器之间的桥梁，使 Claude、Windsurf 和 Cursor 等 AI 代理能够在 Unity 编辑器中执行操作。

<a href="https://glama.ai/mcp/servers/@CoderGamester/mcp-unity">
  <img width="400" height="200" src="https://glama.ai/mcp/servers/@CoderGamester/mcp-unity/badge" alt="Unity MCP server" />
</a>

## 功能

### IDE 集成 - 包缓存访问

MCP Unity 通过将 Unity `Library/PackedCache` 文件夹添加到您的工作区，提供与 VSCode 类 IDE（Visual Studio Code、Cursor、Windsurf）的自动集成。此功能：

- 提高对 Unity 包的代码智能感知
- 为 Unity 包提供更好的自动完成和类型信息
- 帮助 AI 编码助手理解您项目的依赖关系

### MCP 服务器工具

- `execute_menu_item`: 执行 Unity 菜单项（用 MenuItem 属性标记的函数）
  > **示例提示:** "执行菜单项 'GameObject/Create Empty' 创建一个新的空 GameObject"

- `select_gameobject`: 通过路径或实例 ID 选择 Unity 层次结构中的游戏对象
  > **示例提示:** "选择场景中的 Main Camera 对象"

- `update_gameobject`: 更新 GameObject 的核心属性（名称、标签、层、激活/静态状态），如果不存在则创建
  > **示例提示:** "将 Player 对象的标签设置为 ‘Enemy’ 并使其不可用"

- `update_component`: 更新 GameObject 上的组件字段，如果 GameObject 不包含该组件则添加它
  > **示例提示:** "给 Player 对象添加 Rigidbody 组件并设置其质量为 5"

- `add_package`: 在 Unity 包管理器中安装新包
  > **示例提示:** "给我的项目添加 TextMeshPro 包"

- `run_tests`: 使用 Unity 测试运行器运行测试
  > **示例提示:** "运行我项目中所有的 EditMode 测试"

- `send_console_log`: 发送控制台日志到 Unity
  > **示例提示:** "发送控制台日志到 Unity 编辑器"

- `add_asset_to_scene`: 将 AssetDatabase 中的资源添加到 Unity 场景中
  > **示例提示:** "将我的项目中的 Player 预制体添加到当前场景"

- `create_prefab`: 创建预制体，并可选择添加 MonoBehaviour 脚本和设置序列化字段值
  > **示例提示:** "从 'PlayerController' 脚本创建一个名为 'Player' 的预制体"

- `recompile_scripts`: 重新编译 Unity 项目中的所有脚本
  > **示例提示:** "重新编译我 Unity 项目中的所有脚本"

### MCP 服务器资源

- `unity://menu-items`: 获取 Unity 编辑器中所有可用的菜单项列表，以方便 `execute_menu_item` 工具
  > **示例提示:** "显示与 GameObject 创建相关的所有可用菜单项"

- `unity://scenes-hierarchy`: 获取当前 Unity 场景层次结构中所有游戏对象的列表
  > **示例提示:** "显示当前场景层次结构"

- `unity://gameobject/{id}`: 通过实例 ID 或场景层次结构中的对象路径获取特定 GameObject 的详细信息，包括所有 GameObject 组件及其序列化的属性和字段
  > **示例提示:** "获取 Player GameObject 的详细信息"

- `unity://logs`: 获取 Unity 控制台的所有日志列表
  > **示例提示:** "显示 Unity 控制台最近的错误信息"

- `unity://packages`: 从 Unity 包管理器获取已安装和可用包的信息
  > **示例提示:** "列出我 Unity 项目中当前安装的所有包"

- `unity://assets`: 获取 Unity 资产数据库中资产的信息
  > **示例提示:** "查找我项目中的所有纹理资产"

- `unity://tests/{testMode}`: 获取 Unity 测试运行器中测试的信息
  > **示例提示:** "列出我 Unity 项目中所有可用的测试"

## 要求
- Unity 2022.3 或更高版本 - 用于[安装服务器](#install-server)
- Node.js 18 或更高版本 - 用于[启动服务器](#start-server)
- npm 9 或更高版本 - 用于[调试服务器](#debug-server)

> [!IMPORTANT]
> **项目路径不能包含空格**
>
> 您的 Unity 项目文件路径**不能包含任何空格**，这一点至关重要。
> 如果您的项目路径包含空格，MCP 客户端（例如 Cursor、Claude、Windsurf）将无法连接到 MCP Unity 服务器。
>
> **示例：**
> -   ✅ **有效：** `C:\Users\YourUser\Documents\UnityProjects\MyAwesomeGame`
> -   ❌ **无效：：** `C:\Users\Your User\Documents\Unity Projects\My Awesome Game`
>
> 在继续安装之前，请确保您的项目位于不含空格的路径中。

## <a name="install-server"></a>安装

安装 MCP Unity 服务器是一个多步骤过程：

### 步骤 1: 安装 Node.js 
> 要运行 MCP Unity 服务器，您需要在计算机上安装 Node.js 18 或更高版本：

![node](docs/node.jpg)

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Windows</span></summary>

1. 访问 [Node.js 下载页面](https://nodejs.org/en/download/)
2. 下载 Windows 安装程序 (.msi) 的 LTS 版本（推荐）
3. 运行安装程序并按照安装向导操作
4. 通过打开 PowerShell 并运行以下命令验证安装：
   ```bash
   node --version
   ```
</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">macOS</span></summary>

1. 访问 [Node.js 下载页面](https://nodejs.org/en/download/)
2. 下载 macOS 安装程序 (.pkg) 的 LTS 版本（推荐）
3. 运行安装程序并按照安装向导操作
4. 或者，如果您已安装 Homebrew，可以运行：
   ```bash
   brew install node@18
   ```
5. 通过打开终端并运行以下命令验证安装：
   ```bash
   node --version
   ```
</details>

### 步骤 2: 通过 Unity 包管理器安装 Unity MCP 服务器包
1. 打开 Unity 包管理器 (Window > Package Manager)
2. 点击左上角的 "+" 按钮
3. 选择 "Add package from git URL..."
4. 输入: `https://github.com/CoderGamester/mcp-unity.git`
5. 点击 "Add"

![package manager](https://github.com/user-attachments/assets/a72bfca4-ae52-48e7-a876-e99c701b0497)

### 步骤 3: 配置 AI LLM 客户端

<details open>
<summary><span style="font-size: 1.1em; font-weight: bold;">选项 1: 使用 Unity 编辑器配置</span></summary>

1. 打开 Unity 编辑器
2. 导航到 Tools > MCP Unity > Server Window
3. 点击 "Configure" 按钮为您的 AI LLM 客户端配置，如下图所示

![image](docs/configure.jpg)

4. 使用给定的弹出窗口确认配置安装

![image](https://github.com/user-attachments/assets/b1f05d33-3694-4256-a57b-8556005021ba)

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">选项 2: 手动配置</span></summary>

打开您的 AI 客户端的 MCP 配置文件（例如 Claude Desktop 中的 claude_desktop_config.json）并复制以下文本：

> 将 `ABSOLUTE/PATH/TO` 替换为您的 MCP Unity 安装的绝对路径，或者直接从 Unity 编辑器 MCP 服务器窗口（Tools > MCP Unity > Server Window）复制文本。

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

## <a name="start-server"></a>启动 Unity 编辑器 MCP 服务器
1. 打开 Unity 编辑器
2. 导航到 Tools > MCP Unity > Server Window
3. 点击 "Start Server" 按钮以启动 WebSocket 服务器
4. 打开 Claude Desktop 或您的 AI 编码 IDE（例如 Cursor IDE、Windsurf IDE 等）并开始执行 Unity 工具
   
![connect](https://github.com/user-attachments/assets/2e266a8b-8ba3-4902-b585-b220b11ab9a2)

> 当 AI 客户端连接到 WebSocket 服务器时，它将自动显示在窗口的绿色框中

## 可选：设置 WebSocket 端口
默认情况下，WebSocket 服务器运行在 '8090' 端口。您可以通过两种方式更改此端口：

1. 打开 Unity 编辑器
2. 导航到 Tools > MCP Unity > Server Window
3. 将 "WebSocket Port" 值更改为所需的端口号
4. Unity 将设置系统环境变量 UNITY_PORT 为新的端口号
5. 重启 Node.js 服务器
6. 再次点击 "Start Server" 以重新连接 Unity 编辑器 WebSocket 到 Node.js MCP 服务器

## 可选：设置超时

默认情况下，MCP 服务器与 WebSocket 之间的超时时间为 10 秒。
您可以根据您使用的操作系统进行更改：

1. 打开 Unity 编辑器
2. 导航到 Tools > MCP Unity > Server Window
3. 将 "Request Timeout (seconds)" 值更改为所需的超时秒数
4. Unity 将设置系统环境变量 UNITY_REQUEST_TIMEOUT 为新的超时值
5. 重启 Node.js 服务器
6. 再次点击 "Start Server" 以重新连接 Unity 编辑器 WebSocket 到 Node.js MCP 服务器

> [!TIP]  
> 您的 AI 编码 IDE（例如 Claude Desktop、Cursor IDE、Windsurf IDE）与 MCP 服务器之间的超时时间取决于 IDE。

## 可选：允许远程 MCP Bridge 连接

默认情况下，WebSocket 服务器绑定到 'localhost'。要允许来自其他设备的 MCP Bridge 连接，请执行以下步骤：

1. 打开 Unity 编辑器  
2. 依次点击菜单「Tools > MCP Unity > Server Window」  
3. 勾选"Allow Remote Connections（允许远程连接）"复选框  
4. Unity 将 WebSocket 服务器绑定到 '0.0.0.0'（所有网络接口）  
5. 重新启动 Node.js 服务器以应用新的主机配置  
6. 在远程运行 MCP Bridge 时，将环境变量 UNITY_HOST 设置为 Unity 所在机器的 IP 地址：  
   `UNITY_HOST=192.168.1.100 node server.js`

## <a name="debug-server"></a>调试服务器

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">构建 Node.js 服务器</span></summary>

MCP Unity 服务器使用 Node.js 构建。它需要将 TypeScript 代码编译到 `build` 目录中。
如果出现问题，您可以通过以下方式强制安装：

1. 打开 Unity 编辑器
2. 导航到 Tools > MCP Unity > Server Window
3. 点击 "Force Install Server" 按钮

![install](docs/install.jpg)

如果您想手动构建，可以按照以下步骤操作：

1. 打开终端/PowerShell/命令提示符

2. 导航到 Server 目录：
   ```bash
   cd ABSOLUTE/PATH/TO/mcp-unity/Server~
   ```

3. 安装依赖：
   ```bash
   npm install
   ```

4. 构建服务器：
   ```bash
   npm run build
   ```

5. 运行服务器：
   ```bash
   node build/index.js
   ```

</details>
   
<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">使用 MCP Inspector 调试</span></summary>

使用 [@modelcontextprotocol/inspector](https://github.com/modelcontextprotocol/inspector) 调试服务器：
   - Powershell
   ```powershell
   npx @modelcontextprotocol/inspector node Server~/build/index.js
   ```
   - 命令提示符/终端
   ```cmd
   npx @modelcontextprotocol/inspector node Server~/build/index.js
   ```

在关闭终端或使用 [MCP Inspector](https://github.com/modelcontextprotocol/inspector) 调试之前，请务必使用 `Ctrl + C` 关闭服务器。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">启用控制台日志</span></summary>

1. 启用终端或 log.txt 文件中的日志记录：
   - Powershell
   ```powershell
   $env:LOGGING = "true"
   $env:LOGGING_FILE = "true"
   ```
   - 命令提示符/终端
   ```cmd
   set LOGGING=true
   set LOGGING_FILE=true
   ```

</details>

## 常见问题

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">什么是 MCP Unity？</span></summary>

MCP Unity 是一个功能强大的桥梁，使用 Model Context Protocol (MCP) 将您的 Unity 编辑器环境连接到 AI 助手 LLM 工具。

本质上，MCP Unity：
-   将 Unity 编辑器功能（如创建对象、修改组件、运行测试等）公开为 AI 可以理解和使用的“工具”和“资源”。
-   在 Unity 内运行 WebSocket 服务器，并在 Node.js 服务器（作为 Unity 的 WebSocket 客户端）中实现 MCP。这允许 AI 助手向 Unity 发送命令并接收信息。
-   使您能够使用自然语言提示与 AI 助手在 Unity 项目中执行复杂任务，从而显著加快开发工作流程。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">为什么要使用 MCP Unity？</span></summary>

MCP Unity 为开发人员、美术和项目经理提供了多个优势：

-   **加速开发：** 使用 AI 提示自动化重复任务、生成样板代码并管理资源。
-   **提高生产力：** 无需手动点击菜单或为简单操作编写脚本即可与 Unity 编辑器交互。
-   **提高可访问性：** 让不熟悉 Unity 编辑器或 C# 的用户也能在 AI 引导下进行有效修改。
-   **无缝集成：** 适配支持 MCP 的多种 AI 助手和 IDE。
-   **可扩展性：** 可以扩展协议和工具集，按需暴露更多项目/Unity 功能。
-   **协作潜力：** 促进新的协作方式，帮助新人上手项目结构与操作。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">MCP Unity 与即将发布的 Unity 6.2 AI 功能有何比较？</span></summary>

-   **焦点：**
    -   **MCP Unity：** 侧重于**编辑器自动化与交互**，允许外部 AI *控制和查询 Unity 编辑器* 来操作场景、资源与项目设置。
    -   **Unity 6.2 AI：**
        -   在编辑器内进行内容生成（纹理、精灵、动画、行为、脚本）和 AI 辅助，直接集成到编辑器界面。
        -   提供微调模型以回答关于 Unity 文档与 API 结构的问题。
        -   增加运行 AI 推理能力，支持在运行时*部署并运行*预训练网络（如 NPC 行为、图像识别等）。

-   **用例：**
    -   **MCP Unity：** “创建一个新的 3D 对象，将其命名为 ‘Player’，添加 Rigidbody，并将质量设为 10。” “运行所有 Play Mode 测试。” “请求修复控制台错误。” “执行自定义菜单项 ‘Prepare build for iOS’ 并修复错误。”
    -   **Unity 6.2 AI：** “为该材质生成科幻纹理。” “将所有树木放入标记为 ‘forest’ 的区域。” “创建行走动画。” “生成 2D 精灵。” “询问控制台错误细节。”

-   **互补而非互斥：** 两者可以互补：用 MCP Unity 做编辑器自动化/批量修改，再用 Unity AI 工具做内容生成。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">当前哪些 MCP 主机和 IDE 支持 MCP Unity？</span></summary>

已知兼容的平台包括：
-  Windsurf
-  Cursor
-  GitHub Copilot
-  Claude Desktop

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">我可以为我的项目扩展 MCP Unity 以使用自定义工具吗？</span></summary>

可以。
-   **在 Unity (C#) 中：** 创建继承自 `McpToolBase` 的 C# 类并在 `McpUnityServer.cs` 注册。
-   **在 Node.js (TypeScript) 中：** 在 `Server/src/tools/` 定义对应工具（含 Zod 输入/输出模式），并在 `Server/src/index.ts` 注册。Node 端会将请求转发给 Unity C# 工具。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">MCP Unity 是免费使用的吗？</span></summary>

是的，MCP Unity 在 MIT 许可证下开源发布。

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">为什么我无法连接到 MCP Unity？</span></summary>

- 确认 WebSocket 服务器已启动（在 Unity 的 Server Window）
- 从 MCP 客户端发送一条控制台日志以强制重连
- 在 Unity Editor MCP Server 窗口更改端口号（Tools > MCP Unity > Server Window）

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">为什么 MCP Unity 服务器无法启动？</span></summary>

- 检查 Unity 控制台错误
- 确保 Node.js 已安装并在 PATH 中
- 验证 Server 目录依赖已安装

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">为什么运行 Play Mode 测试时会出现连接失败错误？</span></summary>

`run_tests` 工具会返回：
```
Error:
Connection failed: Unknown error
```

这是因为切换到 Play Mode 时域重载导致桥接连接丢失。解决方法是在 **Edit > Project Settings > Editor > "Enter Play Mode Settings"** 中关闭 **Reload Domain**。

</details>

## 故障排除：WSL2（Windows 11）网络

当 MCP（Node.js）服务器在 WSL2 内运行，而 Unity 在 Windows 11 上运行时，连接 `ws://localhost:8090/McpUnity` 可能会失败并报错 `ECONNREFUSED`。

原因：WSL2 与 Windows 使用不同的网络命名空间——WSL2 内的 `localhost` 并不指向 Windows 主机。默认情况下，Unity 监听 `localhost:8090`。

### 解决方案 1 — 启用 WSL2 镜像网络（推荐）
- Windows 11：设置 → 系统 → 面向开发人员 → WSL → 启用“镜像模式网络”。
- 或通过 `.wslconfig`（之后执行 `wsl --shutdown` 并重新打开 WSL）：

```ini
[wsl2]
networkingMode=mirrored
```

启用后，Windows 与 WSL2 共享 `localhost`，默认配置（`localhost:8090`）即可正常工作。

### 解决方案 2 — 将 Node 客户端指向 Windows 主机
在启动 MCP 客户端之前，在 WSL 终端中设置：

```bash
# 从 resolv.conf 中检测 Windows 主机 IP
export UNITY_HOST=$(grep -m1 nameserver /etc/resolv.conf | awk '{print $2}')
```

这样，`Server~/src/unity/mcpUnity.ts` 将连接到 `ws://$UNITY_HOST:8090/McpUnity` 而不是 `localhost`（它读取 `UNITY_HOST`，如果 `ProjectSettings/McpUnitySettings.json` 中存在 `Host` 字段，也会优先使用）。

### 解决方案 3 — 允许 Unity 接受远程连接
- Unity：Tools → MCP Unity → Server Window → 勾选“Allow Remote Connections”（Unity 绑定到 `0.0.0.0`）。
- 确保 Windows 防火墙允许所配置端口（默认 8090）的入站 TCP。
- 在 WSL2 中，连接到 Windows 主机 IP（见解决方案 2），或在启用镜像网络后连接 `localhost`。

> [!NOTE]
> 默认端口为 `8090`。您可以在 Unity 的 Server Window（Tools → MCP Unity → Server Window）中进行更改。该值映射到 `McpUnitySettings`，并持久化到 `ProjectSettings/McpUnitySettings.json`。

#### 验证连接

```bash
npm i -g wscat
# 启用镜像网络后
wscat -c ws://localhost:8090/McpUnity
# 或使用 Windows 主机 IP
wscat -c ws://$UNITY_HOST:8090/McpUnity
```

## 支持与反馈

如有问题或需要支持，请在本仓库提交 [issue](https://github.com/CoderGamester/mcp-unity/issues)，或通过以下方式联系：
- Linkedin: [![](https://img.shields.io/badge/LinkedIn-0077B5?style=flat&logo=linkedin&logoColor=white 'LinkedIn')](https://www.linkedin.com/in/miguel-tomas/)
- Discord: gamester7178
- Email: game.gamester@gmail.com

## 贡献

欢迎贡献！欢迎提交 Pull Request 或 Issue。

提交请遵循 [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) 规范。

## 许可证

本项目使用 [MIT License](LICENSE.md) 授权。

## 鸣谢

- [Model Context Protocol](https://modelcontextprotocol.io)
- [Unity Technologies](https://unity.com)
- [Node.js](https://nodejs.org)
- [WebSocket-Sharp](https://github.com/sta/websocket-sharp)
