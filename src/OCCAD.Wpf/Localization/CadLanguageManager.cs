using System.Globalization;
using System.Windows;
using OCCAD;

namespace OCCAD.Wpf;

internal static class CadLanguageManager
{
    private const string ResourcePrefix = "Localization/Strings.";
    private static readonly string[] Supported = ["en-US", "zh-CN"];

    private static readonly IReadOnlyDictionary<string, string> ZhText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Cad.Text.Color"] = "颜色",
        ["Cad.Text.MoreColors"] = "更多颜色...",
        ["Cad.Text.ByLayer"] = "随层",
        ["Cad.Text.Frustum"] = "圆台",
        ["Cad.Text.Arc"] = "圆弧",
        ["Cad.Text.Factor"] = "系数",
        ["Cad.Text.StatusSnap"] = "捕捉",
        ["Cad.Text.StatusOrtho"] = "正交",
        ["Cad.Text.StatusPolar"] = "极轴",
        ["Cad.Text.StatusAxis"] = "轴锁定",
        ["Cad.Text.NewLayerTitle"] = "新建图层",
        ["Cad.Text.RenameLayerTitle"] = "重命名图层",
        ["Cad.Text.LayerName"] = "图层名称",
        ["Cad.Text.LayerCreated"] = "已创建图层：{0}",

        ["Cad.ToolPanel.rectangle.Title"] = "矩形",
        ["Cad.Parameter.rectangle.Width"] = "宽度",
        ["Cad.Parameter.rectangle.Height"] = "高度",
        ["Cad.Parameter.rectangle.Angle"] = "旋转角度",
        ["Cad.Prompt.Rectangle.First"] = "矩形：指定第一个角点 [Esc 取消]",
        ["Cad.Prompt.Rectangle.Opposite"] = "矩形：指定对角点 [Backspace 返回，Esc 取消]",

