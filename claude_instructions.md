Atue como Desenvolvedor .NET Sênior especialista em alta performance e Zero Allocation.
Objetivo: Criar a estrutura base de uma POC de ingestão de dados massiva (20 milhões de registros) com interface web.
Nome da Solution: FG.HighThroughput.Engine

Stack obrigatória:
- .NET (versão mais recente disponível)
- Blazor Web App (Interactive Server) para a UI.
- Arquitetura de pastas limpa: /Core, /Infrastructure, /UI.

Requisitos Técnicos Críticos (Não crie a lógica completa agora, apenas as interfaces e o esqueleto):
1. O acesso a dados usará EXCLUSIVAMENTE ADO.NET com SqlBulkCopy (flag TableLock ativada). Não instale Entity Framework.
2. O parser de texto deve prever o uso de `ref struct` e `ReadOnlySpan<char>` para evitar alocações na Heap.
3. O fluxo Produtor-Consumidor usará `System.Threading.Channels` (Bounded Channel) para Backpressure.
4. O banco de dados alvo será o (localdb)\mssqllocaldb.
5. Crie no Program.cs uma rotina de inicialização automática (HostedService) que execute um script SQL bruto (CREATE DATABASE IF NOT EXISTS, CREATE TABLE IF NOT EXISTS) ao dar F5.

Tarefas imediatas:
- Gere os comandos da CLI do .NET (dotnet new) para criar a solution e os projetos.
- Crie o esqueleto da classe responsável pelo SqlBulkCopy na pasta /Infrastructure.
- Crie o esqueleto do serviço de background que configurará o banco local no startup.