using UnityEngine;

namespace Timbeaux.SFXPack
{
    public class SFXButton : MonoBehaviour
    {
        [SerializeField] private SFXManager previewManager;
        [SerializeField] private AudioClip audioClip;

        public void Play()
        {
            if (previewManager != null)
            {
                previewManager.PlaySound(audioClip);
            }
        }
    }
}