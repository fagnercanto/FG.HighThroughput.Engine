# High Throughput Ingestion Engine 🚀

Este projeto é uma Prova de Conceito (POC) interativa de arquitetura orientada a dados (ETL). O objetivo é resolver o maior gargalo de sistemas de alta volumetria (como processamento de SPED Contábil ou telemetria): **o travamento por estouro de memória (Out of Memory) e a paralisação de CPU pelo Garbage Collector (GC).**

O motor foi desenhado para processar arquivos monolíticos na escala de **20 milhões de linhas** em poucos minutos, garantindo vazão máxima de I/O de disco e rede, com alocação quase zero na memória RAM.

## ⚙️ Diferenciais Arquiteturais (O que avaliar neste código)

1. **Zero Allocation Parser:** Substituição de `string.Split` por iteradores customizados via `ref struct` e `ReadOnlySpan<char>`. Os dados são extraídos deslizando ponteiros de memória sobre a string original. Alocação na Heap reduzida a zero.
2. **Backpressure com Channels:** Implementação do padrão Produtor-Consumidor via `System.Threading.Channels` (Bounded). Se o banco de dados apresentar lentidão, a leitura em disco é suspensa mecanicamente, protegendo a RAM do servidor de estrangulamento.
3. **Data Access (Bypass de ORM):** Remoção intencional do Entity Framework Core ou Dapper. A persistência é feita via `ADO.NET` nativo utilizando `SqlBulkCopy` com `SqlBulkCopyOptions.TableLock`. Gravação direta em disco ignorando gargalos de transações unitárias (LDF).
4. **Dois formatos de ingestão, lado a lado:** um formato **Simples** (`Id|RawValue`), que representa o cenário de melhor caso (parsing trivial, contagem fixa de colunas), e um formato **SPED Contábil completo** simulado (registros `0000`/`I200`/`I250`/`9999`, com layout de colunas variável por tipo de registro e dispatch em tempo de parsing). Isso permite comparar de forma honesta a vazão "ideal" contra a vazão de um cenário fiscal realista.
5. **Frictionless Setup:** O projeto não exige configuração de containers Docker ou instâncias completas de SQL Server para rodar.

## 🖥️ Interface (Blazor Web App - Interactive Server)

A UI guia o usuário em 3 passos, cada um em um card com status ao vivo (badges, spinners e cronômetro monotônico baseado em `Stopwatch`, sem depender do relógio do sistema operacional):

1. **Gerar massa de teste** — escolha o formato (Simples ou SPED completo) via radio button, veja uma **prévia das 10 primeiras linhas** do formato selecionado antes de gerar, defina a quantidade de registros (1M a 20M) e acompanhe a geração em tempo real.
2. **Disparar ingestão** — executa o pipeline Produtor/Consumidor (leitura + parsing + bulk insert) sobre o arquivo gerado, com progresso de linhas lidas e registros inseridos.
3. **Registros persistidos** — uma **tabela paginada (20 registros por página)** consultando `dbo.IngestionData` diretamente no banco via ADO.NET, para o usuário confirmar visualmente que os dados foram realmente persistidos (e não apenas confiar em contadores em memória). A tabela é recarregada automaticamente ao final de cada ingestão.

A cada início da aplicação (F5), a tabela `dbo.IngestionData` é esvaziada via `TRUNCATE TABLE` (operação de metadados, quase instantânea mesmo após 20M+ linhas) para garantir um ambiente limpo a cada execução, e possui `PRIMARY KEY CLUSTERED` em `Id` para manter a paginação/contagem rápidas mesmo em grandes volumes.

Estilização via **Bootstrap 5** (restaurado localmente com LibMan, sem dependência de CDN em runtime).

## 🚀 Como Executar (Apenas um F5)

Desenvolvido para avaliação imediata por Tech Leads e Recrutadores.

1. Clone o repositório.
2. Abra a Solution no Visual Studio (Requisito: possuir o componente SQL Server LocalDB instalado, padrão no VS).
3. Pressione **F5**.

O sistema irá, de forma 100% autônoma:
- Conectar no seu `(localdb)\mssqllocaldb`.
- Criar o banco de dados `HighThroughputDB` e a tabela `dbo.IngestionData` caso não existam (com truncamento automático a cada execução).
- Restaurar as dependências client-side (Bootstrap) via LibMan.
- Subir a interface web interativa.
- Na tela, você poderá escolher o formato (Simples ou SPED completo), gerar uma massa de dados de teste local (1M a 20M de registros), iniciar o stress test de ingestão e conferir os dados persistidos na tabela paginada.

---
**Autor:** Fagner Canto dos Santos
**Cargo:** Engenheiro de Software Sênior | Arquiteto .NET