        ["Cad.ToolPanel.circle.Title"] = "圆",
        ["Cad.Parameter.circle.Method"] = "绘制方式",
        ["Cad.Parameter.circle.Radius"] = "半径",
        ["Cad.Parameter.circle.Diameter"] = "直径",
        ["Cad.Parameter.circle.Method.CenterRadius"] = "圆心 + 半径",
        ["Cad.Parameter.circle.Method.CenterDiameter"] = "圆心 + 直径",
        ["Cad.Parameter.circle.Method.TwoPoints"] = "两点定直径",
        ["Cad.Parameter.circle.Method.ThreePoints"] = "三点定圆",
        ["Cad.Prompt.Circle.Diameter"] = "圆：指定直径 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Circle.FirstDiameterPoint"] = "圆：指定直径第一点 [Esc 取消]",
        ["Cad.Prompt.Circle.SecondDiameterPoint"] = "圆：指定直径第二点 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Circle.FirstPoint"] = "圆：指定第一点 [Esc 取消]",
        ["Cad.Prompt.Circle.SecondPoint"] = "圆：指定第二点 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Circle.ThirdPoint"] = "圆：指定第三点 [Backspace 返回，Esc 取消]",

        ["Cad.ToolPanel.arc.Title"] = "圆弧",
        ["Cad.Parameter.arc.Method"] = "绘制方式",
        ["Cad.Parameter.arc.Method.ThreePoints"] = "三点",
        ["Cad.Parameter.arc.Method.CenterStartEnd"] = "圆心 + 起点 + 终点",
        ["Cad.Parameter.arc.Method.StartCenterEnd"] = "起点 + 圆心 + 终点",
        ["Cad.Prompt.Arc.First"] = "圆弧：指定第一点 [Esc 取消]",
        ["Cad.Prompt.Arc.Second"] = "圆弧：指定第二点 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Arc.Third"] = "圆弧：指定第三点 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Arc.Center"] = "圆弧：指定圆心 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Arc.Start"] = "圆弧：指定起点 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Arc.End"] = "圆弧：指定终点 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Arc.Invalid"] = "圆弧：当前输入不能构成有效圆弧。",

        ["Cad.ToolPanel.ellipse.Title"] = "椭圆",
        ["Cad.Parameter.ellipse.Method"] = "绘制方式",
        ["Cad.Parameter.ellipse.MajorRadius"] = "长轴半径",
        ["Cad.Parameter.ellipse.MinorRadius"] = "短轴半径",
        ["Cad.Parameter.ellipse.Angle"] = "旋转角度",
        ["Cad.Parameter.ellipse.Method.CenterMajorMinor"] = "中心 + 长轴 + 短轴",
        ["Cad.Parameter.ellipse.Method.AxisEndpointsMinor"] = "长轴两端点 + 短轴",
        ["Cad.Prompt.Ellipse.Center"] = "椭圆：指定中心 [Esc 取消]",
        ["Cad.Prompt.Ellipse.Major"] = "椭圆：指定长轴点 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Ellipse.Minor"] = "椭圆：指定短轴距离 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Ellipse.AxisFirst"] = "椭圆：指定长轴第一端点 [Esc 取消]",
        ["Cad.Prompt.Ellipse.AxisSecond"] = "椭圆：指定长轴第二端点 [Backspace 返回，Esc 取消]",
        ["Cad.Prompt.Ellipse.InvalidMinor"] = "椭圆：短轴半径必须大于零且不能超过长轴半径。",

        ["Cad.Prompt.Frustum.Center"] = "圆台：指定底面中心 [Esc 取消]",
        ["Cad.Prompt.Frustum.BaseRadius"] = "圆台：指定底面半径 [Esc 取消]",
        ["Cad.Prompt.Frustum.Height"] = "圆台：指定高度 [Esc 取消]",
        ["Cad.Prompt.Frustum.TopRadius"] = "圆台：指定顶面半径 [Esc 取消]",
        ["Cad.Text.PlaneSide"] = "侧视绘图平面 (S)",
        ["Cad.Text.PlaneFront"] = "前视绘图平面 (F)",
        ["Cad.Text.PlaneTop"] = "俯视绘图平面 (T)",
        ["Cad.Text.WorkPlaneLocked"] = "当前工具已锁定绘图平面。",
        ["Cad.Text.SnapPlanePolicy"] = "捕捉平面",
        ["Cad.Text.SnapPlaneKeep3DShort"] = "保留三维点",
        ["Cad.Text.SnapPlaneProjectShort"] = "投影到工作平面",
        ["Cad.Text.SnapPlaneRequireShort"] = "仅工作平面上的点",
        ["Cad.Text.SnapPlaneKeep3D"] = "捕捉使用实体真实三维点。",
        ["Cad.Text.SnapPlaneProject"] = "捕捉点投影到当前工作平面。",
        ["Cad.Text.SnapPlaneRequire"] = "仅接受位于当前工作平面的捕捉点。",
        ["Cad.Text.Lock"] = "锁定",
        ["Cad.Text.LockLength"] = "锁定长度",
        ["Cad.Text.LockAngle"] = "锁定角度",
        ["Cad.Text.InvalidLength"] = "长度必须是大于零的有限数值。",
        ["Cad.Text.InvalidAngle"] = "角度必须是有限数值。",
        ["Cad.History.LayerColor"] = "图层颜色",
        ["Cad.History.LayerEdit"] = "图层属性",
        ["Cad.History.PropertyEdit"] = "属性修改",
        ["Cad.History.PropertyEditNamed"] = "修改属性 {0}",
        ["Cad.Category.General"] = "常规",
        ["Cad.Category.Display"] = "显示",
        ["Cad.Category.State"] = "状态",
        ["Cad.Category.Geometry"] = "几何",
        ["Cad.Category.Measurement"] = "测量",
        ["Cad.Property.EntityType"] = "类型",
        ["Cad.Property.Id"] = "标识",
        ["Cad.Property.Name"] = "名称",
        ["Cad.Property.Layer"] = "图层",
        ["Cad.Property.Visible"] = "可见",
        ["Cad.Property.Selectable"] = "可选择",
        ["Cad.Property.ColorByLayer"] = "颜色随层",
        ["Cad.Property.LineWidthByLayer"] = "线宽随层",
        ["Cad.Property.LineStyleByLayer"] = "线型随层",
        ["Cad.Property.Color"] = "颜色",
        ["Cad.Property.Transparency"] = "透明度",
        ["Cad.Property.LineWidth"] = "线宽",
        ["Cad.Property.LineStyle"] = "线型",
        ["Cad.Property.DisplayMode"] = "显示模式",
        ["Cad.Property.Material"] = "材质",
        ["Cad.Property.Locked"] = "锁定",
        ["Cad.Property.X"] = "X 坐标",
        ["Cad.Property.Y"] = "Y 坐标",
        ["Cad.Property.Z"] = "Z 坐标",
        ["Cad.Property.Start"] = "起点",
        ["Cad.Property.End"] = "终点",
        ["Cad.Property.Center"] = "中心",
        ["Cad.Property.Radius"] = "半径",
        ["Cad.Property.MajorRadius"] = "长轴半径",
        ["Cad.Property.MinorRadius"] = "短轴半径",
        ["Cad.Property.StartAngleDegrees"] = "起始角度",
        ["Cad.Property.SweepAngleDegrees"] = "圆心角",
        ["Cad.Property.Height"] = "高度",
        ["Cad.Property.Width"] = "宽度",
        ["Cad.Property.Length"] = "长度",
        ["Cad.Property.Angle"] = "角度",
        ["Cad.Property.RotationDegrees"] = "旋转角度",
        ["Cad.Property.ScaleFactor"] = "缩放系数",
        ["Cad.Property.Position"] = "位置",
        ["Cad.Property.Area"] = "面积",
        ["Cad.Property.Perimeter"] = "周长",
        ["Cad.Property.Circumference"] = "圆周长",
        ["Cad.Property.ArcLength"] = "弧长"
    };

    private static readonly IReadOnlyDictionary<string, string> ZhFallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["General"] = "常规",
        ["Display"] = "显示",
        ["State"] = "状态",
        ["Geometry"] = "几何",
        ["Measurement"] = "测量",
        ["Color"] = "颜色",
        ["Frustum"] = "圆台",
        ["Rectangle"] = "矩形",
        ["Sides"] = "边数",
        ["Mode"] = "模式",
        ["Method"] = "绘制方式",
        ["Radius"] = "半径",
        ["Diameter"] = "直径",
        ["Major Radius"] = "长轴半径",
        ["Major Axis Length"] = "长轴长度",
        ["Minor Radius"] = "短轴半径",
        ["Tube Radius"] = "管半径",
        ["Base Radius"] = "底面半径",
        ["Top Radius"] = "顶面半径",
        ["Height"] = "高度",
        ["Width"] = "宽度",
        ["Length"] = "长度",
        ["Angle"] = "角度",
        ["Factor"] = "系数",
        ["Segments"] = "分段数",
        ["Closed"] = "闭合",
        ["Clockwise"] = "顺时针"
    };

    public static string CurrentLanguage { get; private set; } = "en-US";
    public static event EventHandler? Changed;

    public static string Text(string key, string fallback)
    {
        if (Application.Current.TryFindResource(key) is string resource)
            return resource;

        if (string.Equals(CurrentLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase))
        {
            if (ZhText.TryGetValue(key, out var translated))
                return translated;
            if (ZhFallback.TryGetValue(fallback, out translated))
                return translated;
        }

        return fallback;
    }

    public static string ToolPrompt(CadToolPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (string.IsNullOrWhiteSpace(prompt.ResourceKey))
            return prompt.Message;

        var template = Text(prompt.ResourceKey, prompt.Message);
        if (prompt.FormatArguments.Count == 0)
            return template;

        try
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                prompt.FormatArguments.ToArray());
        }
        catch (FormatException)
        {
            return prompt.Message;
        }
    }

    public static string CommandMessage(CadCommandResult result)
    {
        var fallback = result.Message ?? string.Empty;
        if (string.IsNullOrEmpty(result.MessageKey)) return fallback;
        var template = Text(result.MessageKey, fallback);
        if (result.MessageArguments.Count == 0) return template;
        try { return string.Format(CultureInfo.CurrentCulture, template, result.MessageArguments.ToArray()); }
        catch (FormatException) { return fallback; }
    }
    public static string HistoryName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        if (name.StartsWith("Property ", StringComparison.Ordinal) && name != "Property Edit")
        {
            var property = name[9..];
            return string.Format(CultureInfo.CurrentCulture,
                Text("Cad.History.PropertyEditNamed", "Property {0}"),
                Text($"Cad.Property.{property}", property));
        }
        if (name.StartsWith("Create ", StringComparison.Ordinal))
        {
            var entityName = name["Create ".Length..];
            var localizedEntityName = Text(
                $"Cad.Text.{entityName.Replace(" ", string.Empty, StringComparison.Ordinal)}",
                entityName);
            return string.Format(
                CultureInfo.CurrentCulture,
                Text("Cad.History.CreateNamed", "Create {0}"),
                localizedEntityName);
        }

        if (name.StartsWith("Assign Layer ", StringComparison.Ordinal))
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Text("Cad.History.AssignLayerNamed", "Assign Layer {0}"),
                name["Assign Layer ".Length..]);
        }
        foreach (var operation in new[] { "Copy", "Delete", "Hide" })
        {
            if (name.StartsWith(operation + " ", StringComparison.Ordinal) &&
                int.TryParse(name[(operation.Length + 1)..], out var count))
                return string.Format(CultureInfo.CurrentCulture,
                    Text($"Cad.History.{operation}Count", operation + " {0}"), count);
        }
        if (name is "Display Wireframe" or "Display Shaded")
            return string.Format(CultureInfo.CurrentCulture,
                Text("Cad.History.DisplayNamed", "Display {0}"),
                Text($"Cad.Value.OcctDisplayMode.{name[8..]}", name[8..]));
        return Text($"Cad.History.{name.Replace(" ", "")}", name);
    }
    public static void Apply(string language)
    {
        var normalized = Supported.Contains(language, StringComparer.OrdinalIgnoreCase)
            ? Supported.First(value => string.Equals(value, language, StringComparison.OrdinalIgnoreCase))
            : "en-US";
        var resources = Application.Current.Resources.MergedDictionaries;
        var previous = resources.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains(ResourcePrefix, StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary
        {
            Source = new Uri($"{ResourcePrefix}{normalized}.xaml", UriKind.Relative)
        };

        if (previous is null)
            resources.Add(replacement);
        else
            resources[resources.IndexOf(previous)] = replacement;

        CurrentLanguage = normalized;
        var culture = CultureInfo.GetCultureInfo(normalized);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
