# Ch2–Ch4 合併規劃：AST + Parser（運算式 + 陳述式）

## Context

Ch1 Lexer 已完成（`Lumen.Core.Lexing.Lexer.Tokenize()` → `IEnumerable<Token>`）。下一階段要把 token 流轉成 AST。
規格唯一來源是 repo 根目錄的 `agents.md`；`claude.md` / `gemini.md` 是指向它的 symlink（改 roadmap 只改 `agents.md`，**不要用 `sed -i` 之類會把 symlink 換成實體檔的工具碰另外兩個**）。本文件提到 claude.md 時即指 agents.md。
claude.md roadmap 原本拆成 Ch2 AST / Ch3 Parser 運算式 / Ch4 Parser 陳述式，使用者決定**一次規劃、分三個 milestone 實作並各自 commit**，
每個 milestone 結束都要 green（`dotnet build` 零 warning、`dotnet test` 全過、`dotnet format --verify-no-changes`、de-DE culture 也過）。

claude.md 已鎖定且**不得偏離**的設計：

- Pipe 契約：`Parser: IEnumerable<Token> → Program + IReadOnlyList<ParseError>`；錯誤只累積在 list，**不丟 exception**
- AST = 零依賴純資料：`abstract record` 階層、`IExpression` / `IStatement` marker interface、**無 Visitor、無 `Eval()`、無 code generator**
- 每個 node 攜帶 `Token`（source location）；`ToString()` 是 **canonical source printer**：輸出合法且語意等價的正規化原始碼（不保留原始空白 / 換行 / 可省略的分號），而非逐字還原
- Pratt parser：`PrefixParseFn` / `InfixParseFn` delegate + 兩張 `Dictionary<TokenType, …>`；`Precedence` enum 內容照 spec（Lowest=1 … Index）
- `**` right-assoc（`ParseExpression(precedence - 1)`）；`&&` / `||` 產生獨立 `LogicalExpression`
- panic-mode `Synchronize()`；`_position` 必須前進；ParseError 上限 100；`MaxNestingDepth = 200` 用 `try/finally` 遞減
- `break` / `continue` 在 loop 外 → parse error；`arr[0] := 1` → parse error
- 數值 parse 一律 `InvariantCulture`；`Int`/`Float` token 的 `Literal` 是原始文字，Parser 再 parse 一次
- 命名 `Method_Scenario_ExpectedResult`；Parser test 用 `[Theory]` + `[InlineData]` table-driven；不 mock

本次與使用者確認的三個文法決定：

1. **分號**：`ExpressionStatement` 的 `;` 可省略；`let` / 賦值 / `return` / `break` / `continue` 缺 `;` 即 ParseError
2. **`fn add(a, b) { … }`** desugar 成 `LetStatement(add, FunctionLiteral)`，`FunctionLiteral` 多一個 `string? Name`
3. **賦值是 `AssignStatement`**（非 expression）；`for` 的 init / update 子句用專屬方法解析不含結尾分號的簡單陳述式

---

## Package / 型別總覽

### `src/Lumen.Core/Ast/`（namespace `Lumen.Core.Ast`）

**`Node.cs`**

```csharp
public interface IExpression;          // marker
public interface IStatement;           // marker
public abstract record Node(Token Token);            // 所有 node 的共同基底，Token = 該 node 的「代表 token」
public sealed record Program(IReadOnlyList<IStatement> Statements);  // 不繼承 Node（沒有單一代表 token）
```

- 每個具體 node 都是 `public sealed record … : Node(Token), IExpression|IStatement`，並 **override `ToString()`**（record 預設 ToString 會印成員名稱，不能用）
- `Program.ToString()` = 各 statement 的 `ToString()` 以換行接起來。**每個 statement 的 ToString 都自帶結尾 `;`（或 `}`）**，所以相鄰的 statement 之間永遠有分隔符；不會出現 `a\n-b` 被重新解析成 `a - b`、或 `a\n(b)` 變成 call 的問題（Lexer 會丟掉換行）
- ToString 的 round-trip 契約：`Parse(Parse(src).ToString())` 零錯誤，且兩次 `ToString()` 相等（冪等）
- **注意**：record 內含 `IReadOnlyList<T>` 時 value equality 退化成 reference equality。**測試一律以 `ToString()` + type pattern 斷言**，不要 `Assert.Equal(nodeA, nodeB)`

