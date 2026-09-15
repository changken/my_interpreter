# Ch5–Ch8 規劃：Object System → Evaluator → Builtins / CLI

## Context

Ch1–Ch4 完成：`string → Lexer → IEnumerable<Token> → Parser → Program + IReadOnlyList<ParseError>`。
接下來四章把 AST 變成能跑的直譯器。規格來源是 `agents.md`（`claude.md` / `gemini.md` 是 symlink），以下只列 spec **沒定**而這次補上的決定；spec 已鎖定的（Signal 階層、`is Signal` 泛型檢查、`Eval(Program, Environment)` 簽章、數值 promotion、call depth 1000、closure 捕捉 binding、`for` 每輪新 binding、REPL / CLI 行為、exit code）不再重述，實作時直接照 spec。

本次與使用者確認的四個決定：

1. **Conformance 從 Ch6 開始**：Ch6 提前建立最小 `BuiltinRegistry`（delegate + `Register` / `TryGet`）與 `puts`，讓 `tests/Lumen.Conformance` 有輸出可比對；`len` / `first` / `rest` / `push` 留 Ch8
2. **`==` / `!=` 跨型別回 `false`**（不報錯）；`< > <= >=` 只接受數值，否則 ErrorSignal。**Hash key 依 `==` 正規化**：整數值的 Float key（`1.0`）正規化成 Int key，`{1: "a"}[1.0]` 找得到
3. **`let` 可重複宣告**（同 scope 直接覆蓋）；**賦值給未宣告變數 → ErrorSignal `undefined variable: x`**；賦值沿 scope chain 往外找到就改外層（counter closure 需要）
4. **Array 越界 / 負數下標 → ErrorSignal `index out of range`**；hash 缺 key → Null；`first([])` / `rest([])` → Null

其他實作層決定（不需再問，寫進各章 decisions.md）：

- `!` 只接受 Bool、`-` 只接受 Int / Float，其他 → ErrorSignal（延續「不做 truthiness、不做隱式轉型」）
- Int `/` `%` 用 C# 語意（截斷向零、餘數同被除數正負）
- 頂層 `return x;`：`Eval()` 出口把 `ReturnSignal` 拆成 `Value` 回傳；`Break` / `Continue` 不可能到頂層（parser 已擋）
- `Signal` 依 spec 是 `internal`，用 `InternalsVisibleTo("Lumen.Tests")` / `("Lumen.Repl")` 讓測試與 REPL 能判斷 `ErrorSignal`（決定 exit code 70）
- Builtin 不塞進 `Environment`：`Evaluator` 建構子收 `BuiltinRegistry`，識別字解析先查 env、找不到再查 registry。好處：`.env` 只列使用者 binding，`let len := 1` 可以 shadow，Evaluator 本身零 builtin 知識
- Ctrl+C 用 `CancellationToken` + `OperationCanceledException`：這是宿主中斷、不是 Lumen 錯誤，不走 Signal；REPL 攔下後回 prompt
- 「Evaluator 原始碼不得出現 `is ErrorSignal`」用測試強制：從 `AppContext.BaseDirectory` 往上找 `Lumen.slnx` 定位 `Evaluator.cs`，grep `is ErrorSignal`，允許清單只有標了 `// interception point` 的那幾行（Ch8 的 call-stack 附加點）

---

## Ch5 — Object System

### `src/Lumen.Core/Objects/`（namespace `Lumen.Core.Objects`）

**`LumenValue.cs`**

```csharp
public abstract record LumenValue
{
    public abstract string TypeName { get; }   // "Int" / "Float" / … 供錯誤訊息 "type mismatch: Int + String"
    public abstract string Inspect();
}
public sealed record IntValue(long Value)      // Inspect: InvariantCulture
public sealed record FloatValue(double Value)  // Inspect: "R" + InvariantCulture，且沒有小數點時補 ".0"（1.0 印 1.0）
public sealed record BoolValue(bool Value)     // 靜態 True / False 單例
public sealed record StringValue(string Value) // Inspect 印原字串（不加引號）；Array/Hash 內嵌時才加引號 → 用 InspectNested()
public sealed record NullValue                 // 靜態 Instance；Inspect "null"
public sealed record ArrayValue(IReadOnlyList<LumenValue> Elements)  // 手動 Equals/GetHashCode：逐元素 structural
public sealed record HashValue                 // 見下
public sealed record FunctionValue(FunctionLiteral Declaration, Environment Closure)  // Equals/GetHashCode 覆寫成 reference；Environment 不參與
public sealed record BuiltinValue(string Name, BuiltinFn Fn)  // reference equality；Inspect "<builtin len>"
public delegate LumenValue BuiltinFn(IReadOnlyList<LumenValue> args);
```

