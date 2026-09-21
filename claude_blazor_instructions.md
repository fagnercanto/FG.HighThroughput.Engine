# Instruções para Geração da Interface Blazor (POC Ingestão de Dados)

Atue como Desenvolvedor .NET Sênior especialista em Blazor.
Objetivo: Criar a interface gráfica (UI) principal do projeto FG.HighThroughput.Engine no arquivo `Home.razor` (ou `Index.razor`), utilizando renderização interativa no servidor (Interactive Server).

## Diretrizes de Layout e Comportamento
Crie uma interface baseada em Bootstrap (ou CSS nativo limpo) dividida em 5 seções claras. Utilize variáveis de estado no bloco `@code` para gerenciar a reatividade.

### 1. Seção de Geração de Massa (Mock)
- **Input:** Um `<input list="presets">` (datalist HTML) permitindo digitar números entre 0 e 20.000.000.
- **Presets no datalist:** 10000, 1000000, 5000000, 10000000, 20000000.
- **Ação:** Botão "1. Gerar Arquivo".
- **Comportamento visual:** Ao clicar, exibir um spinner e a mensagem: "Gerando arquivo em disco. Isso exige I/O e pode levar alguns minutos." Desabilitar o botão durante o processo.

### 2. Fila de Processamento (Metadados)
- **Tabela HTML:** Colunas: Arquivo | Tamanho (MB) | Quantidade de Linhas | Status.
- **Ação:** Botão "2. Iniciar Motor de Ingestão (Stress Test)".

### 3. Dashboard de Telemetria (Tempo Real)
- Crie cards ou labels para exibir métricas que serão atualizadas durante o processo via `InvokeAsync(StateHasChanged)`.
- **Métricas:** Tempo Decorrido (cronômetro), Registros Inseridos, Vazão (Linhas/Segundo), Uso de Memória (MB).

### 4. Validação de Dados
- **Label destacada:** Contagem total exata de registros inseridos no banco.
- **Grid de Preview:** Tabela simples exibindo apenas os TOP 100 registros (sem paginação complexa) para provar o sucesso do parser.

### 5. Gerenciamento
- **Ação:** Botão com estilo de perigo (vermelho) nomeado "Limpar Arquivos e Banco de Dados".

## Tarefas Imediatas
1. Gere o código completo do componente `.razor`, incluindo o bloco `@code` com as propriedades de estado necessárias.
2. Crie métodos vazios (stubs) para os eventos dos botões.
3. Estruture o HTML/CSS.
4. **Restrição:** Não implemente a lógica de persistência de dados agora. Crie apenas o esqueleto reativo da interface.