**`Expressions.cs`**（同檔多個小 record，符合「小型 record 群組可共檔」）

| Node | 欄位 | ToString 形式 |
|---|---|---|
| `Identifier` | `string Name` | `x` |
| `IntegerLiteral` | `long Value` | `Token.Literal` |
| `FloatLiteral` | `double Value` | `Token.Literal`（直接用原始文字，避開 culture / 格式問題） |
| `StringLiteral` | `string Value` | `"…"`，**重新 escape** `\n \t \\ \" \0` |
| `BooleanLiteral` | `bool Value` | `true` / `false` |
| `NullLiteral` | — | `null` |
| `PrefixExpression` | `TokenType Operator, IExpression Right` | `(-x)` / `(!x)` |
| `InfixExpression` | `IExpression Left, TokenType Operator, IExpression Right` | `(a + b)`；Token = 運算子 token，符號用 `Token.Literal` |
| `LogicalExpression` | `IExpression Left, TokenType Operator /*And\|Or*/, IExpression Right` | `(a && b)` |
| `IfExpression` | `IExpression Condition, BlockStatement Consequence, BlockStatement? Alternative` | `if (c) { … } else { … }` |
| `FunctionLiteral` | `IReadOnlyList<Identifier> Parameters, BlockStatement Body, string? Name` | **一律**印匿名形式 `fn(a, b) { … }`；`Name` 只是 metadata（供 Ch8 call stack），不參與 ToString。宣告語法由 `LetStatement` 負責印（見下） |
| `CallExpression` | `IExpression Function, IReadOnlyList<IExpression> Arguments` | `f(a, b)`；Token = `(` |
| `ArrayLiteral` | `IReadOnlyList<IExpression> Elements` | `[1, 2]` |
| `HashLiteral` | `IReadOnlyList<KeyValuePair<IExpression, IExpression>> Pairs` | `{"a": 1, "b": 2}`（list 保 insertion order） |
| `IndexExpression` | `IExpression Left, IExpression Index` | `(arr[0])`；Token = `[` |

- Prefix / Infix / Logical / Index 的 ToString 一律加括號，讓 precedence test 用字串就能驗證結構
- `Operator` 用 `TokenType` 而非 string：Ch6 Evaluator 用 switch 比對 enum 比比對字串乾淨

**`Statements.cs`**

| Node | 欄位 | ToString 形式 |
|---|---|---|
| `LetStatement` | `Identifier Name, IExpression Value` | `let x := 5;`；**特例**：若 `Value is FunctionLiteral { Name: var n } && n == Name.Name`，印回宣告語法 `fn add(a, b) { … }`（不帶 `;`），否則 `let add := fn add(...)` 不是合法語法、round-trip 會失敗 |
| `AssignStatement` | `Identifier Name, IExpression Value` | `x := 5;` |
| `ReturnStatement` | `IExpression? Value` | `return x;` / `return;` |
| `ExpressionStatement` | `IExpression Expression` | `(x + 1);` — **一律印 `;`**（canonical form；`;` 對 expression statement 是可省略的，所以印出來永遠合法，且 block 尾端 `{ (n * 2); }` 語意不變） |
| `BlockStatement` | `IReadOnlyList<IStatement> Statements` | `{ s1 s2 }` |
| `WhileStatement` | `IExpression Condition, BlockStatement Body` | `while (c) { … }` |
| `ForStatement` | `IStatement? Init, IExpression? Condition, IStatement? Update, BlockStatement Body` | `for (init; cond; update) { … }`，例：`for (let i := 0; (i < 3); i := (i + 1)) { … }` / `for (; ; ) { }` |
| `BreakStatement` / `ContinueStatement` | — | `break;` / `continue;` |

