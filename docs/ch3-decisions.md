# Ch3 Parser: Expressions — Decisions

- `-a ** 2` 解析成 `((-a) ** 2)`：precedence 表把 Prefix 放在 Power 之上，這與 Python（`-2**2 == -4`）不同，但是照 spec 的表刻意做的；要改必須先改 Parser Contract。`**` 右結合用 `ParseExpression(Power - 1)` 實作，測試 `2 ** 3 ** 2` → `(2 ** (3 ** 2))`。
- 單一 nesting guard（`EnterNesting()` + `try/finally` 遞減）同時套在 `ParseExpression` 與 `ParseBlockStatement` 兩個遞迴入口：所有遞迴（括號、call 引數、if / fn body）都經過其中之一，250 層 `(` 或 `[` 都被同一個計數器擋下。超過上限只記一筆 `nesting too deep`，往上一路回 null 不重複記錄。
- 「回 null = 已記過錯誤」是整個 Parser 的不變式：每個解析方法失敗時自己記錯誤然後回 null，呼叫端只負責往上傳，不再記一次。錯誤訊息裡的 token 一律經 `Display(Token)`（Eof → `end of input`、String 加雙引號、其餘 `'literal'`）與 `Describe(TokenType)` 統一格式。
- Panic-mode `Synchronize()`：吃到分號之後、或停在 `let fn if while for return` / `}` / Eof。`}` 不消費，留給 block 自己關閉。失敗的 statement 若完全沒消費 token，先強制吃掉一個，保證主迴圈每輪都有進展。副作用是連續無分隔的亂碼（`@ @ @`）只會回報第一個，這是 panic-mode 的預期行為。
- `Int` / `Float` literal 用 `TryParse` + `InvariantCulture`，失敗記 ParseError：Parser 是公開 API，token 不一定來自 Lexer，不能假設文字一定合法。缺 Eof 的 token 流會自動補一個，座標接在最後一個 token 之後（空流為 1:1），避免錯誤訊息出現 `0:0`。
