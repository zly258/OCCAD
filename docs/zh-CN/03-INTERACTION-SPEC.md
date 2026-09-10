# 03 交互规范

## 1. 唯一交互链

所有鼠标、键盘、精确输入、Command Line、Snap、Tracking、Grip 都进入当前 Workspace/Tool 交互上下文。MainWindow 不直接修改 Entity 几何。

```text
Menu / Shortcut / Command Line
              ↓
       CadActionManager
              ↓
        Action / Tool

Active Tool + Command Line
              ↓
 Finish / Cancel / StepBack / Precision / Parameter / Point
              ↓
        Tool state machine
```

## 2. Tool 生命周期

统一状态：`Idle → Activate → Drawing/WaitForSelection → Preview Updated → Commit Stage → next/complete`。Esc/Cancel 清理 Preview、Tracking、Snap 临时状态和 Tool 临时工作平面。右键：CanFinish 时 Finish，否则 Cancel。Backspace/StepBack 回退一个阶段，不等价于 Cancel。

## 3. Command Line 键盘优先级

当 Command Line TextBox 获得焦点：Enter 提交；Up/Down 浏览输入历史；Esc 先清空文本，空文本时取消 Active Tool；Backspace 是正常文本编辑。全局 Tool Backspace/快捷键不得抢占 TextBox 编辑。

Viewport 获得焦点：Enter 在 Idle 时重复最近 repeatable Action；Active Tool 时优先 Finish，否则 CommitCurrentStage；Esc Cancel；Backspace StepBack；F3/F8/F10 保留 Snap/Ortho/Polar。

## 4. 统一点解析

屏幕点必须走：`Screen → view ray → active WorkPlane → object Snap → Tracking → axis/angle/length constraint → final world point`。Tool 不重复 XY 投影或锁轴算法。

文本坐标输入也必须进入等价的 world-point commit contract；禁止为了 Command Line 在 MainWindow 中直接写 Entity 字段。

## 5. WorkPlane 与视觉提示

S→YZ、F→XZ、T→XY。切换工作平面不改变相机；persistent user plane、temporary tool plane、grip plane 必须分离。

工作平面 X/Y 两条参考轴默认不显示。Tracking guide 仅在 ORTHO/POLAR 得到真实跟踪结果时显示；普通 pointer movement 不绘制 origin→pointer guide。Line/Polyline 等已有几何 Preview 的 Tool 因而不会出现重复指引线。

## 6. Selection / Grip / Preview

Selection 支持 Replace/Add/Remove/Toggle；左→右框为 Window，右→左框为 Crossing。点选与框选必须共享 SelectionManager 和 selectable/visible/locked filter。

Grip 语义属于 Entity。Grip 编辑：捕获原状态 → duplicate preview → MoveGrip(preview) → validate/rebuild → Accept 写回真实 Entity → 单条 History；鼠标移动期间不得持续修改真实 Entity。

Preview 是 transient，不进入 Document/Selection/History。替换 Preview 必须先成功创建新对象，再删除旧对象。

## 7. 导航、属性和异常

Active Tool 期间仍允许平移/缩放/旋转。Property 编辑采用 capture → validate → apply → rebuild/presentation → history，失败恢复旧状态。

异常分层处理：Tool/Grip/Property 局部事务负责 rollback；Viewport/UI boundary 负责报告并恢复可操作状态；不可恢复异常不得静默吞掉。
