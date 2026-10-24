using TMPro;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Icaro
{
    // Acrescenta "Tremor da câmera" ao menu de pausa do Microgame sem editar o prefab: copia a linha da opção
    // de sombras (rótulo + toggle) para o mesmo grid e liga o toggle a CameraShake.Enabled.
    public class CameraShakeMenuOption : MonoBehaviour
    {
        public string Label = "Tremor da câmera";

        void Start()
        {
            var menu = FindAnyObjectByType<InGameMenuManager>(FindObjectsInactive.Include);
            if (!menu || !menu.ShadowsToggle)
                return;

            // A linha é o filho direto do grid (InnerPanel) que contém o toggle de sombras
            Transform row = menu.ShadowsToggle.transform;
            while (row.parent && !row.parent.GetComponent<LayoutGroup>())
                row = row.parent;
            if (!row.parent)
                return;

            GameObject copy = Instantiate(row.gameObject, row.parent);
            copy.name = "TremorDaCameraPanel";

            var toggle = copy.GetComponentInChildren<Toggle>(true);
            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = CameraShake.Enabled;
            toggle.onValueChanged.AddListener(on => CameraShake.Enabled = on);

            var tmp = copy.GetComponentInChildren<TMP_Text>(true);
            if (tmp)
                tmp.text = Label;
            var legacy = copy.GetComponentInChildren<Text>(true);
            if (legacy)
                legacy.text = Label;
        }
    }
}
