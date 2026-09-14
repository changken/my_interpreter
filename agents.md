# Lumen — Tree-walking Interpreter in C# .NET 10

## What this is

從零手刻一個直譯器，語言叫 **Lumen**，語法自行設計。
學習專案，**過程 > 結果**。v0 目標是 tree-walking interpreter，v1 之後可能接 bytecode VM。

架構路線：手刻 lexer → AST → Pratt parser → object system → evaluator + Environment。
TDD-first，零第三方依賴，每章結束都能跑。

---

## Hard Rules（違反就是 bug）

1. **零第三方依賴**。只用 BCL + xUnit。禁止 ANTLR / Irony / Sprache / Superpower / Pidgin / FluentAssertions。
2. **TDD-first**。先寫 failing test，再寫 implementation。沒有例外。
3. **一次一章**。不要提前實作 roadmap 後面的東西，即使你覺得很簡單。
4. **每章結束必須 green**：`dotnet build` 零 warning、`dotnet test` 全過。
5. **不要重構我沒要求的檔案**。改動範圍限制在當章。
6. 不確定設計方向時**先問我**，不要自己選一個然後做下去。
7. 每個步驟做完**停下來報告**，不要一路衝到底。

---

## 參考資料使用規範

實作過程可參考公開的 tree-walking interpreter 教學資源與 MIT 授權的參考實作。

**但：任何書籍正文、章節敘述、教學文字，一律不得複製或改寫進本專案的
code、註解、commit message、test 名稱或任何文件。**

只借**技法**，不借**文字**。演算法與架構模式不受著作權保護；書的散文受保護。
如果你發現自己在「把某段解說改寫一下貼進註解」，停手，用自己的話重寫，或乾脆不寫。

---

# Part 1 — Architecture

## Architecture Style（已定案）

**Pipeline (pipes and filters) 為主，Microkernel 為輔，單一 process 部署。**

```
string → [Lexer] → IEnumerable<Token> → [Parser] → Program → [Evaluator] → LumenValue
                                                                  ^
                                                         BuiltinRegistry (plugin)
```

### Pipe 型別契約（跨章節不得修改）

| Stage | Input | Output |
|---|---|---|
| Lexer | `string` | `IEnumerable<Token>` |
| Parser | `IEnumerable<Token>` | `Program` + `IReadOnlyList<ParseError>` |
| Evaluator | `Program` + `Environment` | `LumenValue` |

### Filter 隔離規則

- 每個 filter **不得引用下游 filter 的型別**（Lexer 不 import Ast；Parser 不 import Objects）
- 錯誤沿資料流傳遞，**不以 C# exception 跨 pipe**
- 每個 filter 必須能獨立測試，不需要 mock

### Evaluator 的例外

Evaluator 內部不是純 filter，而是 recursive tree walker + 可變 Environment。
這是刻意的混合設計，不要試圖把它改成無狀態 pipeline。

### Environment 所有權（關鍵）

`Environment` 由 **caller 持有**，以參數注入 Evaluator。
Evaluator **不得**在 `Eval()` 內部自行 `new Environment()`，否則 REPL 無法跨行保留變數。

簽章固定為：`LumenValue Eval(Program program, Environment env)`

### Microkernel: BuiltinRegistry

Builtin function 以 plugin 形式註冊，Evaluator **不得 hardcode 任何 builtin 名稱**。

```csharp
public delegate LumenValue BuiltinFn(IReadOnlyList<LumenValue> args);
```

新增 builtin = 註冊一行，Evaluator 零改動。

### 不採用

- **Microservices / Service-based**：無獨立部署、無團隊自治、無故障隔離需求
- **Space-based**：無 database、無並發、無 elasticity 需求
- **Event-driven**：pipeline 是同步單向，引入 event bus 只會讓 debug 變難

### 必要 test

