using UnityEngine;

namespace AstralBloom.Gacha
{
    public class GachaBootstrap : MonoBehaviour
    {
        public GachaBannerConfig bannerConfig;

        private void Awake()
        {
            GachaManager manager =
                GetComponent<GachaManager>();

            if (manager == null)
                manager = gameObject.AddComponent<GachaManager>();

            manager.banner = bannerConfig;

            GachaUI ui =
                GetComponent<GachaUI>();

            if (ui == null)
                ui = gameObject.AddComponent<GachaUI>();

            ui.manager = manager;
        }
    }
}