- `Inspect()` 是唯一放在型別上的 operation（spec：多型）；集合內嵌字串要加引號，所以再加一個 `internal virtual string InspectNested() => Inspect()`，只有 `StringValue` 覆寫成加引號版本
- `ArrayValue.Inspect` → `[1, "a", null]`；`HashValue.Inspect` → `{"a": 1, 2: true}`（insertion order）
- `FunctionValue.Inspect` → `fn(a, b) { … }`（借 AST 的 ToString）
- `HashValue`：內部 `Dictionary<LumenValue, LumenValue>` + `List<LumenValue> _keys` 保 insertion order；建構時經 `HashKey.Normalize(value)`：`FloatValue` 整數值 → `IntValue`，其他原樣；**只有 Int / Float / Bool / String 可當 key**，`TryGet` / `With(key, value)`（回新 HashValue，immutable）。Equals：key 集合相同且對應 value 相等（順序不影響相等）

**`Signal.cs`**：照 spec 逐字（`internal abstract record Signal : LumenValue` + 四個子型別）。Signal 的 `Inspect()`：`ErrorSignal` → `[line L:C] {Payload.Inspect()}`，其餘三個理論上不會被印，回 `<return>` 之類即可。`TypeName` 回 `"Error"` 等。

**`Environment.cs`**（`public sealed class Environment`，命名與 `System.Environment` 衝突 → Repl/測試用 `using LumenEnv = Lumen.Core.Objects.Environment;`）

```csharp
public Environment();                         // global
public Environment(Environment outer);        // enclosed
public void Define(string name, LumenValue value);   // 當前 scope，重複就覆蓋
public bool TryGet(string name, out LumenValue value);  // 沿 chain
public bool TryAssign(string name, LumenValue value);   // 沿 chain 找到就改；找不到回 false（caller 產 ErrorSignal）
public IReadOnlyList<KeyValuePair<string, LumenValue>> Bindings { get; }  // 只有本層，insertion order，給 .env 用
```

### `tests/Lumen.Tests/Objects/`

| 檔案 | 測什麼 |
|---|---|
| `InspectTests.cs` | `[Theory]` 每種值的 Inspect；`1.0` 印 `1.0`、`1E+16` 之類仍含小數點；de-DE 下 `3.14` 不變成 `3,14`；Array / Hash 巢狀字串加引號；hash insertion order |
| `ValueEqualityTests.cs` | `[1,2] == [1,2]` true、`[1,[2]] == [1,[2]]` true、`[1,2] != [2,1]`；`{a:1,b:2} == {b:2,a:1}` true；`fn == 同一個 fn` true、兩個不同 FunctionValue false；FunctionValue 內含自我引用的 Environment 做 `==` 不 stack overflow；`GetHashCode` 與 Equals 一致（相等的 Array 放進 HashSet 只算一個） |
| `HashValueTests.cs` | key 正規化：用 `1.0` 存、用 `1` 取；`1.5` 與 `1` 是不同 key；Bool / String key；Array / Hash / Function 當 key → `With` 回傳失敗（回 ErrorSignal 的產生放 Ch8，Ch5 只用 `bool TryWith` 或丟 `ArgumentException`？→ **決定：Ch5 提供 `static bool IsValidKey(LumenValue)`，`With` 假設合法**）；immutability：`With` 回新物件、原物件不變 |
| `EnvironmentTests.cs` | Define / TryGet；enclosed 找外層；內層 Define 遮蔽外層不影響外層；TryAssign 改外層；TryAssign 未定義回 false；重複 Define 覆蓋；`Bindings` 只含本層且依序 |

Commit：`feat(ch5): object system`

---

## Ch6 — Evaluator: Expressions

### `src/Lumen.Core/Evaluation/`（namespace `Lumen.Core.Evaluation`）

**`Evaluator.cs`**（`public sealed class Evaluator`）