- REPL 連續兩行：第一行 `let x := 1;`，第二行 `x + 1` 應得 `2`
- 新增一個 builtin 不需修改 Evaluator
- `Lumen.Core/Ast` 目錄下任何檔案不得 `using` Objects 或 Evaluation 命名空間

---

## Pattern 選用理由（避免之後被「改回去更 OO」）

### AST 求值 — 外部 pattern matching，不用 GoF Interpreter pattern

**不要**把 `Eval()` 定義在 AST node 上。理由有二：

1. **Expression Problem**：本專案 node type 數量會收斂（約 25 個），但 operation 會持續增加
   （Evaluator → AstPrinter → TypeChecker → Compiler）。行為放 node 上，每加一個 operation 就要改 25 個檔案。
2. **依賴方向**：`Expr.Eval(Environment)` 會讓 `Ast` reference `Objects`，
   使 Parser 間接依賴 runtime 型別，違反 filter 隔離規則。`Ast` 必須是零依賴的純資料。

### LumenValue.Inspect() — 用多型，放在型別上

`Inspect()` 是 `LumenValue` 上唯一會存在的 operation，不會再增加。
此處 Expression Problem 不成立，多型比外部 switch 乾淨。

### 判斷法則

- operation 固定、type 會增加 → 行為放型別上（多型）
- type 固定、operation 會增加 → 行為放外部（pattern matching）

---

## Layout

```
src/
  Lumen.Core/
    Lexing/        TokenType.cs, Token.cs, Lexer.cs
    Ast/           Node.cs, Expressions.cs, Statements.cs
    Parsing/       Precedence.cs, ParseError.cs, Parser.cs
    Objects/       LumenValue.cs, Signal.cs, Environment.cs
    Evaluation/    Evaluator.cs, Numeric.cs, BuiltinRegistry.cs, Builtins.cs
  Lumen.Repl/      Program.cs, Repl.cs
tests/
  Lumen.Tests/
    Lexing/  Parsing/  Evaluation/
  Lumen.Conformance/
    scripts/*.lumen
    ConformanceRunner.cs
    ATTRIBUTION.md
docs/
  chN-decisions.md
```

---

# Part 2 — Language Spec

## Lumen v0

語法為自行設計。**不要自作主張改成其他教學語言的語法。**

```
// 單行註解

let x := 5;
let pi := 3.14;
let name := "ken";
x := x + 1;                        // 賦值

fn add(a, b) { return a + b; }
let double := fn(n) { n * 2 };     // 無 return 時，最後一個 expression 為回傳值

if (x > 3) { "big" } else { "small" }

while (x > 0) { x := x - 1; }
for (let i := 0; i < 10; i := i + 1) { puts(i); }
break;  continue;

let arr := [1, 2, 3];
let map := {"a": 1, "b": 2};
arr[0];  map["a"];

puts(len(arr));
```

**Types**：`Int` / `Float` / `Bool` / `String` / `Null` / `Array` / `Hash` / `Function` / `Builtin`

**Keywords**：`let` `fn` `return` `if` `else` `while` `for` `break` `continue` `true` `false` `null`

**Prefix operators**：`!` `-`

**Infix operators**：`||` `&&` `==` `!=` `<` `>` `<=` `>=` `+` `-` `*` `/` `%` `**`

**Two-char tokens**：`:=` `==` `!=` `<=` `>=` `&&` `||` `**` `//`

---

## Numeric Model（已定案）

只有兩種數值型別：`Int`（內部 `long`）、`Float`（內部 `double`）。
**不區分 byte / short / int32 / int64，不區分 float / double。**
理由：dynamic typing 下這些沒有語意價值，只會製造 promotion 組合爆炸。

### Promotion

`Int op Int` → Int；任一邊是 Float → Float。

### 語意決策

