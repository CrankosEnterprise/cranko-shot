# CrankoShot — Captura de Tela & Inteligência Visual

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Plataforma](https://img.shields.io/badge/Plataforma-Windows%2010%20%7C%2011-0078D6.svg)](https://www.microsoft.com/windows)
[![Arquitetura](https://img.shields.io/badge/Arquitetura-x64-green.svg)]()
[![WPF](https://img.shields.io/badge/UI-WPF%20Glassmorphism-purple.svg)]()
[![Release](https://img.shields.io/github/v/release/CrankosEnterprise/cranko-shot?label=Vers%C3%A3o%20Oficial&color=8B5CF6)](https://github.com/CrankosEnterprise/cranko-shot/releases/latest)

O **CrankoShot** é um aplicativo desktop moderno para Windows focado em captura ágil de tela, edição visual imediata e análise multimodal através de Inteligência Artificial. Com interface fluida em estilo Glassmorphism, o aplicativo reúne ferramentas completas de anotação vetorial, mira personalizável com inversão localizada de pixels em tempo real, OCR nativo 100% offline e integração independente com os provedores de IA **Google Gemini** e **OpenRouter**.

---

## 📥 Download

Baixe o instalador oficial do **CrankoShot** para Windows:

[![Baixar CrankoShot](https://img.shields.io/badge/⬇%EF%B8%8F%20Baixar-CrankoShot%20para%20Windows-8B5CF6?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/CrankosEnterprise/cranko-shot/releases/latest/download/CrankoShot-Setup.exe)

> **Instalador Oficial:** [`CrankoShot-Setup.exe`](https://github.com/CrankosEnterprise/cranko-shot/releases/latest/download/CrankoShot-Setup.exe)  
> Pacote completo autônomo (*self-contained* x64) com todas as dependências do .NET 8 embutidas, pronto para instalar e executar.
>
> **Compatibilidade do Sistema:**
> - **Sistemas Operacionais:** Windows 10 (versão 2004 / Build 19041 ou superior) e Windows 11.
> - **Arquitetura:** x64 (64 bits).
>
> 📦 **Versões Disponíveis:**
> - [Última Versão Estável (Latest)](https://github.com/CrankosEnterprise/cranko-shot/releases/latest)
> - [Release Inicial v1.0.0](https://github.com/CrankosEnterprise/cranko-shot/releases/tag/v1.0.0)
> - [Todas as Releases Publicadas](https://github.com/CrankosEnterprise/cranko-shot/releases)

---

## 🎯 Principais Funcionalidades

### 1. Captura de Tela Instantânea e Múltiplos Monitores
- **Atalho Global:** Acionamento rápido pela tecla `Print Screen` (ou atalho customizado via Win32 `RegisterHotKey`).
- **Seleção Retangular Suave:** Escurecimento da tela com seleção precisa de áreas, redimensionamento por alças e movimentação do quadro.
- **Suporte PerMonitorV2:** Totalmente adaptado para múltiplos monitores com resoluções e escalas de DPI diferentes.
- **Exibição de Dimensões:** Informação em tempo real sobre a largura e altura em pixels da região selecionada.

### 2. Personalização Avançada da Mira
- **5 Formatos de Mira:**
  - *Mira Aberta:* Linhas direcionais separadas, sem ponto central.
  - *Mira Fechada:* Cruz contínua no ponto central.
  - *Mira com Ponto Central:* Ponto focal isolado para mira fina.
  - *Mira com Linhas e Ponto:* Linhas direcionais com espaçamento e ponto central.
  - *Mira Simples:* Formato clássico equilibrado.
- **Inversão Localizada de Cores em Tempo Real:** Mecanismo gráfico de alto contraste que inverte somente os pixels da tela diretamente cobertos pelos traços da mira (traços escuros sobre superfícies claras e claros sobre superfícies escuras), sem alterar o restante da imagem.
- **Ajustes Geométricos e Cores:** Controle fino de espessura, comprimento das linhas, abertura central (vão), tamanho do ponto central e contorno de contraste.
- **Prévia Dinâmica:** Visualizador nas configurações simulando o comportamento da mira sobre superfícies claras e escuras.

### 3. Barra Flutuante e Ferramentas de Anotação
- **Ferramentas Vetoriais:** Seta direcional, linha reta, retângulo (com preenchimento opcional), círculo/elipse, caneta/lápis livre, marca-texto com transparência e marcadores de etapas sequenciais numeradas.
- **Censura e Proteção de Privacidade:** Ocultação de dados sensíveis por desfoque (*blur*) ou pixelização (*mosaico*), com intensidade ajustável e bordas estritamente confinadas à área selecionada.
- **Texto e Cores:** Inserção de caixas de texto com tamanhos flexíveis e paleta de cores moderna com 8 temas de destaque (Roxo padrão, Vermelho, Rosa, Verde, Amarelo, Cinza, Laranja e Azul).
- **Desfazer / Refazer:** Pilha completa com suporte a `Ctrl+Z` e `Ctrl+Y`.
- **Ações Imediatas:** Copiar para a área de transferência (`Ctrl+C`), salvar em arquivo (`Ctrl+S`), fixar na tela (`Ctrl+Shift+P`), abrir no editor completo (`Ctrl+E`), executar OCR ou acionar análise com IA.

### 4. Editor Completo de Imagens
- Janela independente para edição detalhada de capturas anteriores ou imagens abertas do computador.
- Controles de zoom, navegação panorâmica (*pan*), recorte, rotação e confirmação preventiva antes de descartar alterações não salvas.

### 5. OCR Local (100% Offline e Privado)
- Reconhecimento óptico de caracteres através da API nativa `Windows.Media.Ocr`.
- Totalmente executado localmente, sem envio de imagens para a internet e sem necessidade de chaves de API.
- Suporte a pacotes de idiomas instalados no sistema operacional (incluindo Português do Brasil - `pt-BR`).

### 6. Inteligência Artificial Multimodal com Dois Provedores Independentes
O CrankoShot permite alternar a qualquer momento entre provedores de IA sem perder chaves ou preferências individuais:
- **Google Gemini:**
  - Análise profunda do conteúdo visual de capturas.
  - Extração inteligente de dados estruturados (código-fonte, logs, tabelas).
  - Tradução direta de textos estrangeiros na imagem.
  - Diagnóstico técnico de mensagens de erro na tela e sugestão de soluções.
  - Chat interativo multi-turno para perguntas e respostas sobre a imagem capturada.
  - **Pesquisa na Web com Gemini:** Respostas fundamentadas com links de fontes reais (*Google Search Grounding*).
- **OpenRouter (API Oficial):**
  - Catálogo verificado priorizando **modelos gratuitos (`:free`) com Visão e Texto** (como `google/gemma-4-31b-it:free`, `google/gemma-4-26b-a4b-it:free`, `nvidia/nemotron-3.5-super-vl:free`, `openrouter/free`, etc.).
  - Compatibilidade com modelos somente texto (utilizando extração OCR local automática quando acionados sobre capturas).
  - Bloqueio estrito de modelos de geração de imagem.
  - Botão para atualizar o catálogo online diretamente da API.
- **Proteção Total de Chaves:** Chaves criptografadas via **Windows DPAPI** (`ProtectedData`) no arquivo de configuração do usuário, com sanitização automática nos logs e telas de erro.

### 7. Histórico Local em SQLite
- Indexação automática de capturas no banco SQLite local (`%AppData%\ShotMind\history.db`).
- Visualização em grade com miniaturas pré-renderizadas, busca rápida por texto de OCR ou nome de arquivo e reabertura direta no editor.

### 8. Execução Silenciosa na Bandeja do Sistema (System Tray)
- Funciona em segundo plano sem janela de console aberta.
- Acesso rápido a todas as funções por menu de contexto ou duplo clique no ícone roxo da bandeja.

---

## 🏗️ Arquitetura do Projeto

A solução está estruturada em camadas bem definidas:

```
App de Print/
├── ShotMind/                       # Projeto principal da aplicação WPF (.NET 8)
│   ├── Core/                       # Regras de negócio, serviços e modelos
│   │   ├── Enums.cs                # Tipos de ferramentas, mira, temas e provedores de IA
│   │   ├── Models/
│   │   │   ├── AppSettings.cs      # Configurações do usuário e estado
│   │   │   ├── AnnotationItem.cs   # Elementos vetoriais de anotação
│   │   │   ├── HistoryItem.cs      # Registro do banco de dados SQLite
│   │   │   ├── AiResult.cs         # Modelo de resposta de IA e fontes web
│   │   │   └── AiChatMessage.cs    # Mensagens de conversação multi-turno
│   │   └── Services/
│   │       ├── ScreenCaptureService.cs  # Captura multi-monitor com DPI Awareness
│   │       ├── CrosshairService.cs      # Renderização de miras e inversão de pixels
│   │       ├── ExportService.cs         # Renderização gráfica, PNG/JPG e Clipboard
│   │       ├── GeminiService.cs         # Cliente da API REST do Google Gemini
│   │       ├── OpenRouterService.cs     # Cliente da API REST oficial do OpenRouter
│   │       ├── OcrService.cs            # Reconhecimento local Windows.Media.Ocr
│   │       ├── HistoryService.cs        # Persistência SQLite local
│   │       ├── SettingsService.cs       # Criptografia DPAPI e persistência JSON
│   │       ├── HotkeyService.cs         # Gerenciamento de atalhos globais Win32
│   │       ├── ThemeService.cs          # Gerenciamento dinâmico de temas
│   │       ├── StartupService.cs        # Registro de autoinicialização no Windows
│   │       └── Logger.cs                # Auditoria local com sanitização de segredos
│   ├── UI/                         # Camada de apresentação gráfica (WPF / XAML)
│   │   ├── Theme/
│   │   │   └── Styles.xaml         # Estilos Glassmorphism, botões, cores e controles
│   │   ├── Views/
│   │   │   ├── CaptureOverlayWindow # Overlay de seleção rápida estilo Lightshot
│   │   │   ├── FullEditorWindow     # Janela do editor completo de imagem
│   │   │   ├── AiResultWindow       # Janela flutuante de chat e análise de IA
│   │   │   ├── HistoryWindow        # Navegador de histórico de capturas
│   │   │   ├── PinnedCaptureWindow  # Janela de captura fixada na tela
│   │   │   └── SettingsWindow       # Configurações organizadas em abas
│   │   └── TrayIconManager.cs      # Integração com a bandeja do sistema
│   ├── App.xaml / App.xaml.cs      # Ciclo de vida da aplicação (Single Instance Mutex)
│   ├── app.manifest                # Manifesto de DPI Awareness PerMonitorV2
│   ├── app.ico                     # Ícone oficial da aplicação
│   └── ShotMind.csproj             # Arquivo do projeto (.NET 8, WPF, WinForms, WinRT)
├── ShotMind.Tests/                 # Projeto de testes automatizados MSTest
│   ├── UnitTest1.cs                # 51 testes unitários, de integração e renderização
│   └── ShotMind.Tests.csproj       # Configuração do projeto de testes
├── ShotMind.sln                    # Solução do Visual Studio
├── installer.iss                   # Script oficial do Inno Setup para gerar o .exe
├── app.ico                         # Ícone utilizado pelo instalador
├── .gitignore                      # Regras completas de exclusão para Git/.NET
├── .env.example                    # Exemplo de configuração de ambiente
├── CONTRIBUTING.md                 # Guia de contribuição para colaboradores
├── SECURITY.md                     # Política de segurança e reporte de vulnerabilidades
├── LICENSE                         # Termo sobre o licenciamento do software
└── README.md                       # Apresentação e documentação do projeto
```

---

## 🛠️ Tecnologias Utilizadas

- **Linguagem:** C# 12
- **Framework:** .NET 8.0 (com Target Framework `net8.0-windows10.0.19041.0`)
- **Interface Gráfica:** Windows Presentation Foundation (WPF) com estilo Glassmorphism
- **APIs Nativas do Windows:**
  - `Windows.Media.Ocr` para reconhecimento óptico de caracteres 100% local.
  - `User32.dll` (`RegisterHotKey`, `GetDpiForMonitor`, `BitBlt`) para captura de baixo nível e atalhos globais.
  - `System.Security.Cryptography.ProtectedData` (Windows DPAPI) para segurança de chaves.
- **Banco de Dados:** `Microsoft.Data.Sqlite` 10.0 (SQLite embutido e isolado)
- **Empacotamento:** Inno Setup 6 / 7 para criação do instalador executável autônomo.

---

## 💻 Requisitos do Sistema

- **Sistema Operacional:** Windows 10 (versão 2004 / Build 19041 ou superior) ou Windows 11.
- **Arquitetura:** x64 (64 bits).
- **Ambiente de Desenvolvimento:** .NET SDK 8.0 instalado.

---

## 🚀 Como Compilar e Executar

### 1. Clonar o Repositório
```powershell
git clone https://github.com/CrankosEnterprise/cranko-shot.git
cd cranko-shot
```

### 2. Compilar a Solução
```powershell
dotnet build ShotMind.sln
```

### 3. Executar os Testes Automatizados
```powershell
dotnet test ShotMind.sln
```
> O projeto inclui 51 testes automatizados cobrindo criptografia DPAPI, geração de nomes de arquivo, inversão de cores da mira, validação de catálogo do OpenRouter e renderização visual da interface.

### 4. Iniciar o CrankoShot
```powershell
dotnet run --project ShotMind/ShotMind.csproj
```

---

## 📦 Como Gerar o Instalador Oficial (.exe)

O instalador é gerado utilizando o script [`installer.iss`](file:///c:/Users/joao.garcia/Desktop/App%20de%20Print/installer.iss) com o **Inno Setup**:

1. **Publicar a versão Release (Self-Contained x64):**
   ```powershell
   dotnet publish ShotMind/ShotMind.csproj -c Release -r win-x64 --self-contained true -o ./publish
   ```

2. **Compilar o Instalador Executável:**
   ```powershell
   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
   ```

O instalador final será gerado em:
`installer_output\CrankoShot-Setup.exe`

### Benefícios do Instalador:
- Instalação limpa e padronizada em `C:\Program Files\CrankoShot`.
- Configuração de atalho no Menu Iniciar e opção de inicialização automática com o Windows.
- Criação automática dos diretórios de dados em `%AppData%\ShotMind`.
- Desinstalador seguro que preserva a pasta de imagens do usuário.

### 🚀 Publicação de Novas Versões no GitHub:
Para publicar atualizações mantendo os links de download automático operacionais:
1. **Nome Padronizado:** Mantenha sempre o nome do instalador gerado como `CrankoShot-Setup.exe` (conforme configurado na diretiva `OutputBaseFilename=CrankoShot-Setup` do arquivo `installer.iss`).
2. **Anexo na Release:** Ao criar uma nova tag/release no GitHub (ex.: `v1.1.0`), anexe o arquivo `installer_output\CrankoShot-Setup.exe` na lista de assets.
3. **Persistência do Link:** O link principal de download direto:
   ```
   https://github.com/CrankosEnterprise/cranko-shot/releases/latest/download/CrankoShot-Setup.exe
   ```
   apontará automaticamente para o instalador da nova release assim que ela for publicada, sem exigir nenhuma edição manual no README.

---

## 🔑 Configuração dos Provedores de IA

O uso de IA é opcional. Para habilitar os recursos analíticos:

1. Clique com o botão direito no ícone do CrankoShot na bandeja do sistema e selecione **Configurações**.
2. Acesse a aba **IA & Provedores**.
3. Escolha o seu provedor principal:
   - **Google Gemini:** Insira sua chave obtida gratuitamente no [Google AI Studio](https://aistudio.google.com/) e selecione o modelo desejado (ex.: `gemini-2.5-flash`).
   - **OpenRouter:** Insira sua chave gerada no [OpenRouter](https://openrouter.ai/keys) e selecione um dos modelos gratuitos com suporte a visão (ex.: `google/gemma-4-31b-it:free`).
4. Clique no botão **Testar Conexão** para validar a chave e os limites da API.
5. Clique em **Salvar Configurações**. A chave será criptografada localmente no seu computador através da DPAPI do Windows e nunca será exposta em logs ou arquivos de texto puro.

---

## 🤝 Contribuições

Contribuições são bem-vindas! Consulte o arquivo [CONTRIBUTING.md](file:///c:/Users/joao.garcia/Desktop/App%20de%20Print/CONTRIBUTING.md) para detalhes sobre fluxo de branches, padrões de código e checklist de Pull Requests.

---

## 🔒 Segurança

Para reportar vulnerabilidades ou entender a arquitetura de proteção de dados e credenciais, consulte [SECURITY.md](file:///c:/Users/joao.garcia/Desktop/App%20de%20Print/SECURITY.md).

---

## 📄 Licença

A definição dos termos de licenciamento deste repositório está descrita em [LICENSE](file:///c:/Users/joao.garcia/Desktop/App%20de%20Print/LICENSE).
