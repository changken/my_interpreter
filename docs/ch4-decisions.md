# Ch4 Parser: Statements — Decisions

- 分號採混合規則：expression statement 的 `;` 可省略（block 尾端運算式當回傳值、REPL 單行 `x + 1` 都靠這條），`let` / 賦值 / `return` / `break` / `continue` 缺 `;` 就是 ParseError；具名函式宣告與 `while` / `for` 以 `}` 結尾不需分號。替代方案「全部可省略」會讓 `let x := 1 let y := 2` 合法、錯誤訊息品質變差。
- `fn add(a, b) { … }` 是 `let add := fn(a, b) { … };` 的語法糖，parser 直接產出 `LetStatement` + `FunctionLiteral(Name: "add")`，不另建 `FunctionStatement`；Evaluator 少處理一種語意重複的 node，`Name` 留給之後 call stack 顯示。
- 賦值是 `AssignStatement`（statement）不是 expression：`a := b := c`、`if (x := 1)` 都不合法；目標只能是 `Ident`，`arr[0] := 1` 會在 expression 後面看到 `:=` 時回報 `invalid assignment target`。`for` 的 init / update 子句用 `ParseSimpleStatement()` 解析「不含結尾分號」的 let（僅 init）/ 賦值 / expression，讓 `i := i + 1` 能出現在 `)` 前面。
- `break` / `continue` 在 loop 外是 parse error：`_loopDepth` 在 while / for body 進出時加減；進入函式本體時歸零、離開時還原，所以 `while (…) { fn() { break; } }` 會報錯，而 `fn() { while (…) { break; } }` 合法。
- 頂層或 block 內不支援裸 `{ … }` block statement：statement 開頭的 `{` 一律當 hash literal 解析。`return;` 允許（`Value` 為 null），語意留給 Evaluator 定為 Null。