- `ForStatement` 保留獨立 node（spec：不 desugar 成 while）
- `Init` 只可能是 `LetStatement` / `AssignStatement` / `ExpressionStatement`；`Update` 只可能是 `AssignStatement` / `ExpressionStatement`
- **for 子句的 render 契約**：`LetStatement` / `AssignStatement` / `ExpressionStatement` 三者各實作 `internal string RenderWithoutTerminator()`，自己的 `ToString()` = `RenderWithoutTerminator() + ";"`；`ForStatement.ToString()` 對子句呼叫 `RenderWithoutTerminator()`（透過 type switch），**禁止用 `TrimEnd(';')` 之類字串處理**。不另建 for-clause 專用 AST 型別，避免 Evaluator 多處理一種 node

### `src/Lumen.Core/Parsing/`（namespace `Lumen.Core.Parsing`）

**`Precedence.cs`**：`internal enum Precedence`（內容照 claude.md Part 4）+ `internal static class PrecedenceTable`，`Dictionary<TokenType, Precedence>`：

```
Or → LogicalOr | And → LogicalAnd | Eq NotEq → Equality | Lt Gt LtEq GtEq → Comparison
Plus Minus → Sum | Star Slash Percent → Product | StarStar → Power | LParen → Call | LBracket → Index
其餘 → Lowest
```

**`ParseError.cs`**：`public readonly record struct ParseError(string Message, int Line, int Column)`，`ToString()` → `[line L:C] message`（與 spec 錯誤格式一致）

**`Parser.cs`**：`public sealed class Parser`

```csharp
public Parser(IEnumerable<Token> tokens);        // 內部 ToList()；若末尾不是 Eof 補一個（Parser 是公開 API，不能假設 caller 一定來自 Lexer）
                                                 // 補的 Eof 座標 = 最後一個 token 的 (Line, Column + Literal.Length)；空 stream 則為 (1, 1)，避免錯誤訊息出現 0:0
public Program ParseProgram();
public IReadOnlyList<ParseError> Errors { get; }

private delegate IExpression? PrefixParseFn();
private delegate IExpression? InfixParseFn(IExpression left);
private const int MaxNestingDepth = 200;
private const int MaxErrors = 100;
private readonly List<Token> _tokens; private int _position;   // index-based，與 Lexer 同風格
private readonly List<ParseError> _errors;
private int _nestingDepth; private int _loopDepth;
private readonly Dictionary<TokenType, PrefixParseFn> _prefixFns;
private readonly Dictionary<TokenType, InfixParseFn> _infixFns;
```

核心方法（Ch3 先做運算式部分，Ch4 補陳述式）：

- `ParseProgram()`：`while (Current.Type != Eof && _errors.Count < MaxErrors)`；每輪記下 `_position`，呼叫 `ParseStatement()`；回 null 就 `Synchronize()`；**安全檢查**：若這輪 `_position` 沒動，記 internal error 並強制 `Advance()`
- `ParseStatement()` 依 `Current.Type` 分派：
  - `Let` → `ParseLetStatement()` + `ExpectSemicolon()`
  - `Fn` 且 `PeekNext.Type == Ident` → 具名函式 desugar 成 `LetStatement`（無需 `;`，因為 spec 範例 `fn add(a, b) { return a + b; }` 後面沒分號）
  - `Return` / `Break` / `Continue` / `While` / `For` → 對應方法（`break`/`continue` 在 `_loopDepth == 0` 時記 error）
  - `Ident` 且 `PeekNext.Type == Assign` → `ParseAssignStatement()` + `ExpectSemicolon()`
  - 其餘 → `ParseExpressionStatement()`：解析運算式後 **若** 下一個是 `;` 就吃掉（可省略）
  - `{` 在 statement 開頭不當 block，直接走 expression（hash literal）；v0 不支援裸 block statement
