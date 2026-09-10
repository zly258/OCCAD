# 12 验收清单

## 1. 用法

功能只有通过对应清单才视为完成。验收以实际交互和状态一致性为准，不以“类已创建”或“能显示一次”为准。

## 2. 主界面

- 原生 Menu/ToolBar/StatusBar，无 Ribbon 残留依赖。
- ToolBar 始终可见，不因 Tool 切换导致布局跳动。
- 常用菜单不超过一层子菜单。
- Dock 关闭按钮透明无边框，尺寸和边距统一。
- 125%/150% DPI 下窗口、菜单、Dock 不超屏或错位。
- 中文/英文切换后当前界面立即完整刷新。

## 3. Tool

- Activate 初始化状态完整。
- PointerMove 只更新 transient state。
- 每阶段 Prompt 明确。
- LeftClick/Enter/Finish 语义一致。
- RightClick：可完成则完成，否则取消。
- Backspace StepBack 正确。
- Cancel 清理 Preview/Tracking/临时 WorkPlane。
- Tool 结束后 ToolPanel 隐藏且焦点回 Viewport。
- 参数非法时不崩溃、不清空上一有效 Preview。

## 4. ToolPanel

- 继承唯一 `CadToolPanel` 基类。
- 非模态、不抢焦点。
- 参数修改即时刷新 Preview。
- 关闭只隐藏，不取消 Tool。
- double 显示 3 位小数但不损失真实值。
- 参数标签和枚举完整双语。

## 5. Preview

- 不进入 Document/History/Selection。
- 外观与最终 Entity + Layer 一致。
- 新建时使用当前层而非固定 Layer 0 外观。
- 替换原子化。
- 退化 MouseMove 不清掉上一有效帧。
- Cancel/Complete 后无残留 ViewerObject。

## 6. Selection

- 单击、Ctrl/Shift 组合、空白点击行为明确。
- Window/Crossing 方向正确。
- Hidden/Locked/Selectable=false 不可被正式选择。
- Preselection 与 Selection 不互相污染。
- ModelTree 与 Viewport 同步无递归循环。

## 7. Snap / Tracking

- Endpoint/Midpoint/Center/Vertex/Quadrant/Nearest/Intersection/Perpendicular/Tangent 按实现范围正确工作。
- Marker 隐藏后可再次显示。
- Marker 尺寸稳定，不随 Zoom 改变。
- ORTHO/POLAR/Angle/Axis 优先级明确。
- Length 在方向约束之后应用。
- 临时 Snap 不修改持久 Snap 配置。

## 8. Grip

- 选中实体显示语义正确的 Grip。
- Marker 小而清晰，Hot 不遮挡几何。
- Hit tolerance 以屏幕像素计算。
- Drag 只修改 Preview Copy。
- 无效几何回滚并保持 Tool 活跃。
- Accept 只形成一次实体修改和一次 History。
- Cancel 完全恢复原始几何。
- 任意异常不导致应用直接退出。

## 9. Layer

- 颜色单击直接 ColorDialog。
- 可见/锁定/线宽/线型单击可编辑。
- 编辑非当前层属性不自动切 Current Layer。
- Layer 改动实时更新所有 ByLayer Entity。
- 不通过 `ItemsSource=null` 整表重建。
- 删除含实体的层有明确保护。

## 10. Property

- ColorByLayer/custom Color 均可用。
- LineWidthByLayer/custom LineWidth 均可用。
- LineStyleByLayer/custom LineStyle 均可用。
- double 3 位显示、全精度保存。
- enum 双语。
- 多选只显示共同属性。
- 非法修改回滚。
- 一次用户修改只记录一次 History。

## 11. Entity

每个 Entity 均检查：BuildShape、Duplicate、RestoreGeometry、Transform、Snap、Grip、MoveGrip、Tool、Preview、Property、ByLayer、History、Serialization、Localization、退化输入。

Cone 与 Frustum 必须保持两个独立 Entity/Tool，不允许再次通过 TopRadius=0 混合类型。

## 12. Document / History

- New/Open/Save/SaveAs modified 状态正确。
- Save 后 Undo/Redo 对 modified 状态正确。
- Add/Delete/Move/Copy/Grip/Property/Layer 均可 Undo/Redo。
- 失败操作不生成空 History。
- Reset/Clear 不残留 Selection/Grip/Preview。

## 13. 文件与性能

- 打开大文件有底部进度反馈。
- 文件解析不长时间卡死 UI。
- 失败后当前 Document 状态明确。
- 数千对象批量显示/隐藏使用 display batch。
- 连续绘图和 Grip 拖拽无明显闪烁。

## 14. 发布前

- `build.ps1` 成功。
- 手工完成一轮 2D、3D、编辑、Grip、Layer、Property、Save/Open 回归。
- 中英文各运行一轮。
- 无无用第三方 UI 包、旧 Style、隐藏兼容控件、临时 patch 文件。
- 文档与实际 UI/交互一致。
