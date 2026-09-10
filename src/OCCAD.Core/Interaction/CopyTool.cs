using OcctNet;

namespace OCCAD;

public sealed class CopyTool : CadTranslateToolBase
{
    public override string Id => "copy";
    public override string DisplayName => "Copy";

    protected override void Commit(OcctVector3d displacement) =>
        Context.Workspace.CopyEntities(Entities, displacement);
}