- `/` 兩邊 Int → 整數除法（`5 / 2` 得 `2`）；任一邊 Float → `5.0 / 2` 得 `2.5`
- `%` 只接受 Int，Float 回 ErrorSignal
- `**` 兩邊 Int 且指數 >= 0 → Int；其餘 → Float
- Int 運算使用 `checked`，overflow 回 ErrorSignal，**不 wrap**
- `long.MinValue / -1` 亦視為 overflow
- `1 == 1.0` 為 `true`

### Division & Special Values

- Int `/` 0 或 `%` 0 → ErrorSignal "division by zero"（**不得讓 `DivideByZeroException` 逸出**）
- Float `/` 0.0 → ErrorSignal（**不產生 Infinity**）
- **v0 不暴露 NaN / Infinity 給使用者**：任何運算結果 `double.IsNaN` 或 `IsInfinity` → ErrorSignal

### Literal（Ch1 Lexer）

- `42` → Int；`3.14` → Float
- `1.` 與 `.5` **不合法**，小數點兩側都必須有數字
- 不支援 suffix（`10L` / `1.5f` 一律 lexer error）
- 不支援 `0x` / `0b` / 底線分隔（v1 再說）

### 實作約束（重要）

**禁止為每組 (型別, 型別, operator) 寫獨立 case。** 必須先 promote 再走單一運算路徑：

```csharp
(left, right) switch
{
    (IntValue a, IntValue b) => EvalIntInfix(op, a.Value, b.Value),
    _ when IsNumeric(left) && IsNumeric(right)
        => EvalFloatInfix(op, ToDouble(left), ToDouble(right)),
    _ => Error.TypeMismatch(op, left, right),
};
```

Ch6 若出現 4 條以上的數值型別組合分支，視為設計錯誤。

### 必要 test

- `2 ** 3 ** 2` 得 `512`（right-assoc，不是 64）
- `2 ** 10` 回傳 Int 不是 Float
- `5 / 2` 得 `2` 且 `5.0 / 2` 得 `2.5`
- `1 == 1.0` 為 true
- `long.MaxValue + 1` 回 ErrorSignal 而非 wrap
- `3.14 % 2` 回 ErrorSignal
- `1 / 0` 與 `1.0 / 0.0` 皆回 ErrorSignal

---

## Control Flow（已定案）

```
while (cond) { body }
for (let i := 0; i < 10; i := i + 1) { body }
for (; cond; ) { }          // 三個 clause 皆可省略
break;  continue;
```

- **`for` 保留為獨立的 `ForStatement` node，不 desugar 成 `while`**
  （desugar 會讓 `continue` 跳過 update clause，造成無窮迴圈）
- `for` 的 init clause 宣告的變數，scope 限於該 loop
- `break` / `continue` 出現在 loop 之外 → parse error
- if / while / for 的條件**必須是 Bool**，其他型別回 ErrorSignal（**不做 truthiness**）

### 必要 test

- `for` 中 `continue` 後 update clause 仍會執行（防無窮迴圈）
- `fn` 內 loop 中的 `return` 能跳出整個 function
- 巢狀 loop 的 `break` 只跳出內層
- `for` 的 `i` 在 loop 外不可見
- `while (1) { }` 回 ErrorSignal（條件非 Bool）

---

## Closure Capture（已定案）

Closure 捕捉的是 **binding（reference）**，不是 value snapshot。

`for` 的 init clause 所宣告的變數，**每次 iteration 建立新的 binding**
（語意等同 JS 的 `let`，非 `var`）。
實作：每輪 iteration 建立新的 Environment，把 loop 變數複製進去，
iteration 結束後把值寫回外層供 condition / update 使用。

### 必要 test

```
let fns := [];
for (let i := 0; i < 3; i := i + 1) { fns := push(fns, fn() { return i; }); }
fns[0]()   // 必須是 0
fns[2]()   // 必須是 2
```

- counter closure：兩個 `counter()` 實例的計數互不干擾

---

## String Semantics（已定案）