```csharp
public Evaluator(BuiltinRegistry builtins);          // Ch8 再加 CancellationToken
public LumenValue Eval(Program program, Environment env);   // 出口：ReturnSignal → Value
private LumenValue Eval(IStatement stmt, Environment env);   // Ch6 只處理 ExpressionStatement，其餘 Ch7
private LumenValue Eval(IExpression expr, Environment env) => expr switch { … };  // 外部 pattern matching
```

Ch6 涵蓋的 node：所有 literal、`Identifier`（env → registry → ErrorSignal `undefined variable`）、`PrefixExpression`、`InfixExpression`、`LogicalExpression`（short-circuit：左邊不是 Bool → ErrorSignal；`false && x` 不求值 x）、`IfExpression`（條件非 Bool → ErrorSignal；無 else 且條件 false → Null）、`ArrayLiteral` / `HashLiteral`（元素逐一求值，任一 Signal 就冒泡；hash key 非法 → ErrorSignal `unusable as hash key: Array`）、`CallExpression` 只到「callee 是 BuiltinValue」（FunctionValue 呼叫是 Ch7）、`BlockStatement` 作為 if 分支（逐句求值，Signal 就中斷；最後一句的值為 block 值）。`IndexExpression` 留 Ch8。

錯誤訊息一律 `Error.Xxx(node.Token, …)` 工廠產生 `ErrorSignal(StringValue msg, Line, Column)`；`Inspect()` 印成 `[line 3:12] type mismatch: Int + String`。

**`Numeric.cs`**（`internal static class`）：spec 強制的「先 promote 再單一路徑」：

```csharp
static LumenValue Infix(TokenType op, LumenValue l, LumenValue r, Token at) => (l, r) switch
{
    (IntValue a, IntValue b) => IntInfix(op, a.Value, b.Value, at),
    _ when IsNumeric(l) && IsNumeric(r) => FloatInfix(op, ToDouble(l), ToDouble(r), at),
    _ => Error.TypeMismatch(...)
};
```

- `IntInfix`：`checked` 算術；`/` `%` 除零 → ErrorSignal `division by zero`；`long.MinValue / -1` → overflow；`**` 指數 ≥ 0 用 checked 平方求冪回 Int，指數 < 0 → 轉 Float 路徑；比較回 Bool
- `FloatInfix`：`%` → ErrorSignal（spec）；`/ 0.0` → ErrorSignal；任何結果 `IsNaN || IsInfinity` → ErrorSignal；`**` 用 `Math.Pow`
- 「Ch6 出現 4 條以上數值型別組合分支視為設計錯誤」→ 測試用 reflection 數 `Numeric.cs` 的 `(IntValue, IntValue)` pattern？做不到精確，改為 code review 項目寫進 decisions

`==` / `!=`：`Equality.AreEqual(l, r)`：兩邊都數值 → 數值比較（`1 == 1.0`）；否則 `l.Equals(r)`（record / 覆寫的 Equals）；型別不同 → false。字串 `+` 只接 String + String。

**`BuiltinRegistry.cs`**（最小版）：`Register(string name, BuiltinFn fn)`、`TryGet(string, out BuiltinValue)`、`Names`。**`Builtins.cs`**：`static BuiltinRegistry CreateDefault(TextWriter output)`，Ch6 只註冊 `puts`（每個引數 `Inspect()` 一行，回 Null）。

### `tests/Lumen.Tests/Evaluation/`