- `ParseSimpleStatement()`（供 `for` 子句共用）：let / 賦值 / expression 三選一，**不**消費 `;`；`ParseLetStatement` / `ParseAssignStatement` 本身就不消費分號，由 caller 決定要不要 `ExpectSemicolon()`
- `ParseForStatement()`：`for (` [init] `;` [cond] `;` [update] `)` block；三個子句都可空（看到 `;` / `)` 就是空）
- `ParseBlockStatement()`：`{` … `}`；碰到 Eof 未閉合 → error；body 進出 `_loopDepth` 由 while/for 控制
- `ParseFunctionLiteral()`：解析 body 前 **把 `_loopDepth` 存起來歸零，結束後還原**（`while (…) { fn() { break; } }` 的 `break` 必須報錯）
- **Nesting guard（單一機制）**：`private bool TryEnterNesting(Token at)` — `_nestingDepth++`，超過 `MaxNestingDepth` 記 error `nesting too deep` 並回 false；呼叫端 `try { … } finally { _nestingDepth--; }`。**`ParseExpression` 與 `ParseBlockStatement` 兩個遞迴入口都必須套**：所有遞迴（grouped expr、if、fn body、while/for body）都經過其中之一，因此 250 層 `(` 與 250 層 `while (true) {` 都會被同一個計數器擋下，不會 stack overflow。超過上限時回 null 且**只記一筆** error（上層看到 null 直接回 null，不再重複記錄）
- `ParseExpression(Precedence)`：標準 Pratt 迴圈；`Illegal` token → error；沒有 prefix fn → error `unexpected token '…'`
- Prefix fns：`Ident Int Float String True False Null Bang Minus LParen(grouped) If Fn LBracket(array) LBrace(hash)`
- Infix fns：算術/比較/相等 → `InfixExpression`；`And`/`Or` → `LogicalExpression`；`StarStar` → `ParseExpression(Power - 1)`；`LParen` → call；`LBracket` → index
- Int/Float 用 `long.TryParse` / `double.TryParse(…, NumberStyles.Integer|Float, InvariantCulture)`，失敗記 ParseError（不能假設 token 一定來自 Lexer）
- `Synchronize()`：先至少 `Advance()` 一次（除非 `Current` 已是 `RBrace` / `Eof`），然後一路吃到「剛吃掉 `;`」，或 `Current` 是 `Let Fn If While For Return` / **`RBrace`（不消費，留給 `ParseBlockStatement` 關閉 block，否則 recovery 會越過 block 邊界造成假性「缺 `}`」）** / `Eof`。`)` `]` 不當停止點：expression 層的錯誤已經回 null 冒到 statement 層，落單的 `)` 會在下一輪被當成 `unexpected token` 再前進一格
- `Expect(TokenType)` / `ExpectSemicolon()`：不符就記 error `expected ';' but found …` 回 false
- `DisplayToken(Token)`：錯誤訊息裡的 token 顯示規則 — `Eof` 固定顯示 `end of input`；`String` 顯示 `"…"`；其餘顯示 `'Literal'`。所有 error message 一律經過它，demo 與測試才一致
- `AddError(string, Token)`：超過 `MaxErrors` 就不再累積

### `src/Lumen.Repl/Program.cs`

把 token dump 換成：Lexer → Parser → 有錯印每筆 `ParseError`，否則印 `program.ToString()`。
**注意命名衝突**：top-level statements 會產生隱含的 `Program` class，直接寫 `Program p = …` 會綁到 REPL 自己；用 `using AstProgram = Lumen.Core.Ast.Program;` 別名。
多行輸入 / 括號續行屬 REPL Behaviour（後面章節），本階段不做。

---

## xUnit 測試規劃（`tests/Lumen.Tests/`）

資料夾對應 src：`Ast/` 與 `Parsing/`。既有的 `CultureTestSetup` 會在 CI 用 de-DE 跑，不必額外設定。

### `Ast/`（Milestone 1，手動建 node，不經 Parser）

