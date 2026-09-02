using UnityEngine;

/// The shop stand in the camp. Opens the tool panel while the player is next to it.
public class ToolShopStand : MonoBehaviour
{
    [SerializeField] private EquipmentShopPanel panel;
    [SerializeField] private Transform player;
    [SerializeField] private ScalePop signPop;
    [SerializeField] private float interactionRadius = 2.6f;

    private bool playerWasInside;

    public void Configure(EquipmentShopPanel newPanel, Transform newPlayer, ScalePop newSignPop, float newRadius)
    {
        panel = newPanel;
        player = newPlayer;
        signPop = newSignPop;
        interactionRadius = newRadius;
    }

    private void Start()
    {
        if (player != null) return;
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null) player = movement.transform;
    }

    private void Update()
    {
        if (panel == null || player == null) return;

        bool inside = ProximityZone.Contains(transform.position, player.position, interactionRadius);
        if (inside == playerWasInside) return;

        playerWasInside = inside;
        if (inside)
        {
            if (signPop != null) signPop.Pop(0.26f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.42f, 0.72f, 0.98f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
