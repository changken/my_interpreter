# Ch5 Object System — Decisions

- `Inspect()` 是唯一放在 `LumenValue` 上的 operation；集合內嵌字串要加引號，所以另有 `internal virtual InspectNested()`，只有 `StringValue` 覆寫（加引號並 re-escape）。`FloatValue.Inspect()` 用 `"R"` + InvariantCulture，輸出若只有數字與負號就補 `.0`（`1.0` 印 `1.0`、`1e16` 印 `10000000000000000.0`），確保永遠能跟 Int 區分。
- Value equality 照 spec 三分：scalar 用 record 預設；`ArrayValue` / `HashValue` 手動覆寫 `Equals` / `GetHashCode` 做 structural 比較（Hash 的相等與順序無關，hash code 用可交換加總）；`FunctionValue` / `BuiltinValue` 覆寫成 reference equality，`Environment` 完全不參與，closure 自我引用不會遞迴。注意 record 層的 `IntValue(1)` 與 `FloatValue(1.0)` **不相等**，`1 == 1.0` 為 true 是 Ch6 數值比較的語意，不是 record 相等。
- `HashValue` 不可變：`With(key, value)` 回新物件；內部 `Dictionary` + `List<key>` 保 insertion order，同 key 覆寫時位置不變。Key 只能是 Int / Float / Bool / String（`IsValidKey`），整數值的 Float key 正規化成 Int key，讓 `{1: "a"}[1.0]` 與 `1 == 1.0` 一致；`1.5` 與 `1` 仍是不同 key。
- `Environment`：`Define` 在本層（重複宣告直接覆蓋，方便 REPL 重跑一行）、`TryGet` / `TryAssign` 沿 scope chain 往外找、`TryAssign` 找不到回 false 不偷偷宣告（Ch7 據此產生 `undefined variable` ErrorSignal）。`Bindings` 只列本層且依序，供 REPL `.env` 使用。
- `Signal` 階層照 spec 是 `internal`，用 `InternalsVisibleTo("Lumen.Tests")` / `("Lumen.Repl")` 讓測試與 REPL 能判斷 `ErrorSignal`（之後 CLI 決定 exit code 70），不把 Signal 公開。`BuiltinFn` delegate 提前放在 Objects（`BuiltinValue` 需要它），registry 本身留 Ch6。
