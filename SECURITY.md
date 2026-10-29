# Política de Segurança — CrankoShot

A segurança e a privacidade dos dados de captura e credenciais dos usuários são prioridades no desenvolvimento do **CrankoShot**.

---

## 🔒 Proteção de Credenciais e Chaves de API

1. **Armazenamento com Windows DPAPI:**
   - Todas as chaves de API cadastradas pelo usuário (como Google Gemini e OpenRouter) são criptografadas diretamente através da **Windows Data Protection API (DPAPI)** associada ao usuário atual do sistema operacional (`DataProtectionScope.CurrentUser`).
   - As chaves nunca são salvas em texto puro no disco (`%AppData%\ShotMind\settings.json`).

2. **Sanitização Automática de Logs e Telas de Erro:**
   - O serviço de auditoria (`Logger`) e os tratadores de resposta HTTP contêm filtros regex ativos que detectam e mascaram chaves (`AIza...`, `sk-or-...`, cabeçalhos `Bearer ...` e tokens) com a marcação `[CHAVE_PROTEGIDA]`.
   - As chaves de API nunca são exibidas completas na interface do usuário nem impressas em logs.

3. **Arquivos Privados:**
   - O arquivo de histórico SQLite (`history.db`) e os logs de depuração residem na pasta local do usuário (`%AppData%\ShotMind`) e estão devidamente ignorados no `.gitignore`, nunca devendo ser comitados ou versionados no Git.

---

## ⚠️ Diretrizes para Colaboradores

- **NUNCA comite chaves de API, tokens ou credenciais reais:**
  - Não insira chaves reais em arquivos de teste, código-fonte, scripts ou documentação.
  - Para testes unitários, utilize chaves fictícias já configuradas que são higienizadas e descartadas após a execução.
- **Não versionar arquivos de configuração pessoal:**
  - O arquivo `.gitignore` já bloqueia `.env`, `*.log`, `*.db`, pastas `bin/`, `obj/`, `publish/` e `installer_output/`. Mantenha essa integridade ao criar novos arquivos.

---

## 🛡️ Reportando Vulnerabilidades de Segurança

Se você descobrir uma vulnerabilidade de segurança no CrankoShot:

1. **NÃO crie uma issue pública no GitHub.**
2. Reporte a falha de forma privada e confidencial através de um **GitHub Security Advisory** no próprio repositório ou entre em contato diretamente com o proprietário do projeto.
3. Descreva:
   - Tipo de vulnerabilidade identificada.
   - Passos reproduzíveis ou prova de conceito mínima (PoC).
   - Impacto potencial no ambiente do usuário.
4. O time responsável analisará o relato e trabalhará em uma correção antes da divulgação pública.
