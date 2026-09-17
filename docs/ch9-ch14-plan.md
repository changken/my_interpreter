# Ch9–Ch14 規劃：Compound Assignment → Char → Builtins → Class

## Context

Ch1–Ch8（v0）全部完成。v1 補四項語言能力：複合賦值、`char` 型別、string / array builtin、最小 OOP。
`agents.md` 的 Non-goals 把 Class / OOP 與標準函式庫列為 v1 議題並要求先問過再做；本文件就是問過之後的結果。
仍然**一次一章**：每章 TDD、`dotnet build` 零 warning、de-DE 下 test 全過、REPL demo、`docs/chN-decisions.md`、`feat(chN)` commit。

本次與使用者確認的四個決定：

1. **OOP 只做最小 class**：fields + methods + `this` + `new`。不做 `extends` / inheritance、static、visibility modifier
2. **`char` 是獨立且可排序的型別**（`'a' < 'b'` 合法），`"abc"[0]` 回 Char（目前 String 不支援 `[]`）
3. **複合賦值目標只限變數**（`x += 1`），與 `AssignStatement` 一致；`arr[i] += 1` 不做——Array / Hash 維持不可變
4. **Builtin 只做「純資料進、純資料出」**：不做會回呼 Lumen function 的 `map` / `filter` / `reduce` / `sort(cmp)`（那需要改 `BuiltinFn` 簽章）；`map` / `filter` / `reduce` 維持 `ch8-map-reduce.lumen` 那樣的使用者層函式

順序：**Ch9 → Ch10 → Ch11 → Ch12 → Ch13（→ Ch14 選配）**。Ch9 最小（純 parser desugar）暖身；Ch10 先於 Ch11 讓 `last("abc")` 有 Char 可回；Ch11 是純 `Evaluation` 新增、Evaluator 零改動；Ch12/13 拆兩章壓 diff 大小，且 Ch12+13 不碰任何已定案規則；Ch14 是唯一真正的分岔點，獨立出來不擋前面。

各章都會改到 `TokenType.cs` / `Lexer.cs` / `Parser.cs` / `Evaluator.cs` 這些「前面章節已完成」的檔案——加 keyword、token、switch arm 本來就得改這些擴充點，與 Ch7/Ch8 往 Ch6 的 switch 加 arm 是同一回事；不重構無關邏輯。

---

## Ch9 — Compound Assignment（已完成）

- Token：`PlusEq` `MinusEq` `StarEq` `SlashEq`；Lexer 在 `+ - * /` 四個 arm 各 `Match('=')`
- **不加 AST node**：parser 把 `x += e` desugar 成 `AssignStatement(x, InfixExpression(x + e))`，合成的運算子 token 用 `+=` 的位置、裸運算子的 Literal；Evaluator 零改動
- `ParseStatement` / `ParseSimpleStatement`（for 子句）的 lookahead 改用 `IsAssignOperator`；`ParseBareExpressionStatement` 的 `invalid assignment target` 檢查同步擴成所有賦值運算子
- 細節見 `docs/ch9-decisions.md`

---

## Ch10 — `char`

- Token `Char`；`NextToken` 加 `'\''` 分派到新的 `ScanChar`（比照 `ScanString`：未閉合 / 空 `''` / 多字元 `'ab'` / 未知 escape 一律 Illegal；escape 支援 `\n \t \\ \' \0`）
- AST：`CharLiteral(Token, char Value)`，`ToString()` 還原成 `'a'` / `'\n'`
- Parser：`_prefixFns[TokenType.Char]`
- Object：`CharValue(char Value)`，`TypeName "Char"`，**record 預設 equality**（同 Int / String 那一列）；`Inspect()` 印裸字元，`InspectNested()` 印 `'a'`（比照 String 的裸 / 加引號）
- Evaluator：expression switch 加一 arm；`EvalInfix` 在泛型 `==` / `!=` fallback 之前加一段「兩邊都是 Char 的 `< > <= >=`」；`+` `-` 等其他運算自然掉到 `TypeMismatch` / `UnknownPrefix`。**Char 不進 `Numeric.cs`**（數值型別仍只有 Int / Float，不做 `'a' + 1`）；`EvalIndex` 加 `(StringValue, IntValue)` case，越界同 Array 規則，以 UTF-16 code unit 為單位（與 `len` 同一個已知簡化）
- 明確不做、留待再問：Char 當 Hash key（key 型別清單已定案）、`String + Char`（`+` 只接受 String + String 已定案）
- Conformance：`ch10-char-literals`、`ch10-string-index-char`；malformed literal 是 lexer error，放 unit test

---

## Ch11 — String / Array Builtins

