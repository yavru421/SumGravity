# SumGravity 1.0

A lightweight, locally-hosted developer assistant built specifically for the "Foreman Model" workflow. It pairs a Blazor Server C# backend with a local Qwen-2.5-Coder-7B instance running via KoboldCPP, optimized to fit within an 8GB VRAM limit (RTX 4060).

## Features
- **Local-First**: Runs entirely offline using KoboldCPP.
- **Context-Efficient**: Communicates with the LLM using a strict Search/Replace diff protocol to minimize context window bloat on 8B parameter models.
- **Integrated Tooling**:
  - **File Explorer**: Browse the workspace directly from the UI.
  - **Terminal Log**: Execute raw PowerShell commands on the host machine.
  - **Skills Runner**: Execute complex, multi-agent markdown `.SKILL` definitions directly against the local system.
  - **Dev Chat**: SSE-streamed real-time chat with the model.

## Architecture
- **Framework**: .NET 8 Blazor Server
- **Styling**: Vanilla CSS, Dark Mode, Inter Font
- **Model**: Qwen2.5-Coder-7B-Instruct-Q4_K_M.gguf
- **API**: Standard OpenAI-compatible `/v1/` endpoints provided by KoboldCPP.

## Quick Start

1. Start the LLM backend (requires KoboldCPP):
   ```powershell
   C:\AI\koboldcpp\launch_sumgravity.bat
   ```
   *(Ensure your laptop's cooling profile is set to "Max" for sustained token generation.)*

2. Start the SumGravity Blazor Server:
   ```powershell
   cd C:\dev\SumGravity
   dotnet run --urls "http://localhost:5000"
   ```

3. Open `http://localhost:5000` in your browser. Wait for the KoboldCPP indicator to turn Green.
