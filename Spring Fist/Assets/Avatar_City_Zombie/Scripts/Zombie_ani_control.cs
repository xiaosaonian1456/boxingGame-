using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Zombie_ani_control : MonoBehaviour
{
    Animator ZombieAni;
    // Start is called before the first frame update
    void Start()
    {
        ZombieAni = GetComponent<Animator>();
        ZombieAni.Play("idle");
    }

    public void ClickAniChange(int a)
    {
        if (a == 0) ZombieAni.Play("idle");
        if (a == 1) ZombieAni.Play("walk");
        if (a == 2) ZombieAni.Play("run");
        if (a == 3) ZombieAni.Play("attack");
        if (a == 4) ZombieAni.Play("bite");
        if (a == 5) ZombieAni.Play("bite2");
        if (a == 6) ZombieAni.Play("hit");
        if (a == 7) ZombieAni.Play("death");
    }
}