- 內部為 .NET `string`（UTF-16），immutable
- Escape：`\n` `\t` `\\` `\"` `\0`。**不支援** `\uXXXX`（v1 再說）
- 未知 escape → lexer error，**不得靜默放行**
- `len(str)` 回傳 **UTF-16 code unit 數**，非 grapheme 數。此為已知簡化，寫進 docs，不要試圖修正
- 字串不支援跨行（未閉合的引號遇到換行即為 lexer error）
- `+` 僅支援 String + String，其他組合回 ErrorSignal（**不做隱式轉型**）

---

## Mutability（已定案）

Array / Hash **不可變**。所有 builtin 回傳新物件，不修改原物件。

- `push(arr, x)` 回傳新 array
- `arr[0] := 1` **不合法**，parse error（v1 再考慮）

理由：closure capture 與 value equality 的語意會簡單非常多。

---

# Part 3 — .NET 特有陷阱（書上不會講，必須寫死）

## Recursion Limits（必須）

`StackOverflowException` 在 .NET **無法 catch**，會直接終止 process。
因此必須手動計數，不能依賴 runtime 保護。

- Evaluator 維護 `int _callDepth`，上限 `MaxCallDepth = 1000`
  超過回 ErrorSignal "call stack exceeded"，**不得拋 exception**
- Parser 維護 `int _nestingDepth`，上限 `MaxNestingDepth = 200`
  超過記入 ParseError 並停止解析該 expression
- depth 計數必須在 `try / finally` 中遞減，確保 error path 也會還原

### 必要 test

- `fn f() { return f(); } f();` 回 ErrorSignal，process 不得終止
- 250 層巢狀括號回 ParseError，不得 crash

---

## Culture Invariance（必須）

所有數值的 parse 與 format **一律使用 `CultureInfo.InvariantCulture`**。
禁止使用無參數的 `ToString()` / `double.Parse()` / `long.Parse()`。

- Lexer parse float：`double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture)`
- `Inspect()` format：`Value.ToString("R", CultureInfo.InvariantCulture)`
- Float 輸出必須保留小數點：`1.0` 印 `1.0` 而非 `1`（與 Int 區分）

### 必要 test

- test assembly 層級設定 `CultureInfo.CurrentCulture = new("de-DE")`，所有數值 test 仍須通過

---

## Value Equality（必須明確處理）

`record` 的自動 value equality 在 Array / Hash / Function 上是錯的：

- `List<T>` / `Dictionary<K,V>` 不 override `Equals`，會退化成 reference equality
- `FunctionValue` 若讓 `Environment` 參與比較，closure 自我引用會無限遞迴

規則：

| 型別 | 做法 |
|---|---|
| Int / Float / Bool / String / Null | record 預設 |
| Array / Hash | **手動 override `Equals` / `GetHashCode`**，structural 比較 |
| Function / Builtin | **override 成 reference equality**，`Environment` 不參與比較 |

### 必要 test

- `[1,2] == [1,2]` 為 true
- `let f := fn(){}; f == f` 為 true；`fn(){} == fn(){}` 為 false
- closure 自我引用的 function 做 `==` 不得 stack overflow

---

## Hash Determinism（必須）

`Dictionary<K,V>` 列舉順序未定義，會造成 flaky test。
`HashValue` 內部必須**保證 insertion order**（`Dictionary` + `List<key>` 或自行維護有序結構）。
`Inspect()` 與迭代一律依 insertion order 輸出。

**可作為 key 的型別**：Int / Float / Bool / String。
Array / Hash / Function **不可作為 key** → ErrorSignal "unusable as hash key"。

---

## Source Location（必須）

- 每個 AST node 攜帶 `Token` 或 `(Line, Column)`
- `ParseError` 與 `ErrorSignal` 一律包含 line / column
- 錯誤訊息格式固定：`[line 3:12] type mismatch: Int + String`
- Ch8 的 ErrorSignal 額外攜帶 call stack（function 名稱清單，最多 10 層）

### 必要 test

- 第 3 行的型別錯誤，訊息中必須出現 `line 3`

