# 08 开发指南

## 1. 目的

本文描述 OCCAD 的日常开发流程，不重复完整架构、Transaction、Build 或 Release 契约；这些分别以 `04`、`10`、`11`、`16` 为准。

日常开发优先级：

1. 交互正确；
2. 模型与显示一致；
3. Undo/Redo 原子；
4. Tool 退出后恢复 neutral；
5. 无 Native/transient 幽灵对象；
6. 每个有状态职责只有一个 owner；
7. Registration、UI、文档和真实行为一致。

## 2. 开发环境

仓库文件为权威来源：

- .NET SDK：`global.json`；
- Solution：`OCCAD.sln`；
- Core：`src/OCCAD.Core`；
- Avalonia：`src/OCCAD.Avalonia`；
- Bridge：OcctCSharpBridge SDK 3.0；
- Windows 默认 SDK：`C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`；
- Linux 默认 SDK：`~/.local/share/OcctCSharpBridge/SDK/3.0/linux-x64`。

通过 `OCCTCSHARPBRIDGE_SDK` 覆盖 SDK 位置。

Windows：

```powershell
git pull --ff-only
.\build.ps1
.\run.ps1
```

flat Bridge SDK 使用外部 OCCT runtime 时：

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

Linux：

```bash
git pull --ff-only
./build.sh
./run.sh
```

OCCAD 正常构建只消费已安装 Bridge SDK，不自动 clone、rebuild 或 sync Bridge 源码。

## 3. 标准开发流程

### 3.1 先判断修改属于哪一层

编码前先分类：

- 持久领域状态；
- Interaction 状态；
- Presentation/Input Adapter；
- Persistence/Exchange；
- Build/Runtime/Release Infrastructure。

然后确定唯一 owner。目录与落层规则见 [07 代码组织](07-CODE-ORGANIZATION.md)。

### 3.2 权威行为优先进入 Core

典型职责：

- Entity 语义 → `Entities/`；
- Document membership/presentation 同步 → `Document/`；
- Tool 生命周期和 transient ownership → `Interaction/`；
- Selection → `Selection/`；
- Snap → `Snapping/`；
- Grip → `Grips/`；
- Layer → `Layers/`；
- Property 语义 → `Properties/` + `CadPropertyTransaction`；
- Action 注册 → `Actions/` + `CadCoreRegistration`；
- 纯 Avalonia Control/Layout → `OCCAD.Avalonia`。

如果需求会修改正式 CAD 状态，不应从 UI handler 开始堆业务逻辑。

### 3.3 先定义修改语义

任何会改变状态的操作，编码前先明确：

- authoritative state 是什么；
- commit 点在哪里；
- 哪些失败必须 rollback；
- History 在何时安装；
- Tool completion 是否属于 commit；
- 哪些 transient 必须释放；
- Event 属于 veto、strict propagation 还是 post-state notification。

完整规则见 [10 事务、事件与 Native 资源契约](10-TRANSACTION-RESOURCE-CONTRACT.md)。

### 3.4 只实现最小完整路径

优先扩展现有 owner，不建立平行框架。

例如：

- 新增 Circle 绘制方式，应通过 initial parameter 激活既有 `CircleTool`；
- Entity appearance 修改应走 `CadPropertyTransaction`，而不是直接改 UI；
- 新修改命令只有在有真实交互 Tool 后才进入产品 Shell；
- 新 marker 必须有明确 transient/native owner。

### 3.5 最后接 Avalonia

Avalonia 负责：

- 收集输入；
- 调用 Core API/Action/Tool；
- 观察 Core notification；
- 显示校验错误；
- 构造 Control/Layout；
- 本地化和 DPI-safe presentation。

Avalonia 不复制 Transaction、Geometry、Selection、Layer 或 History 语义。

## 4. Property 开发

Property descriptor 和值语义属于 Core。

Entity Property 修改走 `CadPropertyTransaction`，保证多选修改原子、可撤销。

Appearance 规则：

- `ColorByLayer`、`LineStyleByLayer`、`LineWidthByLayer` 表示继承 Layer；
- 直接编辑 Color/LineStyle/LineWidth 时，在同一事务中关闭对应 ByLayer，形成 Entity **Override**；
- 重新启用 ByLayer 后恢复 Layer 继承；
- Viewer 根据最终 Core 状态刷新。

PropertyGrid 可以选择 TextBox、ComboBox、CheckBox、ColorTable 或复合 ByLayer editor，但 editor 选择只是 Presentation 职责。

## 5. Tool 开发