| 檔案 | 測什麼 |
|---|---|
| `AstToStringTests.cs` | 每種 node 的 `ToString()`：`let x := 5;`、巢狀 infix 加括號 `((a + b) * c)`、StringLiteral 重新 escape、`fn add(a, b) { … }` 有/無 Name、HashLiteral 依 insertion order、ForStatement 三子句全空 `for (; ; ) { }`、`return;`、Program 多行 |
| `AstDependencyTests.cs` | claude.md 必要 test：用 reflection 掃 `Lumen.Core.Ast` namespace 所有型別，**遞迴**檢查 base type、property / field 型別、method 參數與回傳型別，且遇到 generic 型別要拆 `GetGenericArguments()`、陣列要取 `GetElementType()`（否則 `IReadOnlyList<Objects.X>` 外層是 `System.Collections.Generic` 會漏掉）；葉節點 namespace 只能是 `Lumen.Core.Ast` / `Lumen.Core.Tokens` / `System*`（現在必過，之後擋住 Ast 引用 Objects/Evaluation） |

### `Parsing/`（Milestone 2 運算式、Milestone 3 陳述式）

| 檔案 | 測什麼 |
|---|---|
| `ParserTestHelper.cs` | `Parse(string) → (Program, IReadOnlyList<ParseError>)`、`ParseExpression(string) → IExpression`（斷言零錯誤且剛好一個 ExpressionStatement）、`AssertNoErrors`。Lexer 已有同樣先例，重複次數必定 >3 |
| `ParserLiteralTests.cs` (M2) | `[Theory]` Int/Float/String/Bool/Null/Identifier → 正確 node 型別與 `Value`；`3.14` 在 de-DE 下 Value 仍是 3.14；手動塞一個 `Int` token 文字 `"99999999999999999999"` → ParseError 不 throw |
| `ParserOperatorTests.cs` (M2) | `[Theory]` 每個 prefix（`!` `-`）與 infix 運算子 → `InfixExpression.Operator` 是對應 `TokenType`；`&&` / `||` → `LogicalExpression` 且 **不是** `InfixExpression` |
| `ParserPrecedenceTests.cs` (M2) | `[Theory]` input → 預期 ToString：`a + b * c`→`(a + (b * c))`、`2 ** 3 ** 2`→`(2 ** (3 ** 2))`、`-a ** 2`→`((-a) ** 2)`（照 spec Prefix > Power）、`a \|\| b && c`→`(a \|\| (b && c))`、`a == b < c`、`(a + b) * c`、`f(a)(b)`、`arr[1][2]`、`a * [1, 2][0]`、`f(a + b, c * d)`、`!a == b`→`((!a) == b)` |
| `ParserCompositeTests.cs` (M2) | grouped、`if` 有/無 else、FunctionLiteral 參數 0/1/N 且 `Name == null`、Call 引數 0/1/N、Array 空/N、Hash 空/N 且順序、Index、巢狀 fn 內 if |
| `ParserStatementTests.cs` (M3) | `let`；賦值；`fn add(a, b) { … }` → `LetStatement` + `FunctionLiteral.Name == "add"` 且 ToString 印回 `fn add(a, b) { … }`；`let f := fn(a) { … };` → `Name == null`；`return x;` / `return;`；expression statement 有/無 `;`；block 尾端無 `;`；`while`；`for` 三子句全有 / 全空 / 部分（`[Theory]`）；`break` / `continue` 在 loop 內合法 |
| `ParserErrorTests.cs` (M3) | `let x := 5` 缺 `;` → 一筆 error 含 `expected ';'`；`let := ;;; @@@ let x := 1;` → 多筆 error、正常終止、且最後的 `let x := 1;` 仍成功解析；純亂碼 `@@@@@ ### $$$` 不 hang；250 層括號 → ParseError 不 crash；250 層 `while (true) {` → ParseError 不 crash（block 也走 nesting guard）；`{ let x := ; }` 之後仍能正確關閉 block（Synchronize 不吃 `}`）；第 3 行的錯誤訊息含 `[line 3:`；缺 `;` 於 EOF → 訊息含 `end of input`；`break;` 在 loop 外 → error；`while (true) { fn() { break; } }` → error；`arr[0] := 1;` → error；缺 `)` / `}` / `]`；`Illegal` token → error；150 個 `@` → **恰好 100** 筆 error；`+ 5` 無 prefix fn → error |
| `ParserIntegrationTests.cs` (M3) | claude.md Part 2 的完整範例程式解析零錯誤；**round-trip**：`Parse(src).ToString()` 再 parse 一次，**第二次 parse 也斷言零 error**（否則兩邊可能都是部分 AST 而假性相等），且兩次 `ToString()` 相等；案例要涵蓋相鄰 expression statement（`a\n-b`）、具名函式宣告、for 三子句 |

