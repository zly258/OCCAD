# 06 质量与数据

## 稳定性
普通非法输入、退化几何、Preview、Grip、Property/Layer、文件解析错误不能让应用退出，也不能留下不一致的 Document、Selection、Preview、Grip、WorkPlane、Drafting、History。能在写入前验证的先验证，提交失败时回滚真实状态。Fatal error 不做伪恢复。

## Preview / Grip
Preview 是 transient，更新失败保留上一帧有效 Preview。Grip 编辑只改 Duplicate Preview；编辑期间抑制真实 source presentation，Accept 时只写回一次并记录一条 History。

## Redraw / 性能
Bridge 已 request redraw 或 display batch 已执行 pending redraw 时，不再额外 Redraw。PointerMove 禁止同步 I/O、整面板 Rebuild、重复 marker、重复 Redraw、无意义 delete/recreate、持续修改持久 Geometry。优先 display batch、Snap/Grip cache、增量 UI refresh。

## 大数据
大模型/文件拆分 read、parse、model creation、presentation。进度放底部状态区域，不用阻塞式弹窗。OCCT Viewer 调用遵守所属线程。

## 本地化
中英文资源 Key 同步。可见 UI 使用稳定资源键；内部 Id、EntityType、ActionId、ToolId、枚举序列化值、Persistence 字段保持语言无关。

## 数值 / 外观
Model/History/Persistence 保存完整精度。NaN、Infinity、越界、退化方向、非法拓扑 index 在 Core 边界拒绝。

Entity 保存 Color/LineStyle/LineWidth override 和独立 ByLayer flag；最终有效值由 Document + Layer 解析。ByLayer 不销毁 override。多选不同值显示 `—`。

## History / 发布验证
UI 修改要么产生一条完整 History，要么不产生。Undo/Redo 恢复前取消 Active Tool 并清理 stale Selection/Subobject/Preselection/Grip。

发布前人工验证代表性二维/三维、Modify、Snap、Tracking、Grip、Property、Layer、Undo/Redo、Save/Open、语言切换、DPI、Preview cleanup、框选矩形 cleanup、非法输入恢复。仓库不维护庞大临时 smoke/check 脚本。
