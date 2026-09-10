# 10 本地化、数值与数据表达规范

## 1. 双语是基础能力

中文和英文必须保持相同功能覆盖。任何新 Menu、Tool、ToolPanel 参数、Property、枚举、Prompt、错误、状态、Snap 类型、Grip 操作结果都必须同时提供 `zh-CN` 与 `en-US` 显示文本。

禁止依赖 fallback 长期显示英文。fallback 只用于开发期防止资源缺失导致空字符串。

## 2. 可见字符串来源

所有用户可见文本必须来自统一资源或显示映射。包括：窗口标题、菜单、按钮、Dock 标题、列名、Property Category/DisplayName/Description、ToolPanel Label、枚举值、StatusBar、MessageBox、文件错误、History 可见名称。

内部 ID、Action ID、EntityType、序列化字段名保持稳定英文，不随语言变化。

## 3. 枚举

`OcctLineStyle.Solid`、DisplayMode、Material、SnapMode 等不得直接 `ToString()` 作为用户界面。统一使用 `CadDisplayText`/converter 一类单一映射入口，避免不同页面翻译不一致。

## 4. 数值显示

默认工程数值显示 3 位小数：`0.000`。坐标、长度、半径、宽高、ToolPanel 参数、Property 编辑器 double 使用同一默认格式。角度默认可显示 3 位小数并带上下文中的 `°`，但底层保持 double。

显示格式绝不能修改模型精度：

```text
真实值 12.345678901
UI 显示 12.346
再次读取且未编辑 → 仍为 12.345678901
用户输入 12.34567 → 保存 12.34567
```

禁止在 getter、serializer、history snapshot 中 Round(3)。

## 5. 输入解析

数字输入优先使用当前 Culture，同时接受 invariant 小数点作为兼容输入。空字符串、NaN、Infinity、超范围值返回验证失败，不抛到 UI 顶层。

输入框失焦与 Enter 的 Commit 语义必须一致，不得 Enter 应用一次、LostFocus 又重复创建 History。

## 6. 单位

当前核心可继续使用无单位/工程单位数值，但 UI 不应把单位字符串硬编码到模型值里。未来单位系统应在显示/解析层转换，Entity 始终保存规范内部单位。

## 7. ByLayer 数据语义

颜色、线宽、线型均采用“两部分状态”：

- `ColorByLayer` + `Color`；
- `LineWidthByLayer` + `LineWidth`；
- `LineStyleByLayer` + `LineStyle`。

ByLayer=true 时自定义值仍可保留为上一次 override 值，关闭 ByLayer 后恢复使用该值。不要因为切随层而覆盖用户之前的自定义颜色。

## 8. 颜色

图层和实体颜色按钮点击直接打开系统 ColorDialog。颜色按钮本身只展示当前有效颜色或 override 状态；ByLayer 通过独立布尔属性表达。

Preview 和正式 Presentation 必须调用相同的外观解析；当前图层创建的新实体在 Commit 前的 Preview 也必须解析为当前层颜色。

## 9. 序列化

序列化写稳定内部字段，不写本地化文本。double 使用可往返精度；enum 写稳定值/名称；Guid/EntityType/Layer 引用不可因 UI 改名而失效。

读取旧文件时的兼容逻辑只能存在于 serializer 边界，不污染 Entity 公共 API 和 UI。
