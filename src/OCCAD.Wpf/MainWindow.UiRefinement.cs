using System.Drawing;
using OcctNet;

namespace OCCAD.Wpf;

public partial class MainWindow
{
    private bool _cadUiRefined;

    private void RefineCadUiLayout()
    {
        if (_cadUiRefined) return;
        _cadUiRefined = true;
        ApplySceneDefaults(_workspace.Engine);
        Viewport.EngineRecreated += (_, args) => ApplySceneDefaults(args.Engine);
    }

    private static void ApplySceneDefaults(OcctEngine? engine)
    {
        if (engine is null || !engine.IsInitialized) return;

        using (engine.BeginDisplayBatch())
        {
            engine.SetGradientBackground(Color.Black, Color.Black);
            engine.SetTriedron(new OcctTriedronOptions
            {
                Visible = true,
                Position = OcctCornerPosition.LeftLower,
                Scale = 0.08,
                Color = Color.White
            });
            engine.SetViewCubeVisible(false);
        }
    }
}