---

# Part 4 — Parser

## Parser Contract（Ch3 定案，之後不得修改簽章）

採用 **Pratt parser**（top-down operator precedence）。

```csharp
delegate IExpression? PrefixParseFn();
delegate IExpression? InfixParseFn(IExpression left);

Dictionary<TokenType, PrefixParseFn> _prefixFns;
Dictionary<TokenType, InfixParseFn>  _infixFns;

enum Precedence
{
    Lowest     = 1,
    LogicalOr,      // ||
    LogicalAnd,     // &&
    Equality,       // == !=
    Comparison,     // < > <= >=
    Sum,            // + -
    Product,        // * / %
    Power,          // **
    Prefix,         // ! -
    Call,           // ()
    Index,          // []
}
```

### 兩個必考題（模型最愛在這裡翻車）

**1. `**` 是 right-associative**
infix 處理要用 `ParseExpression(precedence - 1)`，不是 `ParseExpression(precedence)`。
Ch3 必須有 test 驗證 `2 ** 3 ** 2` 解析為 `(2 ** (3 ** 2))`。

**2. `&&` / `||` 產生獨立的 `LogicalExpr` node**
不可以跟 `InfixExpr` 共用，因為 evaluator 需要 short-circuit，走不同路徑。
Ch6 必須有 test 驗證 `false && boom()` 與 `true || boom()` 不得拋錯。

---

## Parser Error Recovery（已定案）

採用 panic-mode synchronization。

- 記錄 ParseError 後，**必須保證 `_position` 有前進**，否則無窮迴圈
- `Synchronize()`：一路吃 token 直到分號之後，或遇到
  `let` / `fn` / `if` / `while` / `for` / `return` 之一，或 EOF
- ParseError 上限 100 筆，超過即停止解析
- Parser 主迴圈加 safety check：若單輪未消耗任何 token，強制前進一格並記 internal error

### 必要 test

- `let := ;;; @@@ let x := 1;` 能回報多筆錯誤且**正常終止**
- 純亂碼輸入不得 hang

---

# Part 5 — Error Model

## 統一訊號機制（v0 定案，為 v1 的 try/catch 鋪路）

所有非正常控制流一律以 `Signal` 表示，**不使用 C# exception 跨越 evaluator**。

```csharp
internal abstract record Signal : LumenValue;
internal sealed record ReturnSignal(LumenValue Value)   : Signal;
internal sealed record BreakSignal                       : Signal;
internal sealed record ContinueSignal                    : Signal;
internal sealed record ErrorSignal(LumenValue Payload,
                                   int Line, int Column) : Signal;
```

### 強制寫法（違反即 bug）

Evaluator 每個子運算式求值後的檢查**必須寫成泛型形式**：

```csharp
var left = Eval(node.Left, env);
if (left is Signal) return left;        // OK
if (left is ErrorSignal) return left;   // 禁止：具名檢查
```

理由：v1 的 `throw` 只是帶 user payload 的 `ErrorSignal`。
寫成泛型檢查，v1 為純增量；寫成具名檢查，v1 要翻修整個 evaluator。

### 攔截規則

- loop 攔 `BreakSignal` / `ContinueSignal`，**放行** `ReturnSignal` / `ErrorSignal`
- function call 攔 `ReturnSignal`，**放行** `ErrorSignal`
- v0 中 `ErrorSignal` 無人攔截，一路冒泡到 REPL 頂層印出

### 必要 test

- Evaluator 原始碼中不得出現 `is ErrorSignal` 形式的傳播檢查
  （攔截點除外，且攔截點須明確註明）
- 深層巢狀運算式中的 error 能正確冒泡到頂層，且攜帶原始 line / column

---

## v1 Error Handling 規格草案（現在不實作，僅作為 v0 設計約束）

```
throw {"kind": "IOError", "msg": "file not found"};

try {
  risky();
} catch (e) when (e["kind"] == "IOError") {
  puts("io: " + e["msg"]);
} catch (e) {
  puts("other");
} finally {
  cleanup();
}
```