Tool 是分阶段状态机。新增 Tool 必须明确：

- 稳定 Tool ID；
- Stage 和 Prompt；
- 支持的 Exact Input；
- Preview；
- Backspace/StepBack；
- Enter/Space/右键结束语义；
- Cancel/Deactivate cleanup；
- Transaction/History 边界；
- Snap/Tracking/WorkPlane policy；
- 必要时的 Parameter descriptor。

Commit 和 Cancel 最终都必须恢复 neutral。

## 6. Action 与 Command 开发

Action 是稳定命令入口。

新增 Action 时：

1. 选择稳定分层 ID；
2. 在 `CadCoreRegistration` 注册；
3. 只给真实注册 Action 添加 Alias；
4. 本地化 Caption 与稳定 ID 分离；
5. 只有完整交互闭环可用后才放入 Menu/Toolbar；
6. 同步 [15 命令参考](15-COMMAND-REFERENCE.md) 与 [14 功能矩阵](14-FEATURE-MATRIX.md)。

禁止使用本地化文本作为 Action/Tool/Entity ID。

## 7. Entity 开发

持久 Entity 根据功能需要定义：

- Geometry 校验；
- Duplicate/Snapshot/Restore；
- Transform；
- Snap Point/Curve；
- Grip Point/Edit；
- Presentation Build；
- Persistence Read/Write；
- Property descriptor。

仅新增一个 Entity class 不代表形成用户功能。

## 8. Native 与 Transient 开发

任何 Native 临时对象创建后必须立即有 owner。

代码必须能回答：

- handle 存在哪里；
- 谁删除；
- 删除失败怎么办；
- cleanup 失败后是否仍 visible/selectable；
- Engine recreate 如何处理；
- 后续 cleanup 如何重试保留状态。

临时 Native Presentation 不依赖 GC/finalizer 保证正确性。

## 9. 异常规则

API 输入错误使用标准参数异常；破坏运行时 invariant 使用 `InvalidOperationException`。

Recoverable failure 只允许在明确的 Presentation/Input/Cleanup 边界收敛，并保留诊断信息。

至少以下异常不能当普通 recoverable error 吞掉：

- `OutOfMemoryException`；
- `StackOverflowException`；
- `AccessViolationException`。

Fatal failure 不得包装成“输入无效”。

## 10. 性能

高频 Pointer path 避免：

- 每次移动做完整 Document snapshot；
- 重复 Native Presentation rebuild；
- 冗余 redraw；
- 相同状态重复计算昂贵 Snap geometry；
- 大量可避免分配。

正确 invalidation 和 ownership 优先于猜测式缓存。

## 11. 验证

当前仓库**没有独立 Test project**，文档和 Review Checklist 不得暗示它存在。

每次修改使用当前能执行的最小真实验证：

- `build.ps1` / `build.sh`：编译和 runtime layout；
- Tool/Selection/Snap/Grip/Property：针对性手工交互回归；
- Viewer 相关改动：Native object/transient 观察；
- 持久化改动：Save/Open 回归；
- UI 改动：125%/150% DPI + 中文/English。

完整 Build Matrix 见 [11 构建与验收](11-BUILD-VALIDATION.md)，Release Matrix 见 [16 发布指南](16-RELEASE-GUIDE.md)。

没有执行的验证必须明确写“未执行”。静态检查不能等同于 Build Passed。

## 12. 提交前检查

- [ ] 修改属于唯一 owner。
- [ ] 未新增 Core/UI 双份状态。
- [ ] no-op 不产生 History/Modified。
- [ ] rollback 和 Undo/Redo 语义明确。
- [ ] Tool Commit/Cancel 在适用时恢复 neutral。
- [ ] Preview/Snap/Tracking/Grip transient 清理完整。
- [ ] Native handle cleanup 失败后不会丢失。
- [ ] Appearance 的 ByLayer/Override 语义正确。
- [ ] 稳定 ID、Registration 与文档一致。
- [ ] 中文/English 资源和文档按需同步。
- [ ] 必需的 Build/Manual/Native 验证已真实执行，或明确标记未执行。

## 13. 发布收尾规则

发布准备阶段不增加大类新功能，也不做纯审美式架构重写。

只允许：

- 编译/runtime 修复；
- 可复现 Release Blocker；
- Transaction/Transient/Selection/Native cleanup 修复；
- 本地化/DPI 修复；
- 文档纠正；
- build/run/publish 修复；
- 在不破坏 Persistence 或公开契约前提下，删除已证明无用的代码。

见 [16 发布指南](16-RELEASE-GUIDE.md)。
