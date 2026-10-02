namespace Gallop
{
    // Serialized Live camera subtype. The base image effect is also used outside Live.
    public class LiveImageEffect : GallopImageEffect
    {
        public override void InitializeVolume()
        {
            if (IsInitialized) return;
            base.InitializeVolume();
            // Actual game Initialize RVA 0x7395c80 calls the base initializer,
            // then writes render Parameter+0x1b=true (0x7395ced). Not source Setup.
            RenderParameter.DofDiffuionBloomOverlay.IsEnableDofAutoDisable = true;
        }
    }
}
