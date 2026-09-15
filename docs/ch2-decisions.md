# Ch2 AST — Decisions

- `ToString()` 是 canonical source printer，不是逐字還原：prefix / infix / logical / index 一律加括號，`ExpressionStatement` 一律印 `;`（對 expression statement 而言 `;` 可省略，印出來永遠合法）。這樣每個 statement 都自帶結尾，`Program` 用換行接起來時相鄰 statement 不會黏成另一個運算式（Lexer 會丟掉換行，`a` + `-b` 若沒分隔會變成 `a - b`），precedence test 也能直接比字串。
- 運算子存 `TokenType` 而不是 string；符號文字從 `Token.Literal` 取。之後 Evaluator 用 switch 比對 enum，比比對字串乾淨也不會打錯字。`FloatLiteral` / `IntegerLiteral` 的 ToString 直接印 `Token.Literal`，避開 culture 與 `"R"` 格式在邊界值的差異。
- `FunctionLiteral.Name` 只是 metadata（具名宣告 `fn add(...)` desugar 成 `LetStatement` 時填入，供之後 call stack 顯示），ToString 永遠印匿名形式；`LetStatement` 偵測到 `Value` 是同名 `FunctionLiteral` 才印回宣告語法。替代方案是獨立 `FunctionStatement` node，但那會讓 Evaluator 多處理一種語意上與 let 完全相同的 node。
- `for` 子句不能帶 `;`，所以 `LetStatement` / `AssignStatement` / `ExpressionStatement` 各提供 `internal RenderWithoutTerminator()`，`ForStatement` 用 type switch 呼叫；不用字串 `TrimEnd(';')`，也不另建 for-clause 專用 node。
- 含 `IReadOnlyList<T>` 的 record 其 value equality 會退化成 reference equality，測試一律用 `ToString()` + type pattern 斷言，不比較 node 本身。`Program` 沒有單一代表 token，所以不繼承 `Node`。
