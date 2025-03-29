# MCP Unity

[![](https://badge.mcpx.dev?type=server 'MCP Server')](https://modelcontextprotocol.io/introduction)
[![](https://img.shields.io/badge/Unity-000000?style=flat&logo=unity&logoColor=white 'Unity')](https://unity.com/releases/editor/archive)
[![](https://img.shields.io/badge/Node.js-339933?style=flat&logo=nodedotjs&logoColor=white 'Node.js')](https://nodejs.org/en/download/)

[![smithery badge](https://smithery.ai/badge/@CoderGamester/mcp-unity)](https://smithery.ai/server/@CoderGamester/mcp-unity)
[![](https://img.shields.io/github/stars/CoderGamester/mcp-unity 'Stars')](https://github.com/CoderGamester/mcp-unity/stargazers)
[![](https://img.shields.io/github/forks/CoderGamester/mcp-unity 'Forks')](https://github.com/CoderGamester/mcp-unity/network/members)
[![](https://img.shields.io/github/last-commit/CoderGamester/mcp-unity 'Last Commit')](https://github.com/CoderGamester/mcp-unity/commits/main)
[![](https://img.shields.io/badge/License-MIT-red.svg 'MIT License')](https://opensource.org/licenses/MIT)

<a href="https://glama.ai/mcp/servers/@CoderGamester/mcp-unity">
  <img width="380" height="200" src="https://glama.ai/mcp/servers/@CoderGamester/mcp-unity/badge" alt="Unity MCP server" />
</a>

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

MCP Unity is an implementation of the Model Context Protocol for Unity Editor, allowing AI assistants to interact with your Unity projects. This package provides a bridge between Unity and a Node.js server that implements the MCP protocol, enabling AI agents like Claude, Windsurf, and Cursor to execute operations within the Unity Editor.

## Features
MCP Unity currently provides the following tools:

- __**execute_menu_item**__: Executes Unity menu items (functions tagged with the MenuItem attribute)
- __**select_gameobject**__: Selects game objects in the Unity hierarchy by path or instance ID
- __**update_component**__: Updates component fields on a GameObject or adds it to the GameObject if it does not contain the component
- __**package_manager**__: Installs, removes, and updates packages in the Unity Package Manager
- __**run_tests**__: Runs tests using the Unity Test Runner
- __**notify_message**__: Displays messages in the Unity Editor

MCP Unity currently provides the following resources:

- __**get_menu_items**__: Retrieves a list of all available menu items in the Unity Editor to facilitate __**execute_menu_item**__ tool
- __**get_hierarchy**__: Retrieves a list of all game objects in the Unity hierarchy
- __**get_gameobject**__: Retrieves detailed information about a specific GameObject by instance ID, including all GameObject components with it's serialized properties and fields
- __**get_console_logs**__: Retrieves a list of all logs from the Unity console
- __**get_packages**__: Retrieves information about installed and available packages from the Unity Package Manager
- __**get_assets**__: Retrieves information about assets in the Unity Asset Database
- __**get_tests**__: Retrieves information about tests in the Unity Test Runner

## Requirements
- Unity 2022.3 or later - to [install the server](#install-server)
- Node.js 18 or later - to [start the server](#start-server)
- npm 9 or later - to [debug the server](#debug-server)

## <a name="install-server"></a>Installation

Installing this MCP Unity Server is a multi-step process:

### Step 1: Install Unity MCP Server package via Unity Package Manager
1. Open the Unity Package Manager (Window > Package Manager)
2. Click the "+" button in the top-left corner
3. Select "Add package from git URL..."
4. Enter: `https://github.com/CoderGamester/mcp-unity.git`
5. Click "Add"

![package manager](https://github.com/user-attachments/assets/a72bfca4-ae52-48e7-a876-e99c701b0497)


### Step 2: Install Node.js 
> To run MCP Unity server, you'll need to have Node.js 18 or later installed on your computer:

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Windows</span></summary>

1. Visit the [Node.js download page](https://nodejs.org/en/download/)
2. Download the Windows Installer (.msi) for the LTS version (recommended)
3. Run the installer and follow the installation wizard
4. Verify the installation by opening PowerShell and running:
   ```bash
   node --version
   ```
</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">macOS</span></summary>

1. Visit the [Node.js download page](https://nodejs.org/en/download/)
2. Download the macOS Installer (.pkg) for the LTS version (recommended)
3. Run the installer and follow the installation wizard
4. Alternatively, if you have Homebrew installed, you can run:
   ```bash
   brew install node@18
   ```
5. Verify the installation by opening Terminal and running:
   ```bash
   node --version
   ```
</details>

### Step 3: Configure AI LLM Client

<details open>
<summary><span style="font-size: 1.1em; font-weight: bold;">Option 1: Configure using Unity Editor</span></summary>

1. Open the Unity Editor
2. Navigate to Tools > MCP Unity > Server Window
3. Click on the "Configure" button for your AI LLM client as shown in the image below
4. Confirm the configuration installation with the given popup

![MCP configuration](https://github.com/user-attachments/assets/ea9bb912-94a7-4409-81c1-3af39158dac0)

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Option 2: Configure via Smithery</span></summary>

To install MCP Unity via [Smithery](https://smithery.ai/server/@CoderGamester/mcp-unity):

```
Currently not available
```
</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Option 3: Configure Manually</span></summary>

Open the MCP configuration file of your AI client (e.g. claude_desktop_config.json in Claude Desktop) and copy the following text:

> Replace `ABSOLUTE/PATH/TO` with the absolute path to your MCP Unity installation or just copy the text from the Unity Editor MCP Server window (Tools > MCP Unity > Server Window).

```json
{
   "mcpServers": {
   "mcp-unity": {
      "command": "node",
      "args": [
         "ABSOLUTE/PATH/TO/mcp-unity/Server/build/index.js"
      ],
      "env": {
         "UNITY_PORT": "8090"
      }
   }
   }
}
```

</details>

## <a name="start-server"></a>Start Unity Editor MCP Server
1. Open the Unity Editor
2. Navigate to Tools > MCP Unity > Server Window
3. Click "Start Server" to start the WebSocket server
4. Open Claude Desktop or your AI Coding IDE (e.g. Cursor IDE, Windsurf IDE, etc.) and start executing Unity tools
   
![connect](https://github.com/user-attachments/assets/2e266a8b-8ba3-4902-b585-b220b11ab9a2)

## Configure the WebSocket Port
By default, the WebSocket server runs on port 8080. You can change this port in two ways:

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Option 1: Using the Unity Editor</span></summary>

1. Open the Unity Editor
2. Navigate to Tools > MCP Unity > Server Window
3. Change the "WebSocket Port" value to your desired port number
4. Unity will setup the system environment variable UNITY_PORT to the new port number
5. Restart the Node.js server
6. Click again on "Start Server" to reconnect the Unity Editor web socket to the Node.js MCP Server

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Option 2: Using the terminal</span></summary>

1. Set the UNITY_PORT environment variable in the terminal
   - Powershell
   ```powershell
   $env:UNITY_PORT = "8090"
   ```
   - Command Prompt/Terminal
   ```cmd
   set UNITY_PORT=8090
   ```
2. Restart the Node.js server
3. Click again on "Start Server" to reconnect the Unity Editor web socket to the Node.js MCP Server

</details>

## <a name="debug-server"></a>Debugging the Server

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Building the Node.js Server</span></summary>

The MCP Unity server is built using Node.js . It requires to compile the TypeScript code to JavaScript in the `build` directory.
To build the server, open a terminal and:

1. Navigate to the Server directory:
   ```bash
   cd ABSOLUTE/PATH/TO/mcp-unity/Server
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Build the server:
   ```bash
   npm run build
   ```

4. Run the server:
   ```bash
   node build/index.js
   ```

</details>
   
<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Debugging with MCP Inspector</span></summary>

Debug the server with [@modelcontextprotocol/inspector](https://github.com/modelcontextprotocol/inspector):
   - Powershell
   ```powershell
   $env:UNITY_PORT=8090; npx @modelcontextprotocol/inspector node Server/build/index.js
   ```
   - Command Prompt/Terminal
   ```cmd
   set UNITY_PORT=8090 && npx @modelcontextprotocol/inspector node Server/build/index.js
   ```

Don't forget to shutdown the server with `Ctrl + C` before closing the terminal or debugging it with the [MCP Inspector](https://github.com/modelcontextprotocol/inspector).

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Enable Console Logs</span></summary>

1. Enable logging on your terminal or into a log.txt file:
   - Powershell
   ```powershell
   $env:LOGGING = "true"
   $env:LOGGING_FILE = "true"
   ```
   - Command Prompt/Terminal
   ```cmd
   set LOGGING=true
   set LOGGING_FILE=true
   ```

</details>

## Troubleshooting

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Connection Issues</span></summary>

- Ensure the WebSocket server is running (check the Server Window in Unity)
- Check if there are any firewall restrictions blocking the connection
- Make sure the port number is correct (default is 8080)
- Change the port number in the Unity Editor MCP Server window. (Tools > MCP Unity > Server Window)
</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Server Not Starting</span></summary>

- Check the Unity Console for error messages
- Ensure Node.js is properly installed and accessible in your PATH
- Verify that all dependencies are installed in the Server directory
</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Menu Items Not Executing</span></summary>

- Ensure the menu item path is correct (case-sensitive)
- Check if the menu item requires confirmation
- Verify that the menu item is available in the current context
</details>

## Support & Feedback

If you have any questions or need support, please open an [issue](https://github.com/CoderGamester/mcp-unity/issues) on this repository.

Alternative you can reach out on [![](https://img.shields.io/badge/LinkedIn-0077B5?style=flat&logo=linkedin&logoColor=white 'LinkedIn')](https://www.linkedin.com/in/miguel-tomas/)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request or open an Issue with your request.

**Commit your changes** following the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) format.

## License

This project is under [MIT License](License.md)