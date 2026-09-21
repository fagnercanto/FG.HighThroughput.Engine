# Build Guidelines: FG.HighThroughput.App

Act as a Senior .NET Software Engineer.
Objective: Create a NEW Blazor Web App project (Interactive Server rendering) within the current Solution. Do not alter or convert the existing Console project.

## 1. Project Infrastructure (Modular Monolith)
Create the project with the following internal folder structure:
- /Core: Will contain interfaces, data structures, and the ref struct parser (skeletons).
- /Infrastructure: Will contain persistence logic via SqlBulkCopy and database initialization.
- /Components/Pages: Will contain the main graphical interface (`Home.razor`).

## 2. User Interface (Home.razor)
Create a Bootstrap-based (or clean CSS) interface with the 5 sections below. Use state variables in the `@code` block and `InvokeAsync(StateHasChanged)` for reactivity.

**A. Mass Generation (Mock)**
- Input: `<input list="presets">` (HTML datalist) allowing values from 0 to 20,000,000.
- Presets: 10000, 1000000, 5000000, 10000000, 20000000.
- Action: Button "1. Generate File". Upon clicking, show a spinner and the text "Generating file on disk. This requires I/O and may take a few minutes". Disable the button during execution.

**B. Processing Queue (Metadata)**
- HTML Table with columns: File | Size (MB) | Row Count | Status.
- Action: Button "2. Start Ingestion Engine (Stress Test)".

**C. Telemetry Dashboard (Real-Time)**
- Reactive Cards/Labels for: Elapsed Time (stopwatch), Inserted Records, Throughput (Rows/Second), Memory Usage (MB).

**D. Data Validation**
- Label: Exact total count of records in the database.
- Preview Table: Display only the TOP 100 records (no deep pagination).

**E. Management**
- Danger-style button (red): "Clear Files and Database".

## 3. Immediate Tasks
1. Execute the command to create the new Blazor project.
2. Create the /Core and /Infrastructure folders.
3. Generate the complete code for `Home.razor` (HTML and @code block with states and button stubs).
4. Constraint: Create only the visual skeleton and empty event stubs. Do not implement disk access or database logic in this step.