- **不做 exception class hierarchy**，可丟任何 `LumenValue`
- 選擇性攔截用 **exception filter**（`when` 子句），非型別比對
- **不做 `throws`（checked exception）**：需要 static analysis，且為公認的設計失誤
- `finally` 中出現 `return` / `break` / `continue` → **parse error**
- runtime error（type mismatch、除零等）**可被 catch**，與 user throw 同一機制

---

# Part 6 — Conventions

## C# Conventions（已定案，不要再問）

- .NET 10、`<Nullable>enable</Nullable>`、`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- file-scoped namespace，一個檔案一個主要型別（小型 record 群組可共檔）
- Token → `readonly record struct Token(TokenType Type, string Literal, int Line, int Column)`
- AST node → `abstract record` hierarchy。**不要 Visitor pattern**，用 switch expression + type pattern
- **不要寫 AST code generator**。C# 有 record，手寫就好
- Runtime value → `abstract record LumenValue`
- Parser 錯誤累積在 `List<ParseError>`，**不丟 exception**
- Evaluator runtime error 走 `ErrorSignal`，**不丟 exception**
- Lexer 用 `int _position` index-based。**禁止把 `ReadOnlySpan<char>` 存成 field**
- 公開 API 一律 explicit return type，不要用 `var` 遮
- 命名：`IExpression` / `IStatement` 為 marker interface，具體 node 為 record
- 禁止 `ILogger`、DI container、`IOptions`

---

## Testing Conventions

### Unit tests（Lumen.Tests）

- 一個 test method 測一件事，命名 `Method_Scenario_ExpectedResult`
- Parser / Evaluator test 用 `[Theory]` + `[InlineData]` 跑 table-driven
- 不要寫 test helper 抽象層，直到重複超過 3 次
- **不 mock**。這專案沒有需要 mock 的 I/O boundary

### Conformance tests（Lumen.Conformance）

端到端測試。每個 `.lumen` 腳本用註解標註預期輸出：

```
let counter := fn() {
  let n := 0;
  fn() { n := n + 1; return n; }
};
let c := counter();
puts(c());   // expect: 1
puts(c());   // expect: 2
```

Runner 逐檔執行、蒐集 `puts` 輸出、比對所有 `// expect:` 標記。
**Ch6 之後每章都要新增對應的 conformance case。**

`tests/Lumen.Conformance/ATTRIBUTION.md` 保留參考來源的 MIT license 全文與出處連結。
腳本**邏輯**可改寫自 MIT 授權的測試套件，syntax 改為 Lumen；**書中文字一律不得引用**。

---

## REPL Behaviour（已定案）

- 每次求值以 `CancellationToken` 包住，Ctrl+C 中斷回到 prompt，**不結束 process**
- Evaluator 在 loop 與 function call 的進入點檢查 cancellation
- `Environment` 由 REPL 持有並跨行保留
- expression statement 的結果**自動印出**；其他 statement 不印
- 內建指令：`.exit` / `.env`（列出目前 binding）/ `.clear`
- 多行輸入：括號未閉合時繼續讀下一行，prompt 變 `... `

---

## CLI（Ch8 補齊）

```
lumen              → REPL
lumen script.lumen → 執行檔案後結束
lumen -e "1 + 1"   → 求值單一 expression
```

Exit codes：`0` 正常／`65` parse error／`70` runtime error／`130` 使用者中斷

---

## CI（Ch1 建立後不再修改）

