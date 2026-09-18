# Ch11 String / Array Builtins — Decisions

- 只動 `Builtins.cs`（`Evaluator` 零改動），沿用 `len` 的 list-pattern switch + `Error.FromBuiltin` / `Error.WrongArity` 寫法。8 個必做 builtin 全部照 `docs/ch9-ch14-plan.md` 的型別表：`upper` `lower` `trim`（String → String）、`split`（String, String → Array）、`join`（Array, String → String）、`contains`（String,String 或 Array,any → Bool）、`reverse`（String 或 Array）、`last`（Array 或 String，與 `first` 對稱）。
- `upper` / `lower` 用 `ToUpperInvariant` / `ToLowerInvariant`，不用無參數的 `.ToUpper()` / `.ToLower()`，避免 de-DE（或任何 culture）下的大小寫轉換差異。
- `split` 的空分隔字串明確擋在 list pattern 的第一個 arm（`[StringValue, StringValue { Value.Length: 0 }]`），不交給 .NET 的 `string.Split` 決定語意。
- `join` 需要「哪個元素不是 String」的逐一檢查，list pattern 表達不了迴圈，拆成獨立的 `JoinArray` 私有方法——這是 `Builtins.cs` 第一個需要 helper 方法的 builtin，之後 `slice` / `sort`（Ch11b）會延續這個模式。
- 型別錯誤訊息沿用既有慣例：兩個參數以上用「first/second argument to X must be TYPE, got TYPE」，單參數用「argument to X must be TYPE, got TYPE」；`join` 的元素錯誤用「argument to join: array element must be String, got TYPE」（plan 沒明講措辭，屬於實作細節）。
- **Stretch 拆到 Ch11b**：`indexOf` `slice` `replace` `concat` `sort`（含新檔 `Evaluation/Ordering.cs`）預估還要 300+ 行（`slice` 的三參數型別檢查矩陣、`sort` 的 stable comparator）才能達到與本章同等的測試嚴謹度，會讓這一章 diff 超過 `agents.md` 的 500 行門檻。與使用者確認後，本章只收斂必做的 8 個 builtin，stretch 五個獨立成 Ch11b，開工前需要使用者再給一次 go-ahead（與 Ch14 同樣的處理方式）。
- Conformance：`ch11-string-builtins.lumen`、`ch11-array-builtins.lumen`，各自在腳本尾端用一個 `expect-error` 驗證一個錯誤路徑（`split` 空分隔、`join` 元素型別錯）。其餘錯誤路徑（型別、arity）都在 `StringArrayBuiltinTests.cs` 的 unit test 覆蓋。
