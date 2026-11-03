# Guia de Contribuição — CrankoShot

Obrigado pelo interesse em contribuir com o **CrankoShot**! Este documento orienta sobre a preparação do ambiente de desenvolvimento, padrões de código e fluxo de envio de contribuições.

---

## 💻 Pré-requisitos do Ambiente

Para compilar, depurar e testar o CrankoShot, você precisará de:

1. **Sistema Operacional:** Windows 10 (versão 19041 ou superior) ou Windows 11.
2. **.NET SDK 8.0:** Com suporte a aplicações de desktop Windows (`Microsoft.WindowsDesktop.App` e `Microsoft.Windows.SDK.NET`).
   - Verifique executando: `dotnet --version`
3. **IDE recomendada:** Visual Studio 2022 (com a carga de trabalho *Desenvolvimento para desktop com .NET*) ou VS Code / Rider com extensões C#.
4. **Inno Setup 6 ou 7 (Opcional):** Apenas se você for compilar o instalador oficial `.exe`.

---

## 🚀 Como Começar

### 1. Clonar o Repositório e Criar uma Branch
```powershell
# Clone seu fork ou o repositório principal
git clone https://github.com/CrankosEnterprise/cranko-shot.git
cd cranko-shot

# Crie uma branch com nome descritivo
git checkout -b feature/nome-da-funcionalidade
# ou
git checkout -b fix/descricao-da-correcao
```

### 2. Restaurar Dependências e Compilar
```powershell
# Restaurar pacotes NuGet e compilar toda a solução
dotnet build ShotMind.sln
```

### 3. Executar os Testes Automatizados
O projeto conta com uma suíte de testes unitários e de integração (51 testes aprovados) cobrindo regras de negócio, criptografia DPAPI, geração de nomes de arquivo, inversão localizada de mira e integração de IA:
```powershell
dotnet test ShotMind.sln
```
> **Importante:** Todos os 51 testes devem passar com sucesso antes de abrir um Pull Request.

### 4. Executar a Aplicação Localmente
```powershell
dotnet run --project ShotMind/ShotMind.csproj
```

---

## 🛠️ Diretrizes de Código e Arquitetura

1. **Preservação de Padrões:**
   - Mantenha o estilo arquitetural em camadas do projeto: `Core/` (modelos, serviços, enums) e `UI/` (visualizações XAML, temas glassmorphism, estilos e controle de bandeja).
   - O projeto utiliza a convenção de namespace `ShotMind.*` para manter compatibilidade binária e atualizações do instalador.

2. **Segurança e Chaves de API:**
   - Nunca insira chaves reais nos arquivos de código ou testes.
   - Todo acesso a chaves deve passar pelo [`SettingsService`](file:///c:/Users/joao.garcia/Desktop/App%20de%20Print/ShotMind/Core/Services/SettingsService.cs) via Windows DPAPI.
   - Qualquer log que possa conter tokens deve utilizar `Logger.Sanitize`.

3. **Interface e Estilos:**
   - Mantenha o tema escuro moderno com glassmorphism definido em [`ShotMind/UI/Theme/Styles.xaml`](file:///c:/Users/joao.garcia/Desktop/App%20de%20Print/ShotMind/UI/Theme/Styles.xaml).
   - Respeite o DPI Awareness (`PerMonitorV2`) e garanta que componentes e formulários tenham altura padronizada (`36px` para campos) e quebra de linha (`TextWrapping="Wrap"`).

---

## 📦 Como Gerar o Instalador Windows

Se a sua alteração envolver novos arquivos ou dependências que precisem ser incluídos no instalador:

1. **Publicar a versão Release (Self-Contained x64):**
   ```powershell
   dotnet publish ShotMind/ShotMind.csproj -c Release -r win-x64 --self-contained true -o ./publish
   ```

2. **Compilar o Instalador Inno Setup:**
   ```powershell
   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
   ```
   O instalador gerado será salvo na pasta `installer_output\CrankoShot-Setup.exe`.

---

## 📋 Checklist para Pull Requests

Antes de submeter o PR, verifique se:
- [ ] O código compila sem erros (`dotnet build ShotMind.sln`).
- [ ] Todos os testes foram executados e passaram (`dotnet test ShotMind.sln`).
- [ ] Nenhuma chave de API, arquivo de log, banco de dados ou credencial pessoal foi incluída.
- [ ] Novas funcionalidades incluem testes automatizados correspondentes em `ShotMind.Tests`.
- [ ] A descrição do PR explica claramente o que foi alterado e o motivo.
