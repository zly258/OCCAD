# 09 稳定性与性能规范

## 1. 原则

CAD 的稳定性来自局部事务和可恢复状态，而不是在最外层吞掉所有异常。任何用户输入、预览、夹点拖拽、属性修改和文件解析失败后，都必须保证 Document、Selection、Preview、Grip、WorkPlane、History 仍然一致。

## 2. 三层异常边界

第一层是业务事务：Tool、Grip、Property、Document mutation 自己捕获可恢复异常并回滚。第二层是 Input/Viewport/UI 分发：阻止普通异常直接逃出 WPF Dispatcher，并清理瞬态状态。第三层是应用兜底：记录未处理错误并尽量提示用户，但不把 OutOfMemory、StackOverflow、AccessViolation 等不可恢复错误伪装成安全恢复。

## 3. 输入兜底

所有数值入口统一检查 `double.IsFinite`。长度、半径、宽高、缩放因子按语义验证正值或非零值；角度规范化；方向向量必须有有效模长；索引必须在范围内；工作平面求交失败不得生成伪坐标。

几何容差集中定义，不在每个 Tool 散落不同的 `1e-9`。UI 输入验证失败只提示并保留当前 Tool，不清空有效 Preview。

## 4. Preview 事务

Preview 更新流程：构造 next entity/state → 构造全部 next presentation → 应用最终外观 → 成功后替换旧 presentation。中途失败删除本次临时对象并保留上一帧。

PointerMove 经过退化点时“不更新”而不是 `Clear()`。只有 Cancel、Complete、显式 StepBack 到无预览阶段、Document reset 才清理 Preview。

## 5. Grip 事务

Grip 开始时保存原始几何并创建独立 Preview Copy。MouseMove 只修改 Preview Copy。每次 MoveGrip 失败恢复该 Preview 的上一有效状态。Accept 时再一次性 Restore/Apply 到真实 Entity，随后记录一条 History。

任何 Grip 失败都不得留下半更新真实 Entity，也不得让 marker 列表与实体 Grip 数量失配。

## 6. Viewer 对象生命周期

调用 Update/Delete/Style 前确认 Engine 已初始化且对象仍存在。删除使用批处理；对象删除后立即从 managed 映射中移除。不得长期保存已失效 ViewerObject ID。

Scene overlay（Preview/Snap/Grip/Preselection/Selection rectangle）分别拥有自己的生命周期，互不借用对象集合。

## 7. PointerMove 热路径

MouseMove 每秒可能触发数十到数百次，因此禁止：

- 重建整个 Layer/Property UI；
- 重建无变化的 Marker pixmap；
- 重算与指针无关的 Tool schema；
- 无条件删除并重建全部 overlay；
- 同一事件链多次 Redraw/Flush；
- 同步执行文件 I/O。

优先更新已有 point/shape presentation；必须重建 Shape 时在一个 display batch 内完成。

## 8. 文件加载

大文件流程拆成：读取 → 解析 → 建模数据 → Viewer presentation → Fit/完成。进度条放底部状态/任务区域。耗时解析在后台执行，但 OCCT Viewer 调用必须遵守其线程边界；UI 线程只接收阶段进度和最终批量展示任务。

取消加载必须安全释放中间对象。失败不能留下“Document 已部分替换但标题/History 仍指向旧文件”的状态。

## 9. 日志

日志只记录可定位的信息：操作、异常类型、简洁消息、必要上下文、耗时。PointerMove 普通失败不刷屏；同类重复错误应节流。用户提示使用本地化短文本，技术堆栈写日志而不是状态栏。

## 10. 性能验收

交互验收重点观察：绘制连续移动无明显闪烁；Snap/Grip marker 不消失；ToolPanel 不因 ToolUpdated 重建 Window；Layer 属性编辑不导致整表跳动；数千 Entity 的选择/显示变化使用 batch；文件打开期间主窗口可响应取消和重绘。
