# 05 Entity 与 Tool 开发契约

## Entity
每个 Entity 提供稳定 Type/Id、合法 Geometry、Presentation 构造、Duplicate/Restore、Transform、Snap/Grip、Change Notification、Persistence。Geometry setter 写入前验证 finite、range/positive、direction、index。

## Property
Browsable 属性必须有普通 CAD 意义：Type/Name/Layer、Geometry、Measurement、Appearance。Viewer handle、Id、内部 Selectable/Display/Material、Feature plumbing、BREP size 等实现细节隐藏。Color/LineStyle/LineWidth 使用内嵌 ByLayer Editor；bool 保留 Core，但不单独占行。

## Snap / Grip
Entity 定义 Snap/Grip 语义。SnapManager 只选择候选，GripManager 只显示/命中 Grip。`MoveGrip` 接收 resolved world point，不处理 raw pointer/Snap/Ortho/UI。Grip PointerMove 始终使用 Duplicate Preview。

## Tool
每个 Tool 定义 ID/Name、State/Stage、InputKind、Prompt、PrecisionInputs、InteractionPolicy、SnapResolvePolicy、PrecisionReference、WorkPlane、Parameters、Preview、Finish/Cancel/StepBack。UI 不根据 Stage 数字复制业务逻辑。

## Preview / Commit
Preview 更新失败保留上一帧有效值。普通 Commit：validate → finalize Entity → apply layer/appearance → Document/History → clear Preview → complete Tool。可能失败的 mutation 前不能先清 Preview。`CadToolContext.AddEntity` 只负责模型 Add。

Replacement 操作 mutation 期间保持有效 preview 和 source suppression；成功后清 transient，失败时 Tool 保持可用。

## Property Transaction
`Capture → Validate → Apply → Presentation/Dependency Refresh → History`。直接修改 Color/LineStyle/LineWidth 自动关闭对应 ByLayer；切换 ByLayer 本身也进入 History。

## 完成标准
Geometry、Registration、Stage、Prompt、Preview、Snap、Grip、Precision、Property、Layer/ByLayer、Undo/Redo、Persistence、本地化、非法输入、Cancel/StepBack/右键、导航全部正确才算完成。
