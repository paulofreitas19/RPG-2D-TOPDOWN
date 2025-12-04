//using UnityEngine;

//public class SlimeAnimationBridge : MonoBehaviour
//{
//    // Referência para o script principal, que está no objeto pai (Slime)
//    private Slime slime;

//    void Awake()
//    {
//        // Como o Animator está no filho e o Slime no pai,
//        // pegamos o componente subindo a hierarquia.
//        slime = GetComponentInParent<Slime>();
//    }

//    // ---------------------------------------------
//    // Estes dois métodos são chamados pelos
//    // Animation Events do clip "attack".
//    // O nome TEM que bater com o nome colocado no Event.
//    // ---------------------------------------------

//    // Chamado no momento em que o slime deve avançar
//    public void BeginAttackJump()
//    {
//        if (slime != null)
//            slime.BeginAttackJump();
//    }

//    // Chamado no momento em que o slime deve voltar
//    public void ReturnFromAttack()
//    {
//        if (slime != null)
//            slime.ReturnFromAttack();
//    }
//}