只動 `Builtins.cs`（`BuiltinRegistry.Register` 一行一個）；Evaluator 零改動。沿用 `len` 的 list-pattern switch 與「同名依型別 overload」寫法，錯誤走 `Error.FromBuiltin` / `Error.WrongArity`。

核心（本章必做）：

| 函式 | 型別 | 備註 |
|---|---|---|
| `upper(s)` / `lower(s)` | String → String | `ToUpperInvariant` / `ToLowerInvariant`（de-DE 下不能出錯） |
| `trim(s)` | String → String | |
| `split(s, sep)` | String, String → Array | `sep` 為空字串 → error，不交給 .NET 的空分隔語意 |
| `join(arr, sep)` | Array, String → String | 元素必須全是 String，不做隱式 `Inspect()` |
| `contains(x, item)` | String,String → Bool；Array,any → Bool | ordinal 子字串 / `LumenValue.Equals` |
| `reverse(x)` | String → String；Array → Array | String 依 code unit |
| `last(x)` | Array → 元素或 Null；String → Char 或 Null | 與 `first` 對稱 |

Stretch（本章塞得下就做，否則拆 Ch11b）：`indexOf`（找不到回 `-1`）、`slice(x, start, end)`（嚴格邊界，越界即 error，不做 clamp）、`replace(s, from, to)`（`from` 空 → error）、`concat(a, b)`（Array only）、`sort(x)`（無 comparator；混型 → error；用 `OrderBy` 保 stable）。`sort` 需要新檔 `Evaluation/Ordering.cs`（`int? Compare(LumenValue, LumenValue)`，涵蓋 Int / Float / String / Char），**只給 `Builtins.cs` 用**，Evaluator 不引用，維持「加 builtin 不改 Evaluator」。

Conformance：`ch11-string-builtins`、`ch11-array-builtins`（+ stretch：`ch11-sort-slice-builtins`、`ch11-builtin-errors`）。

---

## Ch12 — Class I：`class`、fields、`new`、唯讀成員存取

目標：`class Point { x, y }`、`new Point(1, 2)`、`p.x`。沒有 method、沒有 `this`、沒有 mutation——instance 建構後不可變，與 Array / Hash 對稱，本章沒有開放的設計分岔。

- Token / keyword：`Class` `New` `Dot`；`"class"` `"new"` 進 `Lexer.Keywords`
- **Lexer 需要一個真正的修改**：目前 `NextToken` 把所有 `.` 都送進 `ScanNumberOrDot`，其「開頭是 `.`」分支一律回 Illegal。改法：該分支先看下一個字元，是數字就維持現在的 `.5` Illegal 行為，否則只吃 `.` 回 `Dot`。不碰「數字後面的小數點」那段，`1.5` / `1.` / `.5` 行為不變
- AST：`ClassLiteral(Token, Fields: IReadOnlyList<Identifier>, Name)`、`NewExpression(Token, ClassName, Arguments)`、`MemberExpression(Token, Object, Property)`。`class Point {…}` desugar 成 `LetStatement(Point, ClassLiteral(Name:"Point"))`，與 `fn name(){}` 的糖同一套；`LetStatement.ToString()` 多一個 arm
- Parser：statement dispatch 加 `case Class when PeekNext 是 Ident`；`ParseParameterList` 加 terminator 參數（比照 `ParseExpressionList(end)`）給 field list 重用；重複 field 名 → parse error；`_prefixFns[New]`、`_infixFns[Dot]`；`PrecedenceTable[Dot] = Precedence.Index`（沿用最高一級，不動已定案的 `Precedence` enum，`a.b.c` / `a.b()` / `a.b[0]` 靠 Pratt loop 自然左結合）；`IsSynchronizationPoint` 加 `Class`
- Object：`ClassValue(ClassLiteral Declaration, Environment Closure)`、`InstanceValue(ClassValue Class, Environment Fields)`，兩者都是 **reference equality**（比照 `FunctionValue`）。`Fields` 直接用 `Environment` 當儲存體（`TryGet` / `Define` / `TryAssign` 現成）；`InstanceValue.TypeName` 回 class 名稱，錯誤訊息才會是 `type mismatch: Point + Int`
- Evaluator：`ClassLiteral n => new ClassValue(n, env)`；`EvalNew`（查名 → 必須是 ClassValue → 求值引數 → arity 對 `Fields.Count` → 建平的 `Environment` 逐一 `Define` → `InstanceValue`）；`EvalMember`（必須是 InstanceValue → `Fields.TryGet`，找不到 → `UndefinedMember`）。`new` 不碰 `_callDepth`：建構不執行任何 Lumen function body
- `Error.cs` 新增：`NotAClass`、`MemberAccessNotSupported`、`UndefinedMember`
- Conformance：`ch12-class-basic`、`ch12-class-errors`

