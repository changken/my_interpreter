# Ch8 Errors, Builtins & Composites — Decisions

- 索引邊界：Array 下標只接受 Int，越界或負數 → `index out of range: N`（拼錯下標是 bug，不靜默）；Hash 缺 key → Null（查表本來就可能沒有），key 型別非法 → `unusable as hash key`；String / 其他型別不支援 `[]`。位置一律取 `[` token。
- Builtin 產生的 ErrorSignal 沒有位置（`Error.FromBuiltin` 給 Line 0），Evaluator 在 call site 用 `LocateBuiltinError` 補上 `(` token 的位置——這是一個標了 `// interception point` 的具名檢查。`BuiltinFn` 簽章維持 spec 的 `(IReadOnlyList<LumenValue>) → LumenValue`，builtin 本身不需要知道 Token。`len` 回 UTF-16 code unit 數（`len("👍") == 2`，已知簡化）；`first` / `rest` 空陣列回 Null；`push` / `rest` 都回新 Array，原物件不動。
- Call stack：`ErrorSignal.CallStack`（`init` 屬性，預設空）在 function call 出口附加當前函式名稱（`FunctionLiteral.Name`，匿名為 `<anonymous>`），最內層在前、上限 10 層；原始 line / column 不變。`Inspect()` 在第一行後每層印 `  at name`。這是 Evaluator 內第二個標了 `// interception point` 的具名 ErrorSignal 檢查；`call stack exceeded` 本身也會帶 10 層。
- Ctrl+C 用 `CancellationToken` + `OperationCanceledException`，不走 Signal：這是宿主中斷、不是 Lumen 程式的錯誤，也不該被 v1 的 try/catch 攔到。Evaluator 在 while / for 每輪與 function call 入口檢查。REPL 每次求值建新的 CTS、`Interrupt()` 取消它並印 `interrupted` 後回 prompt（CTS 不 dispose，避免與 Console 事件 thread 的 race）；非互動模式回 130。
- CLI（`Cli.Run`）：`-e <source>` 印結果（Null 不印）、`<script>` 執行檔案、其他都是 usage error。Exit code 除 spec 的 0 / 65 / 70 / 130 外，補 sysexits 慣例的 64（用法錯誤）與 66（檔案讀不到）。ParseError 與 ErrorSignal 都寫到 stderr，`puts` 寫到 stdout，讓 `lumen script.lumen > out.txt` 只拿到程式輸出。