`.github/workflows/ci.yml`：

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
LUMEN_TEST_CULTURE=de-DE dotnet test    # 防 culture bug
```

PR 未全綠不得 merge。

---

# Part 7 — Process

## Roadmap

- [x] **Ch1 Lexer** — TokenType、手刻 char-by-char scanner、two-char token、註解、Int/Float literal、REPL 印 token stream
- [ ] **Ch2 AST** — node 型別階層、source location 欄位、`ToString()` 能還原原始碼
- [ ] **Ch3 Parser: Expressions** — Pratt 核心、precedence table、`**` right-assoc、`LogicalExpr`、nesting depth 限制、error recovery
- [ ] **Ch4 Parser: Statements** — `let` / assignment / `if` / `while` / `for` / block / `return` / `break` / `continue`
- [ ] **Ch5 Object System** — `LumenValue` hierarchy、`Signal`、`Inspect()`、`Environment` scope chain、value equality
- [ ] **Ch6 Evaluator: Expressions** — prefix / infix / numeric promotion / short-circuit / 除零與 overflow
- [ ] **Ch7 Evaluator: Statements & Functions** — 控制流、signal 傳播、call depth 限制、function value、closure capture
- [ ] **Ch8 Errors, Builtins & Composites** — BuiltinRegistry、`len` `first` `rest` `push` `puts`、array / hash / index / string、CLI

---

## Anti-patterns（別犯）

- 用 regex 寫 lexer → 手刻 char-by-char
- AST 用 `object` + 連續 `is` 判斷 → record + switch expression
- 寫 AST code generator → C# 有 record，不需要
- 用 Visitor pattern → 用 pattern matching
- 把 `Eval()` 定義在 AST node 上 → 見 Pattern 選用理由
- Parser 混用 exception 跟 error list → 只能用 error list
- signal 檢查寫成 `is ErrorSignal` → 必須 `is Signal`
- `**` 寫成 left-associative → 見 Parser Contract
- `&&` `||` 跟一般 infix 共用求值路徑 → 必須 short-circuit
- 為每組型別組合寫獨立 operator case → 先 promote 再單一路徑
- 無參數的 `ToString()` / `Parse()` → 一律 InvariantCulture
- 依賴 `StackOverflowException` → .NET catch 不到，必須手動計數
- 一口氣把整個 evaluator 寫完 → 一次一種 node type，配 test
- 自作主張加 `ILogger` / DI / `IOptions` → 不需要
- 把 Lumen 語法改回別的教學語言 → 語法是我設計的，不要動

---

## Non-goals（v0 明確不做，不要自作主張加）

- 效能優化：不做 constant folding、不做 AST caching、不做 string interning
- GC / 記憶體管理：交給 .NET
- 模組系統 / import
- 標準函式庫（builtin 只有 spec 列出的那幾個）
- LSP / 語法高亮 / IDE 整合
- 型別註記與 type checker（v1 議題）
- `try` / `catch` / `finally` / `throw`（v1 議題）
- Class / OOP / inheritance（v1 議題）
- async / 並發

若你認為某項現在就該做，**先問我**，不要直接實作。

---

## Autonomy Boundary

implementation detail 可自行決定，但以下必須停下來問我：

- 要新增 NuGet package
- 要修改已定案的 conventions（Architecture / Parser Contract / Language Spec / Error Model）
- 要改動前一章已完成的檔案
- 兩個設計方向 trade-off 明顯、選錯成本高
- 單一 chapter 的 diff 超過 500 行

每章結束額外輸出 `docs/chN-decisions.md`：
記錄做了哪些選擇、為什麼、替代方案是什麼。**3-5 個 bullet，不要寫成論文。**

---

## Per-Chapter Definition of Done

1. 該章所有 unit test green
2. Ch6 起：conformance test 也 green
3. `dotnet build` 零 warning
4. de-DE culture 下 test 仍全過
5. REPL 能 demo 這章新增的能力（附實際 input / output）
6. 寫好 `docs/chN-decisions.md`
7. 更新本檔 Roadmap checkbox
8. commit：`feat(chN): <描述>`

---

## Commands

```bash
dotnet build
dotnet test
dotnet test tests/Lumen.Conformance
dotnet run --project src/Lumen.Repl
dotnet format --verify-no-changes
```