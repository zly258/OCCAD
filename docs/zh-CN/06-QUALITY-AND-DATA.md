# 06 质量与数据

## 稳定性

普通非法输入、退化几何、Preview 失败、Grip 失败、Property 失败和文件解析失败，不得让应用退出，也不得留下不一致的 Document、Selection、Preview、Grip、WorkPlane、Drafting 或 History 状态。

使用局部事务边界。修改前验证有限数值、尺寸、向量、索引、拓扑引用、工作平面交点和 Viewer object 生命周期。OutOfMemoryException、StackOverflowException、AccessViolationException 等不可恢复异常不做伪恢复。

## Preview 与 Grip

Preview 只维护瞬态状态。某一帧构造失败时保留上一帧有效 Preview。Cancel 和完成后必须清理瞬态显示。Grip 编辑只修改独立 Preview 副本；Accept 对真实实体只提交一次原子修改并记录一条 History；Cancel 恢复原状态。

## 性能

PointerMove 热路径避免整面板刷新、重复构造 marker、重复 redraw、同步 I/O 和无条件 delete/recreate。优先使用局部 presentation update、Snap/Grip 缓存、批处理和增量 Tree/Panel 刷新。

大文件加载按读取、解析、模型、显示分阶段，并在底部状态区域显示进度。Viewer 调用遵守 OCCT 线程所有权。

## 本地化

中文和英文同等支持。Menu、Tool、ToolPanel、Property、枚举值、提示、错误、状态、Snap 名称、Grip 结果和文件消息使用稳定资源键。内部 ID、EntityType、Action ID 和序列化字段名保持与语言无关。

## 数值与单位

工程数值保存完整模型精度。UI 可以简洁格式化，但不得对模型状态、History 快照和序列化值做永久舍入。解析时在本地拒绝空值、NaN、Infinity 和越界值。

单位属于显示/解析转换，Entity 存储统一内部值。

## 外观与数据

Color、LineWidth、LineStyle 分别保留 ByLayer 开关和 override 值。开启 ByLayer 不需要销毁已有 override。

序列化使用稳定标识、完整数值精度和与语言无关的枚举/类型值。确有必要的旧文件兼容只能停留在 serializer 边界，不进入 public Entity 或 UI 契约。

## 发布质量

稳定版要求 Release 编译干净，并人工验证代表性的二维绘制、三维建模、编辑、Snap、Grip、Property/Layer、Undo/Redo、Save/Open、语言切换、DPI 缩放和非法输入恢复。该要求属于发布实践，不再维护会快速过期的阶段验收清单。
