# 01 产品目标

## 定位
OCCAD 是通过 OcctCSharpBridge 使用 OCCT 的轻量级、但架构完整的桌面 CAD 框架/应用基础，不是 OCCT API Demo。

## 职责模型
Document 唯一拥有持久 Entity；Entity 负责合法几何、外观、Snap/Grip、复制/恢复和持久化；Tool 负责阶段交互、Prompt、Preview、Finish/Cancel/StepBack；Action 是稳定命令入口；Viewport 不拥有 CAD 业务规则；Property/Layer 只通过 Core Transaction/History 修改状态；Snap 只选择候选；Grip 几何语义属于 Entity。

OCCTBIM-Source 只作为行为和架构参考，不复制 Qt UI、Singleton 耦合或类层级。

## 功能基线
二维、三维、Modify、Object Snap、WorkPlane、Tracking、Precision、Selection、Grip、Layer、Property、Undo/Redo、Persistence、Annotation、Measurement 必须共享一套正确契约。命令只有在 Preview、Commit、Cancel、StepBack、Selection、Property、Undo/Redo、本地化和持久化全部一致时才算完成。

## UI 目标
统一为高密度工业 CAD 风格：克制中性灰、深色 Viewport、统一字体和控件高度、低/无圆角、浅层 Menu、左 Model、右侧可调整 Layer/Property、Command Line 位于 StatusBar 上方、非模态 ToolPanel。

## 属性目标
普通 Property 只暴露 CAD 业务属性。Viewer handle、Id、内部 Selectable、Material/DisplayMode、序列化字节数等隐藏。Color/LineStyle/LineWidth 内置 ByLayer；直接编辑具体值自动退出对应 ByLayer，重新启用时保留 override。

## 质量目标
普通非法几何、Preview、Grip、Property/Layer、文件错误不能让应用退出，也不能留下不一致 transient state。能生成 Shape 不等于功能完成，完整交互契约正确才代表完成。
