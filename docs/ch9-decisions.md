# Ch9 Compound Assignment — Decisions

- `+=` `-=` `*=` `/=` 在 parser 直接 desugar 成 `x := x <op> e`（既有的 `AssignStatement` + `InfixExpression`），不新增 AST node、Evaluator 零改動。理由：求值語意、錯誤種類、位置回報都與一般 infix 完全相同，依「語意不同才開新 node」的原則（`LogicalExpression` 獨立是因為要 short-circuit）不夠格開新 node。代價：`ToString()` 印的是 desugar 後的 `x := (x + 1);`，不會還原成 `x += 1;`。
- 合成的 infix 運算子 token：位置取自 `+=` token、Literal 換成裸運算子，所以 `s -= 1` 的錯誤是 `[line L:C] type mismatch: String - Int`（指向 `-=`），而不是 `String -= Int`。Literal 用明確對照表而不是切掉結尾的 `=`：`Parser` 是公開 API，token 流不一定來自 Lexer。
- 賦值目標仍限定 bare identifier：`arr[0] += 1` / `a + b -= 1` / `1 *= 2` 一律 parse error `invalid assignment target`，與 `arr[0] := 1` 相同。`ParseBareExpressionStatement` 的檢查從只認 `:=` 擴成所有賦值運算子，否則 `+=` 會留在原地變成較差的錯誤。Array / Hash 維持不可變。
- 只做這四個：`%=` / `**=` 不是運算子（`%` `**` 後面的 `=` 是 Illegal token，parser 回報 `illegal token '='`）。`for` 的 init / update 子句也接受複合賦值（`ParseSimpleStatement` 與 `ParseStatement` 共用 `IsAssignOperator`）。
- Lexer 只在 `+` `-` `*` `/` 四個 arm 各多看一格 `=`；`/=` 不會跟 `//` 衝突，因為註解在 `SkipTrivia()` 就被吃掉；`**=` 掃成 `**` + Illegal。
