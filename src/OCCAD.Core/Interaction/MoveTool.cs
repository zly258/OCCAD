using OcctNet;

namespace OCCAD;

public sealed class MoveTool : CadTranslateToolBase
{
    public override string Id => "move";
    public override string DisplayName => "Move";

    protected override bool SuppressSourcesDuringPreview => true;

    protected override void Commit(OcctVector3d displacement) =>
        Context.Workspace.TranslateEntities(Entities, displacement);
}
