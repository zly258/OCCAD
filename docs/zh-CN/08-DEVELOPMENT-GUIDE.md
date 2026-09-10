# 08 开发指南

## 1. 开发目标

OCCAD 的日常开发优先保证：

1. 交互正确。
2. 模型/显示一致。
3. Undo/Redo 原子。
4. Tool 退出后完全 neutral。
5. Native transient 无泄漏、无幽灵对象。
6. 扩展不破坏既有 owner 和依赖边界。

不要为了“代码看起来统一”而重构稳定路径；优先修复可复现问题和明确的架构债务。

## 2. 环境

以仓库文件为准：

- `.NET SDK`：`global.json`
- Solution：`OCCAD.sln`
- Core：`src/OCCAD.Core`
- Avalonia：`src/OCCAD.Avalonia`
- Bridge SDK：默认 `C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`

可通过 `OCCTCSHARPBRIDGE_SDK` 覆盖 Bridge SDK 路径。

Windows 主验证入口：

```powershell
git pull
.\build.ps1
```

运行：

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

Bridge SDK 带 portable runtime 时不需要单独 `OcctRoot`。

## 3. 标准开发流程

### 3.1 先定位责任层

开发前先回答：

- Domain？
- Interaction？
- Presentation？
- Persistence？

然后找到唯一 owner。不要直接从 UI handler 开始堆逻辑。

### 3.2 Core-first

业务行为优先进入 Core：

- Entity 语义 → `Entities`
- Document membership/presentation → `Document`
- Tool 生命周期 → `Interaction`
- Selection → `Selection`
- Snap → `Snapping`
- Grip → `Grips`
- History → `History`
- Layer → `Layers`
- Property transaction → `CadPropertyTransaction` / `CadTransaction`
- Action 注册 → `Actions` / `CadCoreRegistration`

Avalonia 只负责适配。

### 3.3 先写失败语义

任何会修改模型或 native presentation 的功能，编码前明确：

- 哪一步算 commit？
- 哪一步失败必须 rollback？
- History 在哪一步安装？
- Tool completion 是否属于 commit？
- transient 何时释放？
- notification observer 失败是否允许影响业务结果？

### 3.4 加回归测试

优先测试不变量，不测试内部实现细节。

例如：

- 一次用户操作只产生一个 Undo。
- invalid exact input 不产生 history。
- Cancel 后 Preview/Snap/Tracking/GripDrag 清空。
- observer 抛 recoverable 异常不会让已提交状态变成失败。
- native presentation failure 会触发模型 rollback。

### 3.5 最后接 UI

UI 代码只做：

- 参数收集。
- Core API 调用。
- notification 观察。
- visual state 显示。

不在 ViewModel/Controller 中复制事务或几何规则。

## 4. 编码规范

### 命名

- 类型：`CadXxx`。
- Manager 表示明确生命周期 owner，不把纯 helper 命名为 Manager。
- `TryXxx` 只用于失败属于正常分支的 API。
- `CaptureXxx` 返回 snapshot。
- `RestoreXxx` 恢复 snapshot。
- `PublishXxx` 表示 notification，不应暗含模型 mutation。
- `StateChanged` 保留给内部严格传播时使用。

避免：

- `Advanced`
- `Extended`
- `New`
- `V1` / `V2`
- `Helper2`
- 含义不明确的 `Common` / `Util`

### 文件

- 一个主要 public/internal 类型一个文件，紧密小 record/enum 可共存。
- 超大 Controller 优先抽 ownership 清晰的小组件，而不是继续加 region。
- 不为“以后可能用”增加抽象层。

### 可见性

默认使用最小可见性。

- 只在程序集内使用 → `internal`。
- UI 不需要的 Core API 不应 `public`。
- public API 必须有稳定语义、参数校验和扩展文档。

## 5. 异常规则

### 参数错误

使用：

- `ArgumentNullException`
- `ArgumentOutOfRangeException`
- `ArgumentException`

### 状态错误

使用 `InvalidOperationException`，并说明哪个 invariant 被破坏。

### Recoverable native failure

只在明确的 presentation/transient cleanup 边界吞掉，并记录诊断。

### Fatal failure

至少以下不得作为普通 recoverable 错误吞掉：

- `OutOfMemoryException`
- `StackOverflowException`
- `AccessViolationException`

包装异常时，recoverable/fatal 分类必须递归检查 inner/aggregate exception。

## 6. Event 规则

新增事件必须在代码 review 中说明类别：

- veto/pre-state
- strict internal propagation
- post-state notification

公共 notification 默认逐 observer 隔离。不要直接使用 multicast invocation，除非该事件明确属于 strict/veto 契约。

## 7. Transaction 规则

- 单实体/少量实体高频编辑优先 lightweight history entry。
- 多步骤批量修改使用 `CadTransaction`。
- 不要在外层 transaction 内安装嵌套 Undo。
- no-op 不得污染 History 或 Modified state。
- History entry 的 Undo/Redo 自身必须考虑中途失败 rollback。

## 8. Native API 使用规范

每个创建出来的 native object 必须立即有 owner。

代码必须能回答：

- handle 存在哪里？
- 谁删除？
- engine 重建时怎么办？
- delete 失败时是否丢 handle？
- 失败后对象是否仍 selectable/visible？

临时 native object 不得只依赖 GC/finalizer。

## 9. 性能规则

高频 pointer path 避免：

- 每次 MouseMove 全量 Document snapshot。
- 不必要的 LINQ 大分配。
- 重复 BuildPresentation。
- 重复 redraw。
- 同一位置重复计算昂贵 snap 几何。

但不要通过缓存牺牲 invalidation 正确性。

## 10. 提交前检查

Core 改动至少检查：

- [ ] ownership 明确。
- [ ] no-op 不产 history。
- [ ] rollback 路径明确。
- [ ] Undo/Redo 对称。
- [ ] observer 语义正确。
- [ ] Tool Cancel/Deactivate neutral。
- [ ] transient 不进 Document/History。
- [ ] native handle 不丢失。
- [ ] 新增扩展点有测试和文档。

UI 改动额外检查：

- [ ] 没有复制 Core business logic。
- [ ] 125%/150% DPI 不破布局。
- [ ] Command Line / Tool Panel / Status Bar 没有重复完整 prompt。
- [ ] 本地化资源完整。

## 11. 不做什么

- 默认不引入 GitHub Actions。
- 不通过反射实现命令分发。
- 不建立 compatibility/migration 层来保留错误 API。
- 不为一个 bug 增加一套平行框架。
- 不在功能未稳定时横向扩大量 Entity 类型。