---

## Milestones 與 commit

| # | 範圍 | 新增檔案 | commit |
|---|---|---|---|
| 1 | AST | `Ast/Node.cs` `Ast/Expressions.cs` `Ast/Statements.cs`、`tests/Ast/*`、`docs/ch2-decisions.md` | `feat(ch2): ast` |
| 2 | Parser 運算式 | `Parsing/Precedence.cs` `Parsing/ParseError.cs` `Parsing/Parser.cs`（只有 ExpressionStatement + Pratt + depth + error recovery）、`tests/Parsing/{Helper,Literal,Operator,Precedence,Composite}Tests.cs`、REPL 改印 AST、`docs/ch3-decisions.md` | `feat(ch3): parser expressions` |
| 3 | Parser 陳述式 | `Parser.cs` 補 statement 分派 / for 子句 / loop depth / Synchronize 完整版、`tests/Parsing/{Statement,Error,Integration}Tests.cs`、`docs/ch4-decisions.md`、claude.md Roadmap 勾 Ch2–Ch4 | `feat(ch4): parser statements` |

每個 milestone 內部依 TDD：先寫該檔的 failing test → 實作 → green → 下一個。每個 milestone 結束**停下來報告**（Hard Rule 7）。
使用者已知合併範圍的 diff 會超過 500 行（Autonomy Boundary），以三次 commit 分攤。

**不動的檔案**：`Tokens/*`、`Lexing/*`、`tests/Lexing/*`、CI、`Directory.Build.props`。

## decisions.md 要記的重點（每章 3–5 bullet）

- ch2：ToString 是 canonical printer 不是逐字還原（expression statement 一律印 `;`、prefix/infix/index 一律加括號）；Operator 存 `TokenType` 不存 string；FloatLiteral ToString 直接用 `Token.Literal`；含 list 的 record 不依賴 value equality；for 子句用 `RenderWithoutTerminator()` 不用字串 trim；`LetStatement` 對同名 FunctionLiteral 印回宣告語法
- ch3：`-a ** 2` 依 spec 為 `((-a) ** 2)`（Prefix > Power，與 Python 不同，刻意照表）；delegate 宣告為 Parser 私有巢狀型別；Int/Float 用 TryParse 因為 Parser 是公開 API；單一 nesting guard 套在 ParseExpression 與 ParseBlockStatement 兩個遞迴入口；`DisplayToken` 統一錯誤訊息裡的 token 顯示
- ch4：分號混合規則；`fn name(...)` desugar 成 let + `FunctionLiteral.Name`；`AssignStatement` 為 statement、for 子句走 `ParseSimpleStatement`；`return;` 允許（Value 為 null）；FunctionLiteral 進出時歸零/還原 `_loopDepth`；Synchronize 以 `}` 為停止點但不消費；裸 block statement 不支援

---

## 驗證

每個 milestone：

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
LUMEN_TEST_CULTURE=de-DE dotnet test
dotnet run --project src/Lumen.Repl     # 手動 demo，把 input/output 附在報告裡
```

REPL demo 範例（M3 結束時）：

```
lumen> let x := 5; x + 1
let x := 5;
(x + 1);
lumen> 2 ** 3 ** 2
(2 ** (3 ** 2));
lumen> let x := 5
[line 1:11] expected ';' but found end of input
lumen> fn add(a, b) { return a + b; }
fn add(a, b) { return (a + b); }
lumen> for (let i := 0; i < 3; i := i + 1) { break; }
for (let i := 0; (i < 3); i := (i + 1)) { break; }
```
