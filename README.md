# High Throughput Ingestion Engine 🚀

Este projeto é uma Prova de Conceito (POC) interativa de arquitetura orientada a dados (ETL). O objetivo é resolver o maior gargalo de sistemas de alta volumetria (como processamento de SPED Contábil ou telemetria): **o travamento por estouro de memória (Out of Memory) e a paralisação de CPU pelo Garbage Collector (GC).**

O motor foi desenhado para processar arquivos monolíticos na escala de **20 milhões de linhas** em poucos minutos, garantindo vazão máxima de I/O de disco e rede, com alocação quase zero na memória RAM.

## ⚙️ Diferenciais Arquiteturais (O que avaliar neste código)

1. **Zero Allocation Parser:** Substituição de `string.Split` por iteradores customizados via `ref struct` e `ReadOnlySpan<char>`. Os dados são extraídos deslizando ponteiros de memória sobre a string original. Alocação na Heap reduzida a zero.
2. **Backpressure com Channels:** Implementação do padrão Produtor-Consumidor via `System.Threading.Channels` (Bounded). Se o banco de dados apresentar lentidão, a leitura em disco é suspensa mecanicamente, protegendo a RAM do servidor de estrangulamento.
3. **Data Access (Bypass de ORM):** Remoção intencional do Entity Framework Core ou Dapper. A persistência é feita via `ADO.NET` nativo utilizando `SqlBulkCopy` com `SqlBulkCopyOptions.TableLock`. Gravação direta em disco ignorando gargalos de transações unitárias (LDF).
4. **Frictionless Setup:** O projeto não exige configuração de containers Docker ou instâncias completas de SQL Server para rodar.

## 🚀 Como Executar (Apenas um F5)

Desenvolvido para avaliação imediata por Tech Leads e Recrutadores.

1. Clone o repositório.
2. Abra a Solution no Visual Studio (Requisito: possuir o componente SQL Server LocalDB instalado, padrão no VS).
3. Pressione **F5**.

O sistema irá, de forma 100% autônoma:
- Conectar no seu `(localdb)\mssqllocaldb`.
- Criar o banco de dados `HighThroughputDB` caso não exista.
- Subir a interface web interativa.
- Na tela, você poderá gerar uma massa de dados de teste local (1M a 20M de registros) e iniciar o stress test de ingestão.

---
**Autor:** Fagner Canto dos Santos
**Cargo:** Engenheiro de Software Sênior | Arquiteto .NET