| 檔案 | 測什麼 |
|---|---|
| `EvalTestHelper.cs` | `Eval(string, TextWriter? output = null) → LumenValue`（parse 零錯誤斷言）；`EvalError(string) → ErrorSignal`；`AssertInt/Float/Bool/String/Null` |
| `EvaluatorLiteralTests.cs` | 各 literal；array / hash literal 元素求值；hash key 非法 → error |
| `EvaluatorNumericTests.cs` | spec 必要 test 全部：`2 ** 3 ** 2` = 512、`2 ** 10` 是 Int、`5 / 2` = 2、`5.0 / 2` = 2.5、`1 == 1.0`、`long.MaxValue + 1` error、`3.14 % 2` error、`1 / 0` 與 `1.0 / 0.0` error、`long.MinValue / -1` error、`2 ** -1` = 0.5、`0.0 / 0.0` 不產 NaN、`-7 / 2` = -3、`-7 % 3` = -1、`1e308 * 10` error（Infinity） |
| `EvaluatorPrefixTests.cs` | `!true`、`-5`、`-3.14`、`!5` error、`-"a"` error、`!!true` |
| `EvaluatorComparisonTests.cs` | `[Theory]` 六個比較運算子在 Int / Float / 混合；`"a" < "b"` error；`1 == "1"` false、`1 != "1"` true、`null == null` true、`[1] == [1]` true、`"a" == "a"` true |
| `EvaluatorStringTests.cs` | `"a" + "b"`；`"a" + 1` error `type mismatch: String + Int`；`"a" * 2` error |
| `EvaluatorLogicalTests.cs` | `true && false`、`false \|\| true`；**`false && boom()` 與 `true \|\| boom()` 不報錯**（boom 未定義）；`1 && true` error；`true && 1` error |
| `EvaluatorIfTests.cs` | 有 / 無 else、無 else 條件 false → Null、`if (1) {}` error、分支 block 最後一句為值、巢狀 if |
| `EvaluatorErrorTests.cs` | 錯誤訊息含 `[line 3:` 與正確 column；深層巢狀 `1 + (2 * (3 + "a"))` 的 error 冒泡到頂且 line/column 是 `"a"` 那個 `+`；undefined variable；error 之後同一 Program 後續 statement 不執行 |
| `EvaluatorArchitectureTests.cs` | `Evaluator.cs` 原始碼沒有 `is ErrorSignal`（除 `// interception point` 標記行）；`Lumen.Core.Ast` 型別不引用 `Objects` / `Evaluation`（Ch2 的 reflection test 現在真的有東西可擋了，確認仍 green） |
| `BuiltinRegistryTests.cs` | Register / TryGet；`puts(1, "a")` 寫兩行到 TextWriter 回 Null；`let puts := 1; puts` → 1（env 遮蔽 builtin） |

### `tests/Lumen.Conformance/`（新專案，xUnit，reference Lumen.Core）

- `ConformanceRunner.cs`：`[Theory] [MemberData(Scripts)]`，每個 `scripts/*.lumen` 一個 case：Lexer → Parser（有 ParseError 直接 fail）→ `Evaluator` + `Builtins.CreateDefault(StringWriter)` → 蒐集 `// expect: …` 標記（依出現順序）與 stdout 逐行比對；結尾允許一個 `// expect-error: <substring>` 標記，斷言最後結果是 ErrorSignal 且訊息包含該字串
- `scripts/ch6-arithmetic.lumen`、`ch6-comparison.lumen`、`ch6-logic-short-circuit.lumen`、`ch6-if.lumen`、`ch6-div-zero.lumen`（expect-error）
- `ATTRIBUTION.md`：說明腳本為本專案原創、未引用外部測試套件；若之後改寫 MIT 套件的邏輯再補 license 全文
- `Lumen.slnx` 加入專案；CI 的 `dotnet test` 會一併跑（solution 層級）

Commit：`feat(ch6): evaluator expressions`

---

## Ch7 — Evaluator: Statements & Functions

### 變更檔案：`Evaluator.cs`（補 statement 與 function call）、`src/Lumen.Repl/Repl.cs`（新）、`Program.cs`

Statement：

- `LetStatement` → `env.Define`；`AssignStatement` → `env.TryAssign` 失敗 → ErrorSignal `undefined variable: x`；兩者回 Null（statement 沒有值）
- `ExpressionStatement` → 值；`ReturnStatement` → `ReturnSignal(value ?? Null)`；`Break` / `Continue` → 對應 Signal
- `BlockStatement`（頂層 block 或函式 body）：每句求值，`is Signal` 就回傳；否則回最後一句的值（空 block → Null）。**同一個 block 不開新 scope**（v0 只有 function / for 建 scope；if / while body 與外層共用 — 寫進 decisions）
- `WhileStatement`：條件非 Bool → ErrorSignal；body 回 `BreakSignal` → 跳出回 Null、`ContinueSignal` → 下一輪、`ReturnSignal` / `ErrorSignal` → 放行（用 `is Signal` 再細分 Break / Continue 兩個具名攔截）
- `ForStatement`：照 spec 的 per-iteration binding：`loopEnv = new(env)` 跑 init；每輪 `iterEnv = new(loopEnv)`，把 loopEnv 本層 bindings 複製進 iterEnv，body 在 iterEnv 跑，結束後把 iterEnv 中**原本來自 loopEnv 的名字**寫回 loopEnv；再在 loopEnv 跑 update。`continue` 後 update 仍執行

