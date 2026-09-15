# Ch7 Evaluator: Statements & Functions — Decisions

- Scope 只在 function call 與 `for` 建立：`if` / `while` 的 block 與外層共用 Environment，block 內的 `let` 會留在外層。這比每個 block 都開 scope 簡單，且 v0 沒有 block-scoped 語意需求；`for` 例外是因為 spec 要求 loop 變數不可見於外部。`let` 重複宣告直接覆蓋；賦值找不到 binding 回 `undefined variable`，不隱式宣告。
- `for` 的 per-iteration binding 照 spec 實作：`loopEnv` 跑 init / condition / update，每輪建 `iterEnv` 把 `loopEnv` 本層 binding 複製進去跑 body，結束後只把「原本屬於 loopEnv 的名字」寫回。所以 body 裡對 `i` 的賦值 condition 看得到、closure 捕捉到的是自己那一輪的 `i`、body 裡的 `let` 不外漏。`while` 沒有這層，closure 看到最終值——這是刻意的差異，conformance `ch7-closure-in-for.lumen` 把兩者對照固定下來。
- Signal 攔截點只有三處且都是具名的：loop 攔 `BreakSignal` / `ContinueSignal`（`result is Signal and not ContinueSignal` 放行其餘）、function call 攔 `ReturnSignal`、`Eval(Program)` 出口拆頂層 `ReturnSignal`。`ErrorSignal` 在整個 Evaluator 沒有任何具名檢查，架構測試持續掃描。
- Call depth 上限 1000 在 `CallFunction` 用 `try/finally` 維護；實測 `fn f() { return f(); }` 與含運算式的 `1 + f(n) * 2` 在 xUnit 的 thread 上都能安全回 `call stack exceeded`，不需要調低。引數數量不符與呼叫非函式都是 ErrorSignal，位置取 call 的 `(` token。
- `Repl` 獨立成 class（注入 `TextReader` / `TextWriter`）方便測試：Environment 跨行保留、只有最後一句是 expression statement 且結果非 Null 才自動印、`.exit` / `.env` / `.clear`、多行續讀用 Lexer token 數括號（字串內的括號不會誤判）。執行前 `TrimEnd()` 掉緩衝的尾端換行，讓 EOF 錯誤的位置落在使用者實際輸入的那一行。
