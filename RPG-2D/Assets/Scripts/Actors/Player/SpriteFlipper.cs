using UnityEngine;

public class SpriteFlipper : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform magicPointAttack;
    [SerializeField] private float magicPointFlipOffsetX = 0.15f;

    private Vector3 originalMagicPointLocalPos;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (magicPointAttack != null)
            originalMagicPointLocalPos = magicPointAttack.localPosition;
    }

    public void FaceDirection(float dirX)
    {
        if (dirX == 0f) return;

        bool lookingRight = dirX > 0f;

        // Flip visual (como já estava funcionando)
        spriteRenderer.flipX = lookingRight;

        if (magicPointAttack == null) return;

        Vector3 pos = originalMagicPointLocalPos;

        // Offset específico por direção
        if (lookingRight)
        {
            pos.x = originalMagicPointLocalPos.x + magicPointFlipOffsetX;
        }
        else
        {
            pos.x = originalMagicPointLocalPos.x - magicPointFlipOffsetX;
        }

        magicPointAttack.localPosition = pos;
    }

}