Function：

- `FunctionLiteral` → `FunctionValue(literal, env)`（捕捉當前 env 的 reference）
- `CallExpression`：callee 求值 → `FunctionValue`：引數逐一求值（任一 Signal 冒泡）→ 數量不符 → ErrorSignal `wrong number of arguments: expected 2, got 1` → `_callDepth++`（> 1000 → ErrorSignal `call stack exceeded`，`try/finally` 遞減）→ `callEnv = new(closure)` 綁參數 → body → `ReturnSignal` 拆值（`// interception point`），其他 Signal 放行。callee 是 `BuiltinValue` → 直接呼叫。其他 → ErrorSignal `not a function: Int`
- 遞迴：`fn f() { return f(); } f();` → ErrorSignal 不 crash（測試必須跑在預設 1MB stack 的 xUnit thread，1000 層 × 每層 ~3 個 C# frame 要先實測是否安全；不安全就把 `MaxCallDepth` 降到 spec 允許的最大值並記錄）

REPL（`Repl.cs`，`public sealed class Repl(TextReader, TextWriter)` 方便測試）：

- `Environment` 由 Repl 持有，跨行保留；`Evaluator` 一個實例
- expression statement 結果自動印（`Inspect()`），其他 statement 不印；Null 結果也不印（避免 `let` 後噴 null）
- ErrorSignal → 印 `Inspect()`；ParseError → 每筆一行
- `.exit` / `.env`（列 `env.Bindings`：`name = Inspect()`）/ `.clear`（換新 Environment）
- 多行：括號 `(` `[` `{` 未閉合就繼續讀，prompt `... `（用 Lexer token 計數，不看字元，避免字串裡的括號誤判）
- Ctrl+C 與 CLI 留 Ch8

### `tests/Lumen.Tests/Evaluation/` 新增

| 檔案 | 測什麼 |
|---|---|
| `EvaluatorStatementTests.cs` | let / assign / assign 外層 / assign 未宣告 error / let 重複覆蓋 / 頂層 return 拆值 / block 最後一句值 / statement 回 Null |
| `EvaluatorLoopTests.cs` | spec 必要 test：`for` 中 `continue` 後 update 仍執行；巢狀 loop 的 `break` 只跳內層；`for` 的 `i` 在 loop 外不可見（`undefined variable`）；`while (1) {}` error；while 條件變 false 正常結束；break 回 Null |
| `EvaluatorFunctionTests.cs` | 呼叫 / 引數綁定 / 隱式回傳最後一句 / 明確 return / `fn` 內 loop 中 `return` 跳出整個 function / 參數數量錯 / 呼叫非函式 / 遞迴 fib(10)=55 / 無限遞迴 → `call stack exceeded` 不 crash / 高階函式（fn 當引數、回傳 fn） |
| `EvaluatorClosureTests.cs` | counter closure 兩個實例互不干擾（spec）；`for` 內建立的 closure 捕捉每輪 binding（`fns[0]() == 0`, `fns[2]() == 2` — 需要 `push`，Ch7 先用 `let f0 := …` 三個變數版本，Ch8 換成 push 版本）；closure 改外層變數外層看得到 |
| `ReplTests.cs` | 連續兩行 `let x := 1;` / `x + 1` 得 `2`（spec 必要）；let 不印；`.env` 列出 binding；`.clear` 後 undefined；多行括號續讀；parse error 不中斷 REPL |

### Conformance 新增

`ch7-closure-counter.lumen`、`ch7-for-continue.lumen`、`ch7-nested-break.lumen`、`ch7-recursion-fib.lumen`、`ch7-return-in-loop.lumen`、`ch7-stack-overflow.lumen`（expect-error）

Commit：`feat(ch7): evaluator statements and functions`

---

## Ch8 — Errors, Builtins & Composites

### 變更