---

## Ch13 — Class II：method、`this`、method call

目標：class body 內的 `fn` method、method body 內的 `this`、`obj.method(args)`。仍**沒有 mutation 語法**：method 讀 `this.field`，「更新」用回傳新 instance 表達（`return new Counter(this.n + 1);`），與 `push` 回新 array 同一精神。

- Token / keyword：`This`
- AST：`ClassLiteral` 多一個 `Methods: IReadOnlyList<FunctionLiteral>`（Ch12 記錄的計畫性延伸）；**`this` 不開新 node**——`_prefixFns[This]` 直接產生 `Identifier("this")`，`this.x` 就是普通 `MemberExpression`。因為 `this` 是 keyword 而非 Ident，`ExpectIdentifier` 自動擋掉 `let this := 1` / 參數或 field 叫 `this`，不用額外檢查。`CallExpression` 形狀不變：`obj.m(args)` 就是 `CallExpression(MemberExpression(obj, m), args)`
- Parser：把 `ParseFunctionDeclaration` 的核心抽成 `ParseNamedFunctionLiteral()`（純機械重構，唯一既有呼叫點行為不變）；`ParseClassDeclaration` 在 field list 之後 loop `fn name(...) {...}`；重複名稱檢查涵蓋 field + method；method 的 `FunctionLiteral.Name` 改成 `"Class.method"`（call stack 顯示用）
- Evaluator：**只擴充 `EvalMember`**——`Fields.TryGet` 找不到時查 `Class.Declaration.Methods`，找到就 `new Environment(Class.Closure)` + `Define("this", instance)`，回傳普通的 `FunctionValue`。於是 `obj.m(args)` 走既有 `EvalCall` / `CallFunction`，arity、`_callDepth`、`ReturnSignal`、cancellation、call-stack frame 全部免費繼承，不另做 `BoundMethodValue`
- Conformance：`ch13-class-methods`（兩個 instance 互不干擾）、`ch13-class-method-errors`、自呼叫 method 的 stack overflow 測試

---

## Ch14（選配，開工前需另行確認）— 可變欄位 `this.field := v`

四個已確認決定只要求 fields + methods + `this`，Ch12+13 已完整交付，**沒有**決定 instance 是否可變。這是計畫裡唯一「兩個方向取捨明顯、選錯成本高」的分岔，所以獨立成章、不預設答案。

- A（預設，做到 Ch13 為止）：不可變。與 Array / Hash 的不可變理由一致（closure capture 與 value equality 簡單）
- B（要真正的可變 OOP 狀態才做）：只支援 method body 內的 `this.field := v` / `this.field += v`（不支援外部 `obj.field := v`），用 4-token lookahead（`This Dot Ident 賦值運算子`，需要新增 `PeekAt(offset)`）在 `ParseStatement` 最前面攔，完全不動既有的 expression-statement 路徑。**這個要開新 node** `FieldAssignStatement`：求值語意真的不同（先解析 `this`、再 `Fields.TryAssign` 而非 `env.TryAssign`）。寫到未宣告的 field → `UndefinedMember`，不隱式新增欄位。`ForStatement.RenderClause` 刻意不擴充（for 子句改 `this.n` 列為 non-goal）

---

## 各章要碰 / 不碰的「封閉 switch」

| | Ch9 | Ch10 | Ch11 | Ch12 | Ch13 | Ch14 |
|---|---|---|---|---|---|---|
| `ForStatement.RenderClause`（未列型別會 throw） | 不改（沿用 AssignStatement arm） | — | — | 不改 | 不改 | 刻意不擴充 |
| `Synchronize` 的 `IsSynchronizationPoint` | 不改 | 不改 | — | **加 `Class`** | 可選加 `This` | 不改 |
| Evaluator `is ErrorSignal` 架構測試 | 無新具名檢查 | 無 | —（builtin 看不到 Signal） | 無 | 無 | 無 |
| Ast 不得引用 Objects / Evaluation | — | `CharLiteral` 純資料 | — | 三個新 node 純資料 | `Methods` 純資料 | `FieldAssignStatement` 純資料 |

整個計畫**不需要任何新的具名 `is ErrorSignal` 檢查或新的 interception point**。

## 明確不做（維持 agents.md 原狀）

inheritance / `extends`、static、visibility modifier、char 算術、`String + Char`、Char 當 hash key、`arr[i] := x`、會回呼的 builtin（`map` / `filter` / `reduce` / `sort(cmp)`）、型別註記、`try` / `catch` / `throw`。
