# Ch1 Lexer — Decisions

- Lexer 回報錯誤一律用 `TokenType.Illegal` 塞進 token stream，沒有側邊 error list。原因：Pipe 契約鎖死 Lexer 輸出型別只能是 `IEnumerable<Token>`，這是唯一合法做法，不是自由選擇。
- 不合法的數字 literal（`1.`、`.5`、`10L`、`1_000`、`0x1F` 等）一律合併成單一 `Illegal` token，`Literal` 是整段原始文字，不拆成多個 token。用同一條規則（數字後緊跟識別字字元就是壞掉的字面值）同時涵蓋 suffix、底線分隔、誤寫的 hex/exponent 前綴，不必分別特判。
- 字串掃描遇到未知 escape 或未閉合（換行/EOF）時立即中止，回傳目前為止已解碼的內容當 `Illegal`，不嘗試跳過尋找下一個 `"`。游標停在出錯處，下一次 `NextToken()` 直接從那裡繼續。
- 識別字只認 ASCII `[A-Za-z_][A-Za-z0-9_]*`，不支援 Unicode 識別字；避免不必要的複雜度，之後有需要再開。
- `String` token 的 `Literal` 存**解碼後**的值（escape 已展開）；`Int`/`Float` 的 `Literal` 存原始數字文字，供 Ch3 Parser 用同樣的 `InvariantCulture` 規則再 parse 一次成實際數值。