- **`IndexExpression`**（`Evaluator.cs`）：`Array[Int]`（越界 / 負數 → ErrorSignal `index out of range: 5`）、`Hash[key]`（key 非法 → ErrorSignal `unusable as hash key`；缺 key → Null）、其他 → ErrorSignal `index operator not supported: String`
- **`Builtins.cs`** 補齊：`len`（String → UTF-16 code unit 數、Array → 元素數；其他 error）、`first` / `rest`（Array；空 → Null；`rest` 回新 Array）、`push(arr, x)` 回新 Array（immutable）、`puts` 已有。引數數量 / 型別錯 → ErrorSignal（builtin 內部產生 ErrorSignal 需要 line/column → `BuiltinFn` 簽章固定，改由 Evaluator 在呼叫點把 builtin 回傳的 `ErrorSignal(Line: 0)` 補上 call token 的位置：`// interception point`）
- **`ErrorSignal` 加 call stack**：`Signal.cs` 加 `IReadOnlyList<string> CallStack`（預設空），Evaluator 在 function call 出口若結果是 `ErrorSignal` 就 `with { CallStack = [name ?? "<anonymous>", ..existing] }` 上限 10（`// interception point`）。`Inspect()` 多印 `  at name` 行
- **`Evaluator` 加 `CancellationToken`**（建構子參數），loop 每輪與 function call 入口 `ThrowIfCancellationRequested()`
- **CLI**（`Program.cs`）：無參數 → REPL；`script.lumen` → 讀檔執行，exit 0 / 65（ParseError）/ 70（ErrorSignal）；`-e "expr"` → 求值印 Inspect；REPL 內 Ctrl+C → `CancellationTokenSource.Cancel()` 回 prompt，`Console.CancelKeyPress` 設 `e.Cancel = true`；REPL 結束 exit 0，被 SIGINT 結束 130
- Signal 對外：REPL / CLI 需要判斷 ErrorSignal → 已由 `InternalsVisibleTo` 解決

### 測試

| 檔案 | 測什麼 |
|---|---|
| `EvaluatorIndexTests.cs` | array 正常 / 越界 / 負數 / 非 Int 下標；hash 各種 key 型別 / 缺 key Null / `1.0` 找到 `1` 的值 / Array 當 key error；`"abc"[0]` error |
| `BuiltinTests.cs` | `[Theory]` len / first / rest / push 正常與錯誤；`push` 不改原陣列（`let a := [1]; let b := push(a, 2); a` 仍 `[1]`）；`len("héllo")` = 5、`len("👍")` = 2（UTF-16，spec 已知簡化）；**新增一個 builtin 不需改 Evaluator**：測試自己 `Register("twice", …)` 後可呼叫 |
| `ErrorCallStackTests.cs` | 三層呼叫的 error `Inspect()` 含 `at inner` / `at middle` / `at outer`；超過 10 層只保留 10 個；匿名函式顯示 `<anonymous>`；error line/column 仍是原始發生點 |
| `CliTests.cs` | 用 `Process` 或直接呼叫 `Cli.Run(args, stdin, stdout)` 回傳 exit code：正常 0、parse error 65、runtime error 70、`-e "1 + 1"` 印 `2` |
| `CancellationTests.cs` | `while (true) { }` 在 100ms 後 cancel → `OperationCanceledException`，evaluator 不 hang |

### Conformance 新增

`ch8-builtins.lumen`、`ch8-array-hash.lumen`、`ch8-index-errors.lumen`（expect-error）、`ch8-closure-in-for.lumen`（spec 的 `fns[0]()` / `fns[2]()` push 版本）、`ch8-call-stack.lumen`（expect-error 含 `at`）、`ch8-string-len.lumen`

Commit：`feat(ch8): errors, builtins and cli`

---

## 共同流程

每章：先寫該章所有 failing test → 實作 → 四項驗證（`dotnet build -warnaserror` / `dotnet test` / `dotnet format --verify-no-changes` / `LUMEN_TEST_CULTURE=de-DE dotnet test`）→ REPL demo → `docs/chN-decisions.md`（3–5 bullet）→ 勾 `agents.md` roadmap（只改 `agents.md`，symlink 不碰）→ commit → 停下報告。

Ch5 diff 預估 < 500 行；Ch6 / Ch7 / Ch8 各自可能超過 500 行（Autonomy Boundary），若超過會在該章開始前先報預估。

**不動的檔案**：`Tokens/*`、`Lexing/*`、`Ast/*`、`Parsing/*`（Ch8 若要為 ErrorSignal 取 call token 的位置，用 `CallExpression.Token` 已足夠，不改 AST）。例外：Ch8 依 spec 在 `Signal.cs` 加 `CallStack` 欄位、`Evaluator` 建構子加 `CancellationToken`。
